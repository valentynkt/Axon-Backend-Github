using System.Security.Claims;
using System.Text.Json;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Axon.Modules.Identity.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;

namespace Axon.Modules.Identity.Infrastructure.Services.Tests;

[TestFixture]
public class DynamicClaimNormalizerTests
{
    private DynamicClaimNormalizer _normalizer = null!;
    private DynamicXyzOptions _options = null!;
    private ILogger<DynamicClaimNormalizer> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _options = new DynamicXyzOptions
        {
            EnvironmentId = "test-environment-id"
        };

        _logger = new Mock<ILogger<DynamicClaimNormalizer>>().Object;
        var optionsWrapper = Options.Create(_options);
        
        _normalizer = new DynamicClaimNormalizer(optionsWrapper, _logger);
    }

    [Test]
    public void NormalizeClaimsPrincipal_HappyPath_ReturnsCompleteUserData()
    {
        // Arrange
        var verifiedCredentials = JsonSerializer.Serialize(new[]
        {
            new
            {
                format = "blockchain",
                id = "wallet-id-123",
                address = "Sol1234567890abcdef",
                chain = "solana",
                wallet_name = "Phantom Wallet",
                wallet_provider = "phantom",
                lastSelectedAt = "2025-01-15T10:30:00.000Z"
            }
        });

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("email", "user@example.com"),
            new Claim("environment_id", "env-123"),
            new Claim("session_public_key", "session-key-abc"),
            new Claim("first_visit", "2025-01-10T08:00:00.000Z"),
            new Claim("last_visit", "2025-01-15T10:30:00.000Z"),
            new Claim("new_user", "false"),
            new Claim("verified_credentials", verifiedCredentials)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.UserId.Should().Be("user-123");
        result.Email.Should().Be("user@example.com");
        result.EnvironmentId.Should().Be("env-123");
        result.SessionPublicKey.Should().Be("session-key-abc");
        result.FirstVisitUtc.Should().NotBeNull();
        result.FirstVisitUtc!.Value.DateTime.Should().Be(new DateTime(2025, 1, 10, 8, 0, 0, DateTimeKind.Utc));
        result.LastVisitUtc.Should().NotBeNull();
        result.LastVisitUtc!.Value.DateTime.Should().Be(new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc));
        result.IsNewUser.Should().BeFalse();
        
        result.Wallets.Should().HaveCount(1);
        var wallet = result.Wallets[0];
        wallet.Id.Should().Be("wallet-id-123");
        wallet.Address.Should().Be("Sol1234567890abcdef");
        wallet.Chain.Should().Be("solana");
        wallet.WalletName.Should().Be("Phantom Wallet");
        wallet.Provider.Should().Be("phantom");
        wallet.ConnectedAtUtc.Should().NotBeNull();
        wallet.ConnectedAtUtc!.Value.DateTime.Should().Be(new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc));
    }

    [Test]
    public void NormalizeClaimsPrincipal_MultipleVerifiedCredentialsClaims_ProcessesAllClaims()
    {
        // Arrange - Two separate verified_credentials claims
        var firstCredentials = JsonSerializer.Serialize(new[]
        {
            new
            {
                format = "blockchain",
                id = "wallet-1",
                address = "Sol1111111111111111",
                chain = "solana",
                wallet_provider = "phantom"
            }
        });

        var secondCredentials = JsonSerializer.Serialize(new[]
        {
            new
            {
                format = "blockchain", 
                id = "wallet-2",
                address = "Eth2222222222222222",
                chain = "ethereum",
                wallet_provider = "metamask"
            }
        });

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("verified_credentials", firstCredentials),
            new Claim("verified_credentials", secondCredentials)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.Wallets.Count.Should().Be(2);
        result.Wallets.Should().Contain(w => w.Address == "Sol1111111111111111" && w.Chain == "solana");
        result.Wallets.Should().Contain(w => w.Address == "Eth2222222222222222" && w.Chain == "ethereum");
    }

    [Test]
    public void NormalizeClaimsPrincipal_MixedArrayAndObjectFormats_HandlesAllFormats()
    {
        // Arrange - Mix of array and single object formats
        var arrayCredentials = JsonSerializer.Serialize(new[]
        {
            new
            {
                format = "blockchain",
                address = "Sol1111111111111111",
                chain = "solana"
            },
            new
            {
                format = "blockchain", 
                address = "Sol3333333333333333",
                chain = "solana"
            }
        });

        var objectCredentials = JsonSerializer.Serialize(new
        {
            format = "blockchain",
            address = "Eth2222222222222222", 
            chain = "ethereum"
        });

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("verified_credentials", arrayCredentials),
            new Claim("verified_credentials", objectCredentials)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.Wallets.Count.Should().Be(3);
        result.Wallets.Should().Contain(w => w.Address == "Sol1111111111111111");
        result.Wallets.Should().Contain(w => w.Address == "Sol3333333333333333");
        result.Wallets.Should().Contain(w => w.Address == "Eth2222222222222222");
    }

    [Test]
    public void NormalizeClaimsPrincipal_EmailPrecedence_SelectsCorrectEmail()
    {
        // Arrange - Multiple email sources with precedence rules
        var emailCredentials = JsonSerializer.Serialize(new
        {
            format = "email",
            public_identifier = "credentials@example.com"
        });

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("email", "primary@example.com"), // Should win
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "claims@example.com"),
            new Claim("verified_credentials", emailCredentials)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.Email.Should().Be("primary@example.com");
    }

    [Test]
    public void NormalizeClaimsPrincipal_EmailPrecedence_FallsBackToClaimsType()
    {
        // Arrange - No primary email, should use ClaimTypes.Email
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "claims@example.com") // Should win
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.Email.Should().Be("claims@example.com");
    }

    [Test]
    public void NormalizeClaimsPrincipal_EmailPrecedence_FallsBackToCredentials()
    {
        // Arrange - Only email from verified_credentials
        var emailCredentials = JsonSerializer.Serialize(new
        {
            format = "email",
            public_identifier = "credentials@example.com"
        });

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("verified_credentials", emailCredentials)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.Email.Should().Be("credentials@example.com");
    }

    [Test]
    public void NormalizeClaimsPrincipal_EmailNormalization_LowercasesDomain()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("email", "User@EXAMPLE.COM")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.Email.Should().Be("User@example.com"); // Local part preserved, domain lowercased
    }

    [Test]
    public void NormalizeClaimsPrincipal_BadJsonInOneValue_ContinuesWithOthers()
    {
        // Arrange - One bad JSON, one good JSON
        var goodCredentials = JsonSerializer.Serialize(new
        {
            format = "blockchain",
            address = "Sol1111111111111111",
            chain = "solana"
        });

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("verified_credentials", "invalid-json-here"),
            new Claim("verified_credentials", goodCredentials)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.Wallets.Should().HaveCount(1);
        result.Wallets[0].Address.Should().Be("Sol1111111111111111");
        
        // Note: JSON parsing errors are logged but we don't verify logging in these basic tests
    }

    [Test]
    public void NormalizeClaimsPrincipal_MissingEnvironmentId_UsesFallback()
    {
        // Arrange - No environment_id claim
        var claims = new[]
        {
            new Claim("sub", "user-123")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.EnvironmentId.Should().Be("test-environment-id"); // From options
        
        // Note: Fallback usage is logged but we don't verify logging in these basic tests
    }

    [Test]
    public void NormalizeClaimsPrincipal_MissingEnvironmentIdAndNoFallback_LogsWarning()
    {
        // Arrange - No fallback configured
        _options.EnvironmentId = string.Empty;

        var claims = new[]
        {
            new Claim("sub", "user-123")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.EnvironmentId.Should().BeEmpty();
        
        // Note: Warning about missing environment is logged but we don't verify logging in these basic tests
    }

    [Test]
    public void NormalizeClaimsPrincipal_BooleanParsing_HandlesVariousFormats()
    {
        // Test cases for different boolean string formats
        var testCases = new[]
        {
            ("true", true),
            ("True", true),
            ("TRUE", true),
            ("false", false),
            ("False", false),
            ("FALSE", false),
            ("invalid", false) // Should default to false and warn
        };

        foreach (var (input, expected) in testCases)
        {
            // Arrange
            var claims = new[]
            {
                new Claim("sub", "user-123"),
                new Claim("new_user", input)
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = _normalizer.NormalizeClaimsPrincipal(principal);

            // Assert
            result.IsNewUser.Should().Be(expected, $"Input '{input}' should result in {expected}");
        }
    }

    [Test]
    public void NormalizeClaimsPrincipal_InvalidTimestamps_LogsWarningAndReturnsNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("first_visit", "invalid-timestamp"),
            new Claim("last_visit", "also-invalid")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.FirstVisitUtc.Should().BeNull();
        result.LastVisitUtc.Should().BeNull();
        
        // Note: Timestamp parsing failures are logged but we don't verify logging in these basic tests
    }

    [Test]
    public void NormalizeClaimsPrincipal_WalletDeduplication_PreferNewerTimestamp()
    {
        // Arrange - Same address/chain with different timestamps
        var olderWallet = new
        {
            format = "blockchain",
            address = "Sol1111111111111111",
            chain = "solana",
            wallet_provider = "phantom",
            lastSelectedAt = "2025-01-10T10:00:00.000Z"
        };

        var newerWallet = new
        {
            format = "blockchain",
            address = "Sol1111111111111111",
            chain = "solana",
            wallet_provider = "phantom",
            lastSelectedAt = "2025-01-15T10:00:00.000Z"
        };

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("verified_credentials", JsonSerializer.Serialize(olderWallet)),
            new Claim("verified_credentials", JsonSerializer.Serialize(newerWallet))
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.Wallets.Should().HaveCount(1);
        var wallet = result.Wallets[0];
        wallet.ConnectedAtUtc.Should().NotBeNull();
        wallet.ConnectedAtUtc!.Value.DateTime.Should().Be(new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void NormalizeClaimsPrincipal_WalletDeduplication_PreferRicherMetadata()
    {
        // Arrange - Same address/chain, same timestamp, different metadata richness
        var basicWallet = new
        {
            format = "blockchain",
            address = "Sol1111111111111111",
            chain = "solana"
        };

        var richWallet = new
        {
            format = "blockchain",
            address = "Sol1111111111111111", 
            chain = "solana",
            id = "wallet-123",
            wallet_name = "My Phantom",
            wallet_provider = "phantom"
        };

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("verified_credentials", JsonSerializer.Serialize(basicWallet)),
            new Claim("verified_credentials", JsonSerializer.Serialize(richWallet))
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.Wallets.Should().HaveCount(1);
        var wallet = result.Wallets[0];
        wallet.Id.Should().Be("wallet-123");
        wallet.WalletName.Should().Be("My Phantom");
        wallet.Provider.Should().Be("phantom");
    }

    [Test]
    public void NormalizeClaimsPrincipal_VerifiedCredentialsHashes_ParsesCorrectly()
    {
        // Arrange
        var hashes = new Dictionary<string, object>
        {
            ["blockchain"] = "hash123",
            ["email"] = "hash456"
        };

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("verifiedCredentialsHashes", JsonSerializer.Serialize(hashes))
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert
        result.VerifiedCredentialsHashes.Should().NotBeNull();
        result.VerifiedCredentialsHashes.Count.Should().Be(2);
        result.VerifiedCredentialsHashes.ContainsKey("blockchain").Should().BeTrue();
        result.VerifiedCredentialsHashes.ContainsKey("email").Should().BeTrue();
    }

    [Test]
    public void NormalizeClaimsPrincipal_EmptyOrMissingClaims_HandlesGracefully()
    {
        // Arrange - Minimal claims
        var claims = new[]
        {
            new Claim("sub", "user-123") // Only required claim
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert - Should not throw, should have sensible defaults
        result.UserId.Should().Be("user-123");
        result.Email.Should().BeEmpty();
        result.EnvironmentId.Should().Be("test-environment-id"); // From fallback
        result.FirstVisitUtc.Should().BeNull();
        result.LastVisitUtc.Should().BeNull();
        result.IsNewUser.Should().BeFalse();
        result.Wallets.Should().BeEmpty();
        result.SessionPublicKey.Should().BeNull();
        result.VerifiedCredentialsHashes.Should().BeNull();
    }

    [Test]
    public void NormalizeClaimsPrincipal_MappedClaims_HandlesNameIdentifierFallback()
    {
        // Arrange - Simulate ASP.NET claim mapping (sub -> nameidentifier, email -> emailaddress)
        var claims = new[]
        {
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "mapped-user-123"),
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "mapped@example.com"),
            new Claim("environment_id", "mapped-env-123")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert - Should use mapped claims as fallback
        result.UserId.Should().Be("mapped-user-123");
        result.Email.Should().Be("mapped@example.com");
        result.EnvironmentId.Should().Be("mapped-env-123");
    }

    [Test]
    public void NormalizeClaimsPrincipal_SubClaimTakesPrecedenceOverMapped()
    {
        // Arrange - Both JWT and mapped claims present
        var claims = new[]
        {
            new Claim("sub", "jwt-user-123"),
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "mapped-user-123"),
            new Claim("email", "jwt@example.com"),
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "mapped@example.com")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert - Should prefer JWT claims over mapped claims
        result.UserId.Should().Be("jwt-user-123");
        result.Email.Should().Be("jwt@example.com");
    }

    [Test]
    public void NormalizeClaimsPrincipal_WalletWithoutFormatField_DetectedSuccessfully()
    {
        // Arrange - Wallet credential without format field (Dynamic may omit it)
        var walletCredentials = JsonSerializer.Serialize(new
        {
            // No format field
            id = "wallet-without-format",
            address = "NoFormatWallet123456789",
            chain = "solana",
            wallet_name = "No Format Wallet",
            wallet_provider = "phantom"
        });

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("verified_credentials", walletCredentials)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert - Should detect wallet even without format field
        result.Wallets.Should().HaveCount(1);
        var wallet = result.Wallets[0];
        wallet.Id.Should().Be("wallet-without-format");
        wallet.Address.Should().Be("NoFormatWallet123456789");
        wallet.Chain.Should().Be("solana");
        wallet.WalletName.Should().Be("No Format Wallet");
        wallet.Provider.Should().Be("phantom");
    }

    [Test]
    public void NormalizeClaimsPrincipal_MixedCredentialsWithAndWithoutFormat_FiltersCorrectly()
    {
        // Arrange - Mix of credentials with and without format field
        var mixedCredentials = JsonSerializer.Serialize(new object[]
        {
            new
            {
                format = "blockchain",
                address = "BlockchainWallet123",
                chain = "ethereum"
            },
            new
            {
                // No format field - should be detected as blockchain due to address+chain
                address = "NoFormatWallet456", 
                chain = "solana"
            },
            new
            {
                format = "email",
                public_identifier = "test@example.com"
                // No address/chain - should be skipped for wallet detection
            }
        });

        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("verified_credentials", mixedCredentials)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = _normalizer.NormalizeClaimsPrincipal(principal);

        // Assert - Should find 2 wallets (blockchain entries only)
        result.Wallets.Should().HaveCount(2);
        result.Wallets.Should().Contain(w => w.Address == "BlockchainWallet123" && w.Chain == "ethereum");
        result.Wallets.Should().Contain(w => w.Address == "NoFormatWallet456" && w.Chain == "solana");
    }
}