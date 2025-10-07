using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Providers;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Providers.WalletAuthenticationProvider;

/// <summary>
/// Comprehensive tests for WalletAuthenticationProvider - wallet signature authentication.
/// Tests core authentication flow, challenge validation, and principal resolution.
/// </summary>
[TestFixture]
public class WalletAuthenticationProviderTests
{
    private IWalletSignatureVerifier _signatureVerifier = null!;
    private IPrincipalResolutionService _resolutionService = null!;
    private UserManager<AxonUserAuth> _userManager = null!;
    private IChallengeValidationService _challengeValidationService = null!;
    private ILogger<Axon.Modules.Identity.Application.Providers.WalletAuthenticationProvider> _logger = null!;
    private Axon.Modules.Identity.Application.Providers.WalletAuthenticationProvider _provider = null!;

    private static readonly string TestChainId = "ethereum";
    private static readonly string TestAddress = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb1";
    private static readonly string TestSignedMessage = "{\"chain_id\":\"ethereum\",\"wallet_address\":\"0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb1\",\"aud\":\"axon-challenge\"}";
    private static readonly string TestSignature = "0xabcdef1234567890";

    [SetUp]
    public void SetUp()
    {
        _signatureVerifier = Substitute.For<IWalletSignatureVerifier>();
        _resolutionService = Substitute.For<IPrincipalResolutionService>();
        _challengeValidationService = Substitute.For<IChallengeValidationService>();
        _logger = Substitute.For<ILogger<Axon.Modules.Identity.Application.Providers.WalletAuthenticationProvider>>();

        // Setup UserManager mock
        var userStore = Substitute.For<IUserStore<AxonUserAuth>>();
        _userManager = Substitute.For<UserManager<AxonUserAuth>>(
            userStore, null, null, null, null, null, null, null, null);

        _provider = new Axon.Modules.Identity.Application.Providers.WalletAuthenticationProvider(
            _signatureVerifier,
            _resolutionService,
            _userManager,
            _challengeValidationService,
            _logger);
    }

    #region CanHandle Tests

    [Test]
    public void CanHandle_WithWalletRequest_ShouldReturnTrue()
    {
        // Arrange
        var request = CreateWalletAuthenticationRequest();

        // Act
        var result = _provider.CanHandle(request);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void CanHandle_WithNonWalletRequest_ShouldReturnFalse()
    {
        // Arrange
        var request = Substitute.For<AuthenticationRequest>();

        // Act
        var result = _provider.CanHandle(request);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void ProviderType_ShouldReturnWallet()
    {
        // Assert
        _provider.ProviderType.ShouldBe("wallet");
    }

    #endregion

    #region Happy Path Authentication Tests

    [Test]
    public async Task AuthenticateAsync_WithValidWalletSignature_ShouldReturnAuthenticationData()
    {
        // Arrange
        var request = CreateWalletAuthenticationRequest();

        // Setup challenge validation
        _challengeValidationService.ValidateWalletChallengeAsync(default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));

        // Setup signature verification
        _signatureVerifier.VerifySignature(default!, default!, default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));

        // Setup resolution service to create new principal
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = AxonPrincipal.CreateHuman(principalId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);

        _resolutionService.ResolveAsync(default!, default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));

        // Setup identity user
        var user = AxonUserAuth.Create(
            principal.Id,
            "wallet",
            "axon",
            TestAddress,
            null,
            null);

        _userManager.GetUsersForClaimAsync(default!)
            .ReturnsForAnyArgs(Task.FromResult<IList<AxonUserAuth>>(new List<AxonUserAuth> { user }));

        _userManager.UpdateAsync(default!)
            .ReturnsForAnyArgs(IdentityResult.Success);

        // Act
        var result = await _provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.User.ShouldNotBeNull();
        result.Value.ProviderType.ShouldBe("wallet");
        result.Value.AdditionalClaims.ShouldContainKey("chain_id");
        result.Value.AdditionalClaims.ShouldContainKey("wallet_address");
        result.Value.AdditionalClaims.ShouldContainKey("auth_method");
    }

    [Test]
    public async Task AuthenticateAsync_WithExistingPrincipal_ShouldReuseExisting()
    {
        // Arrange
        var request = CreateWalletAuthenticationRequest();

        // Setup challenge validation
        _challengeValidationService.ValidateWalletChallengeAsync(default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));

        // Setup signature verification
        _signatureVerifier.VerifySignature(default!, default!, default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));

        // Setup resolution service to return existing principal via wallet
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = AxonPrincipal.CreateHuman(principalId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Wallet,
            WasAutoLinked: false);

        _resolutionService.ResolveAsync(default!, default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));

        // Setup identity user
        var user = AxonUserAuth.Create(
            principal.Id,
            "wallet",
            "axon",
            TestAddress,
            null,
            null);

        _userManager.GetUsersForClaimAsync(default!)
            .ReturnsForAnyArgs(Task.FromResult<IList<AxonUserAuth>>(new List<AxonUserAuth> { user }));

        _userManager.UpdateAsync(default!)
            .ReturnsForAnyArgs(IdentityResult.Success);

        // Act
        var result = await _provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        // Verify resolution service was called exactly once
        await _resolutionService.Received(1).ResolveAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ChainId>(),
            Arg.Any<Address>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AuthenticateAsync_ShouldUpdateLastAuthenticatedTimestamp()
    {
        // Arrange
        var request = CreateWalletAuthenticationRequest();

        // Setup challenge validation
        _challengeValidationService.ValidateWalletChallengeAsync(default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));

        // Setup signature verification
        _signatureVerifier.VerifySignature(default!, default!, default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));

        // Setup resolution service to create new principal
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = AxonPrincipal.CreateHuman(principalId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);

        _resolutionService.ResolveAsync(default!, default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));

        // Setup identity user
        var identityUser = AxonUserAuth.Create(
            principal.Id,
            "wallet",
            "axon",
            TestAddress,
            null,
            null);

        _userManager.GetUsersForClaimAsync(default!)
            .ReturnsForAnyArgs(Task.FromResult<IList<AxonUserAuth>>(new List<AxonUserAuth> { identityUser }));

        _userManager.UpdateAsync(default!)
            .ReturnsForAnyArgs(IdentityResult.Success);

        // Act
        var result = await _provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _userManager.Received(1).UpdateAsync(Arg.Is<AxonUserAuth>(u => u.Id == identityUser.Id));
    }

    [Test]
    public async Task AuthenticateAsync_WithNewWallet_ShouldCallResolutionService()
    {
        // Arrange
        var request = CreateWalletAuthenticationRequest();

        // Setup challenge validation
        _challengeValidationService.ValidateWalletChallengeAsync(default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));

        // Setup signature verification
        _signatureVerifier.VerifySignature(default!, default!, default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));

        // Setup resolution service to create new principal
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = AxonPrincipal.CreateHuman(principalId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);

        _resolutionService.ResolveAsync(default!, default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));

        _userManager.GetUsersForClaimAsync(default!)
            .ReturnsForAnyArgs(Task.FromResult<IList<AxonUserAuth>>(new List<AxonUserAuth>()));

        _userManager.CreateAsync(default!)
            .ReturnsForAnyArgs(IdentityResult.Success);

        _userManager.AddClaimAsync(default!, default!)
            .ReturnsForAnyArgs(IdentityResult.Success);

        _userManager.UpdateAsync(default!)
            .ReturnsForAnyArgs(IdentityResult.Success);

        // Act
        var result = await _provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _resolutionService.Received(1).ResolveAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ChainId>(),
            Arg.Any<Address>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task AuthenticateAsync_WithInvalidRequestType_ShouldReturnValidationError()
    {
        // Arrange
        var request = Substitute.For<AuthenticationRequest>();

        // Act
        var result = await _provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Test]
    public async Task AuthenticateAsync_WithInvalidSignature_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = CreateWalletAuthenticationRequest();

        // Setup challenge validation
        _challengeValidationService.ValidateWalletChallengeAsync(default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));

        // Setup signature verification to return false (invalid signature)
        _signatureVerifier.VerifySignature(default!, default!, default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(false));

        // Act
        var result = await _provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Message.ShouldContain("Invalid wallet signature");
    }

    [Test]
    public async Task AuthenticateAsync_WithFailedChallengeValidation_ShouldReturnValidationError()
    {
        // Arrange
        var request = CreateWalletAuthenticationRequest();

        // Setup challenge validation to fail
        _challengeValidationService.ValidateWalletChallengeAsync(default!, default!)
            .ReturnsForAnyArgs(Result.Failure<bool, Error>(Error.Validation("Invalid challenge")));

        // Act
        var result = await _provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Test]
    public async Task AuthenticateAsync_WhenIdentityUserCreationFails_ShouldReturnInternal()
    {
        // Arrange
        var request = CreateWalletAuthenticationRequest();

        // Setup challenge validation
        _challengeValidationService.ValidateWalletChallengeAsync(default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));

        // Setup signature verification
        _signatureVerifier.VerifySignature(default!, default!, default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));

        // Setup resolution service to create new principal
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = AxonPrincipal.CreateHuman(principalId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);

        _resolutionService.ResolveAsync(default!, default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));

        // Setup identity user creation to fail
        _userManager.GetUsersForClaimAsync(default!)
            .ReturnsForAnyArgs(Task.FromResult<IList<AxonUserAuth>>(new List<AxonUserAuth>()));

        _userManager.CreateAsync(default!)
            .ReturnsForAnyArgs(IdentityResult.Failed(new IdentityError { Description = "Creation failed" }));

        // Act
        var result = await _provider.AuthenticateAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }

    #endregion

    #region Helper Methods

    private static WalletAuthenticationRequest CreateWalletAuthenticationRequest()
    {
        return new WalletAuthenticationRequest(
            ChainId: TestChainId,
            Address: TestAddress,
            SignedMessage: TestSignedMessage,
            Signature: TestSignature);
    }

    private static ChainId AnyChainId() => ChainId.From(TestChainId);
    private static Address AnyAddress() => Address.Create(TestAddress).Value;

    private void SetupSuccessfulChallengeValidation()
    {
        _challengeValidationService.ValidateWalletChallengeAsync(default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));
    }

    private void SetupSuccessfulSignatureVerification()
    {
        _signatureVerifier.VerifySignature(default!, default!, default!, default!)
            .ReturnsForAnyArgs(Result.Success<bool, Error>(true));
    }

    private void SetupResolutionServiceWithExistingPrincipal(out AxonPrincipal principal)
    {
        var principalId = new AxonUserId(Guid.NewGuid());
        principal = AxonPrincipal.CreateHuman(principalId);

        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Wallet,
            WasAutoLinked: false);

        _resolutionService.ResolveAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ChainId>(),
            Arg.Any<Address>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));
    }

    private void SetupResolutionServiceWithNewPrincipal(out AxonPrincipal principal)
    {
        var principalId = new AxonUserId(Guid.NewGuid());
        principal = AxonPrincipal.CreateHuman(principalId);

        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);

        _resolutionService.ResolveAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ChainId>(),
            Arg.Any<Address>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));
    }

    private void SetupNewWalletScenario()
    {
        // Setup resolution service for new principal
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = AxonPrincipal.CreateHuman(principalId);
        var resolutionResult = new PrincipalResolutionResult(
            Principal: principal,
            Path: ResolutionPath.Created,
            WasAutoLinked: false);

        _resolutionService.ResolveAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ChainId>(),
            Arg.Any<Address>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<PrincipalResolutionResult, Error>(resolutionResult));

        // Identity user mocks - use ReturnsForAnyArgs
        _userManager.GetUsersForClaimAsync(default!)
            .ReturnsForAnyArgs(Task.FromResult<IList<AxonUserAuth>>(new List<AxonUserAuth>()));

        _userManager.CreateAsync(default!)
            .ReturnsForAnyArgs(IdentityResult.Success);

        _userManager.AddClaimAsync(default!, default!)
            .ReturnsForAnyArgs(IdentityResult.Success);

        _userManager.UpdateAsync(default!)
            .ReturnsForAnyArgs(IdentityResult.Success);
    }

    private AxonUserAuth SetupSuccessfulIdentityUserCreation(AxonPrincipal principal)
    {
        var user = AxonUserAuth.Create(
            principal.Id,
            "wallet",
            "axon",
            TestAddress,
            null,
            null);

        _userManager.GetUsersForClaimAsync(default!)
            .ReturnsForAnyArgs(Task.FromResult<IList<AxonUserAuth>>(new List<AxonUserAuth> { user }));

        _userManager.UpdateAsync(default!)
            .ReturnsForAnyArgs(IdentityResult.Success);

        return user;
    }

    #endregion
}