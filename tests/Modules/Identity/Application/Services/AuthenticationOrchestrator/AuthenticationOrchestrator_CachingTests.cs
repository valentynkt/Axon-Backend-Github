using System.Text;
using Axon.Modules.Identity.Application.Tests.Services.AuthenticationOrchestrator._TestInfrastructure;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Services.AuthenticationOrchestrator;

/// <summary>
/// Tests for AuthenticationOrchestrator caching behavior.
/// Verifies JTI replay protection using distributed cache for refresh tokens.
///
/// CACHING STRATEGY:
/// - Cache key format: "refresh:used:{jti}"
/// - Purpose: Prevent refresh token replay attacks
/// - Expiration: 30 days sliding expiration
/// </summary>
[TestFixture]
public class AuthenticationOrchestrator_CachingTests : AuthenticationOrchestratorTestBase
{
    [Test]
    public async Task RefreshToken_WithValidToken_Should_MarkTokenAsUsedInCache()
    {
        // Arrange
        var refreshToken = "valid-refresh-token";
        var jti = "test-jti-123";
        var userId = new AxonUserId(Guid.NewGuid());
        var user = CreateAxonUserAuth(principalId: userId);

        RefreshTokenProvider.GetJtiFromToken(refreshToken).Returns(jti);        RefreshTokenProvider.GetUserIdFromToken(refreshToken).Returns(userId.Value);
        UserManager.FindByIdAsync(userId.Value.ToString()).Returns(user);
        RefreshTokenProvider.ValidateAsync("RefreshToken", refreshToken, UserManager, user).Returns(true);
        RefreshTokenProvider.GenerateAsync("RefreshToken", UserManager, user).Returns("new-refresh-token");

        // Mock cache returns null (token not used yet)
        // GetStringAsync is an extension that wraps GetAsync, so mock GetAsync
        Cache.GetAsync($"refresh:used:{jti}", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));

        // Act
        var result = await Orchestrator.RefreshTokenAsync(refreshToken, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        // Verify token was marked as used in cache with 30-day sliding expiration
        // SetStringAsync is an extension that wraps SetAsync, so verify SetAsync was called
        await Cache.Received(1).SetAsync(
            $"refresh:used:{jti}",
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(opts => 
                opts.SlidingExpiration == TimeSpan.FromDays(30)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RefreshToken_WithPreviouslyUsedToken_Should_ReturnUnauthorized()
    {
        // Arrange - Token already used (exists in cache)
        var refreshToken = "already-used-token";
        var jti = "used-jti-456";

        RefreshTokenProvider.GetJtiFromToken(refreshToken).Returns(jti);

        // Mock cache returns "used" (token already used)
        // GetStringAsync wraps GetAsync, so mock GetAsync with byte[] encoding of "used"
        Cache.GetAsync($"refresh:used:{jti}", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes("used")));

        // Act
        var result = await Orchestrator.RefreshTokenAsync(refreshToken, CancellationToken.None);

        // Assert - Should fail with replay detection
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Message.ShouldBe("Refresh token already used");
        
        // Verify no new token was generated or cached
        await RefreshTokenProvider.DidNotReceive().GenerateAsync(
            Arg.Any<string>(), 
            Arg.Any<Microsoft.AspNetCore.Identity.UserManager<Domain.Entities.AxonUserAuth>>(), 
            Arg.Any<Domain.Entities.AxonUserAuth>());
    }

    [Test]
    public async Task RefreshToken_WithEmptyJti_Should_NotCheckCache()
    {        // Arrange - Invalid token with no JTI
        var refreshToken = "invalid-token-no-jti";

        RefreshTokenProvider.GetJtiFromToken(refreshToken).Returns(string.Empty);

        // Act
        var result = await Orchestrator.RefreshTokenAsync(refreshToken, CancellationToken.None);

        // Assert - Should fail before cache check
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Message.ShouldBe("Invalid refresh token");
        
        // Verify cache was never accessed (early validation failure)
        await Cache.DidNotReceive().GetAsync(
            Arg.Any<string>(), 
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InvalidateSession_Should_RemoveUserCacheEntries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateAxonUserAuth(principalId: new AxonUserId(userId));

        UserManager.FindByIdAsync(userId.ToString()).Returns(user);
        UserManager.UpdateSecurityStampAsync(user)
            .Returns(Microsoft.AspNetCore.Identity.IdentityResult.Success);

        // Act
        var result = await Orchestrator.InvalidateSessionAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        // Verify cache entries for user were removed
        await Cache.Received(1).RemoveAsync(
            $"user:session:{userId}",
            Arg.Any<CancellationToken>());
    }
}