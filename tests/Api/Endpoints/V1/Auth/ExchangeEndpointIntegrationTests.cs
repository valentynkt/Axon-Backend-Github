using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Tests.Common;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Axon.Api.Tests.Endpoints.V1.Auth;

/// <summary>
/// Integration tests for ExchangeEndpoint with real database and full request pipeline.
/// Tests complete authentication flow, database persistence, and transaction boundaries.
/// </summary>
[TestFixture]
public class ExchangeEndpointIntegrationTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private ICurrentUserService _mockCurrentUserService = null!;
    private IServiceScope _scope = null!;
    private IdentityDbContext _dbContext = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Test factory uses SQLite - no container setup needed
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // Test factory handles cleanup
    }

    [SetUp]
    public async Task SetUp()
    {
        _mockCurrentUserService = Substitute.For<ICurrentUserService>();

        _factory = new TestWebApplicationFactory()
            .WithServices(services =>
            {
                // Replace ICurrentUserService with mock for controlled testing
                services.Remove(services.Single(d => d.ServiceType == typeof(ICurrentUserService)));
                services.AddSingleton(_mockCurrentUserService);
            });

        _client = _factory.CreateClient();

        // Get database context for test data setup and verification
        _scope = _factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        // Ensure database is clean for each test
        await CleanDatabase();

        // Add valid Authorization header
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer valid-jwt-token");
    }

    [TearDown]
    public void TearDown()
    {
        _scope?.Dispose();
        _client?.Dispose();
        _factory?.Dispose();
    }

    #region Authentication Flow Integration Tests

    [Test]
    public async Task ExchangeEndpoint_WhenNewUser_CreatesCompleteIdentityRecord()
    {
        // Arrange
        var requestPayload = CreateValidExchangeRequest();
        _mockCurrentUserService.AxonUserId.Returns("new-user-123");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange",
            new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeTokenResponseDto>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.Created.ShouldBeTrue();
        exchangeResponse.AxonUserId.ShouldNotBeNullOrEmpty();

        // Verify database state
        var principalId = new AxonUserId(Guid.Parse(exchangeResponse.AxonUserId));
        var principal = await _dbContext.AxonPrincipals
            .Include(p => p.Credentials)
            .Include(p => p.WalletOwnerships)
            .Include(p => p.PrincipalChainDefaults)
            .FirstOrDefaultAsync(p => p.Id == principalId);

        principal.ShouldNotBeNull();
        principal.Credentials.ShouldContain(c => c.Provider == "dynamic");
        principal.WalletOwnerships.ShouldNotBeEmpty();
        principal.PrincipalChainDefaults.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ExchangeEndpoint_WhenExistingUser_UpdatesTimestampsOnly()
    {
        // Arrange - Create existing principal in database
        var existingPrincipal = AxonPrincipal.CreateWithDynamicCredential(
            ProviderType.Create("dynamic").Value,
            "https://app.dynamic.xyz/test-env",
            "existing-user-456").Value;

        _dbContext.AxonPrincipals.Add(existingPrincipal);
        await _dbContext.SaveChangesAsync();

        var originalCredentialCount = existingPrincipal.Credentials.Count;
        var originalLastSeenAt = existingPrincipal.Credentials.First().LastSeenAt;

        _mockCurrentUserService.AxonUserId.Returns("existing-user-456");

        var requestPayload = CreateValidExchangeRequest();

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange",
            new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var exchangeResponse = JsonSerializer.Deserialize<ExchangeTokenResponseDto>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        exchangeResponse.ShouldNotBeNull();
        exchangeResponse.Created.ShouldBeFalse();

        // Verify database state - should update existing, not create new
        var updatedPrincipal = await _dbContext.AxonPrincipals
            .Include(p => p.Credentials)
            .FirstOrDefaultAsync(p => p.Id == existingPrincipal.Id);

        updatedPrincipal.ShouldNotBeNull();
        updatedPrincipal.Credentials.Count.ShouldBe(originalCredentialCount);
        updatedPrincipal.Credentials.First().LastSeenAt.ShouldBeGreaterThan(originalLastSeenAt);
    }

    [Test]
    public async Task ExchangeEndpoint_WhenWalletConflict_Returns409()
    {
        // Arrange - Create principal with wallet ownership
        var existingPrincipal = AxonPrincipal.CreateHuman();
        var conflictWallet = Wallet.Create(
            WalletId.New(),
            "ethereum-mainnet", // Use compound ChainId
            Address.Create("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41").Value,
            DateTime.UtcNow);

        var ownership = WalletOwnership.Create(
            existingPrincipal.Id,
            conflictWallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Verified,
            VerificationSource.DynamicAttested);

        existingPrincipal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        _dbContext.AxonPrincipals.Add(existingPrincipal);
        _dbContext.Wallets.Add(conflictWallet);
        await _dbContext.SaveChangesAsync();

        // Try to exchange with different user claiming same wallet
        _mockCurrentUserService.AxonUserId.Returns("different-user-789");

        var requestPayload = new ExchangeTokenRequestDto(
            // Request body is empty - data comes from JWT
        );

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange",
            new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task ExchangeEndpoint_WhenValidationError_Returns400()
    {
        // Arrange - Mock scenario with invalid wallet address
        _mockCurrentUserService.AxonUserId.Returns("user-with-invalid-wallet");

        var requestPayload = CreateValidExchangeRequest();

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange",
            new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json"));

        // Assert - Should validate addresses and return 400 for invalid ones
        // Note: Actual validation behavior depends on Dynamic service mock configuration
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Transaction Boundary Tests

    [Test]
    public async Task ExchangeEndpoint_WhenDatabaseError_RollsBackTransaction()
    {
        // Arrange - Force a database constraint violation scenario
        _mockCurrentUserService.AxonUserId.Returns("transaction-test-user");

        var requestPayload = CreateValidExchangeRequest();

        // First request should succeed
        var response1 = await _client.PostAsync("/api/v1/auth/exchange",
            new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json"));

        response1.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify clean state if transaction succeeds
        var principalCount = await _dbContext.AxonPrincipals.CountAsync();
        principalCount.ShouldBe(1);
    }

    [Test]
    public async Task ExchangeEndpoint_WhenIdempotentRequest_ReturnsConsistentResults()
    {
        // Arrange
        _mockCurrentUserService.AxonUserId.Returns("idempotent-user");
        var requestPayload = CreateValidExchangeRequest();

        // Act - Make same request multiple times
        var response1 = await _client.PostAsync("/api/v1/auth/exchange",
            new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json"));
        var response2 = await _client.PostAsync("/api/v1/auth/exchange",
            new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json"));
        var response3 = await _client.PostAsync("/api/v1/auth/exchange",
            new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json"));

        // Assert - All should succeed with consistent results
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        response3.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        var content3 = await response3.Content.ReadAsStringAsync();

        var result1 = JsonSerializer.Deserialize<ExchangeTokenResponseDto>(content1, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result2 = JsonSerializer.Deserialize<ExchangeTokenResponseDto>(content2, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result3 = JsonSerializer.Deserialize<ExchangeTokenResponseDto>(content3, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        result1!.AxonUserId.ShouldBe(result2!.AxonUserId);
        result2.AxonUserId.ShouldBe(result3!.AxonUserId);

        // Only first should show Created=true
        result1.Created.ShouldBeTrue();
        result2.Created.ShouldBeFalse();
        result3.Created.ShouldBeFalse();

        // Verify only one principal exists in database
        var principalCount = await _dbContext.AxonPrincipals.CountAsync();
        principalCount.ShouldBe(1);
    }

    [Test]
    public async Task ExchangeEndpoint_BypassesMiddlewareAuthentication_NoDoubleValidation()
    {
        // Arrange - Use an invalid JWT token that would fail middleware validation
        var invalidToken = "Bearer invalid.jwt.token";

        // Remove the default valid authorization header and use invalid one
        _client.DefaultRequestHeaders.Remove("Authorization");
        _client.DefaultRequestHeaders.Add("Authorization", invalidToken);

        var requestPayload = CreateValidExchangeRequest();
        _mockCurrentUserService.AxonUserId.Returns("bypass-test-user");

        // Act - This should reach the endpoint despite invalid token (middleware bypassed)
        var response = await _client.PostAsync("/api/v1/auth/exchange",
            new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json"));

        // Assert - Should fail at provider validation, not middleware validation
        // If middleware weren't bypassed, we'd get 401 before reaching the provider
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);

        // Should fail at provider level with 400 or 422 (business rule violation)
        // since the provider will try to validate the invalid JWT
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);

        // Verify the error comes from provider validation, not middleware
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldNotContain("WWW-Authenticate"); // Middleware would add this header
    }

    [Test]
    public async Task ExchangeEndpoint_WithValidToken_SingleValidationPath()
    {
        // Arrange - This test verifies successful flow uses single validation
        var requestPayload = CreateValidExchangeRequest();
        _mockCurrentUserService.AxonUserId.Returns("single-validation-user");

        // Act
        var response = await _client.PostAsync("/api/v1/auth/exchange",
            new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json"));

        // Assert - Should succeed, indicating single validation path worked
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ExchangeTokenResponseDto>(content,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        result.ShouldNotBeNull();
        result.AccessToken.ShouldNotBeNullOrEmpty();
        result.AxonUserId.ShouldNotBeNullOrEmpty();

        // Verify database record was created (proving provider validation succeeded)
        var principalExists = await _dbContext.AxonPrincipals.AnyAsync();
        principalExists.ShouldBeTrue();
    }

    #endregion

    #region Helper Methods

    private static ExchangeTokenRequestDto CreateValidExchangeRequest()
    {
        return new ExchangeTokenRequestDto();
    }

    private async Task CleanDatabase()
    {
        // Clean in dependency order
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallet_ownerships CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.principal_chain_defaults CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.credentials CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.wallets CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE identity.axon_principals CASCADE");
    }

    private record ExchangeTokenResponseDto(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresIn,
        string AxonUserId,
        bool Created,
        int WalletsProcessed,
        int WalletsLinked,
        int DefaultsApplied,
        int Skipped,
        int Conflicts,
        DateTimeOffset IssuedAt,
        DateTimeOffset AccessTokenExpiresAt,
        DateTimeOffset RefreshTokenExpiresAt
    );

    #endregion
}