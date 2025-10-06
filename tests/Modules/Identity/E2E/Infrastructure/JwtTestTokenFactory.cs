using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;
using Microsoft.IdentityModel.Tokens;

namespace Axon.Modules.Identity.E2E.Infrastructure;

/// <summary>
/// Factory for creating test JWT tokens and JWKS for Dynamic authentication testing.
/// Uses Ed25519 signatures for cryptographic validity and supports key rotation scenarios.
/// </summary>
public static class JwtTestTokenFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    // Test key IDs for rotation scenarios
    public const string TestKid1 = TestDataFixtures.Kid1;
    public const string TestKid2 = TestDataFixtures.Kid2;

    // RSA test keys for JWT signing (matching the actual token generation)
    // These are cached to ensure consistent keys across JWKS and token generation
    private static readonly Lazy<RSA> TestRsa1 = new(() => RSA.Create(2048));
    private static readonly Lazy<RSA> TestRsa2 = new(() => RSA.Create(2048));

    #region JWT Token Creation

    /// <summary>
    /// Creates a valid Dynamic JWT with standard claims.
    /// Uses real system time for JWT timestamps to work with JWT middleware validation.
    /// Application logic (wallet signatures, etc.) still uses FakeTimeProvider for deterministic testing.
    /// </summary>
    public static string CreateValidDynamicJwt(
        string subject = TestDataFixtures.DynA_Subject,
        string issuer = TestDataFixtures.DynamicIssuer,
        string audience = "axon-api",
        string kid = TestKid1,
        DateTime? expiration = null,
        Dictionary<string, object>? additionalClaims = null)
    {
        // Use real system time for JWT claims (middleware doesn't respect FakeTimeProvider)
        // This allows proper JWT validation while keeping deterministic testing for application logic
        var exp = expiration ?? DateTime.UtcNow.AddHours(1);
        var iat = DateTime.UtcNow.AddMinutes(-5);

        var claims = new List<Claim>
        {
            new("sub", subject),
            new("iss", issuer),
            new("aud", audience),
            new("iat", new DateTimeOffset(iat).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("exp", new DateTimeOffset(exp).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("jti", Guid.NewGuid().ToString())
        };

        // Add additional claims if provided
        if (additionalClaims != null)
        {
            foreach (var claim in additionalClaims)
            {
                claims.Add(new Claim(claim.Key, claim.Value?.ToString() ?? ""));
            }
        }

        return CreateJwtToken(claims, kid);
    }

    /// <summary>
    /// Creates a JWT with invalid issuer for testing issuer validation.
    /// </summary>
    public static string CreateInvalidIssuerJwt(
        string subject = TestDataFixtures.DynA_Subject,
        string invalidIssuer = "https://evil.example.com")
    {
        return CreateValidDynamicJwt(subject: subject, issuer: invalidIssuer);
    }

    /// <summary>
    /// Creates a JWT with invalid audience for testing audience validation.
    /// </summary>
    public static string CreateInvalidAudienceJwt(
        string subject = TestDataFixtures.DynA_Subject,
        string invalidAudience = "wrong-audience")
    {
        return CreateValidDynamicJwt(subject: subject, audience: invalidAudience);
    }

    /// <summary>
    /// Creates an expired JWT for testing TTL validation.
    /// Token expired 1 hour ago (real system time).
    /// </summary>
    public static string CreateExpiredJwt(
        string subject = TestDataFixtures.DynA_Subject)
    {
        var expiredTime = DateTime.UtcNow.AddHours(-1);
        return CreateValidDynamicJwt(subject: subject, expiration: expiredTime);
    }

    /// <summary>
    /// Creates a JWT with unknown key ID for testing JWKS key rotation.
    /// </summary>
    public static string CreateUnknownKidJwt(
        string subject = TestDataFixtures.DynA_Subject,
        string unknownKid = "unknown_kid_123")
    {
        return CreateValidDynamicJwt(subject: subject, kid: unknownKid);
    }

    /// <summary>
    /// Creates a JWT with the rotated key (kid2) for testing key rotation scenarios.
    /// </summary>
    public static string CreateRotatedKeyJwt(
        string subject = TestDataFixtures.DynA_Subject)
    {
        return CreateValidDynamicJwt(subject: subject, kid: TestKid2);
    }

    /// <summary>
    /// Creates a JWT that will be valid after JWKS rotation.
    /// Token valid for 30 minutes from now (real system time).
    /// </summary>
    public static string CreateFutureValidJwt(
        string subject = TestDataFixtures.DynA_Subject)
    {
        var futureValid = DateTime.UtcNow.AddMinutes(30);
        return CreateValidDynamicJwt(subject: subject, expiration: futureValid, kid: TestKid2);
    }

    /// <summary>
    /// Creates a valid Dynamic JWT with wallet data in verified_credentials claim.
    /// </summary>
    public static string CreateValidDynamicJwtWithWallets(
        string subject = TestDataFixtures.DynA_Subject,
        string issuer = TestDataFixtures.DynamicIssuer,
        string audience = "axon-api",
        string kid = TestKid1,
        DateTime? expiration = null,
        List<(string address, string chain, string? walletName, string? provider)>? wallets = null)
    {
        // Build verified_credentials array with wallet data
        var credentials = wallets ?? DefaultTestWallets();
        var verifiedCredentials = BuildVerifiedCredentialsJson(credentials);

        var additionalClaims = new Dictionary<string, object>
        {
            ["verified_credentials"] = verifiedCredentials,
            ["environment_id"] = TestDataFixtures.DynamicEnvironmentId
        };

        return CreateValidDynamicJwt(subject: subject, issuer: issuer, audience: audience,
                                     kid: kid, expiration: expiration, additionalClaims: additionalClaims);
    }

    /// <summary>
    /// Gets default test wallets (W1 and W2 on Solana mainnet).
    /// </summary>
    private static List<(string address, string chain, string? walletName, string? provider)> DefaultTestWallets() =>
        new()
        {
            (TestDataFixtures.W1MainAddress, TestDataFixtures.SolanaMainnetChain, "Phantom", "phantom"),
            (TestDataFixtures.W2MainAddress, TestDataFixtures.SolanaMainnetChain, "Phantom", "phantom")
        };

    /// <summary>
    /// Builds verified_credentials JSON array for inclusion in JWT claims.
    /// </summary>
    private static string BuildVerifiedCredentialsJson(
        List<(string address, string chain, string? walletName, string? provider)> wallets)
    {
        var credentials = wallets.Select((w, i) => new
        {
            format = "blockchain",
            address = w.address,
            chain = w.chain,
            wallet_name = w.walletName,
            wallet_provider = w.provider,
            id = $"wallet_{i + 1}"
        }).ToArray();

        return JsonSerializer.Serialize(credentials, JsonOptions);
    }

    #endregion

    #region Wallet Signature Test Data

    /// <summary>
    /// Creates test data for wallet signature validation.
    /// NOTE: Wallet signatures use FakeTimeProvider for deterministic testing (unlike JWT timestamps).
    /// </summary>
    public static class WalletSignatureTestData
    {
        /// <summary>
        /// Valid signed message with current timestamp (uses FakeTimeProvider).
        /// </summary>
        public static (string message, string signature, DateTime issuedAt) CreateValidSignature()
        {
            var message = TestDataFixtures.SignatureTestVectors.ValidMessageContent;
            var signature = TestDataFixtures.SignatureTestVectors.Sig_W1_Msg_V1;
            // Use FakeTimeProvider for wallet signatures (application logic, not JWT validation)
            var issuedAt = E2ETestBase.TestTime.AddMinutes(-5);

            return (message, signature, issuedAt);
        }

        /// <summary>
        /// Expired signed message (TTL exceeded, uses FakeTimeProvider).
        /// </summary>
        public static (string message, string signature, DateTime issuedAt) CreateExpiredSignature()
        {
            var message = TestDataFixtures.SignatureTestVectors.ValidMessageContent;
            var signature = TestDataFixtures.SignatureTestVectors.Sig_W1_Expired;
            // Use FakeTimeProvider for wallet signatures (application logic, not JWT validation)
            var issuedAt = E2ETestBase.TestTime.AddHours(-2); // Expired

            return (message, signature, issuedAt);
        }

        /// <summary>
        /// Reused signature for replay detection testing (uses FakeTimeProvider).
        /// </summary>
        public static (string message, string signature, DateTime issuedAt) CreateReusedSignature()
        {
            var message = TestDataFixtures.SignatureTestVectors.ValidMessageContent;
            var signature = TestDataFixtures.SignatureTestVectors.Sig_W1_Msg_V1; // Same as valid
            // Use FakeTimeProvider for wallet signatures (application logic, not JWT validation)
            var issuedAt = E2ETestBase.TestTime.AddMinutes(-3);

            return (message, signature, issuedAt);
        }
    }

    #endregion

    #region JWKS Generation

    /// <summary>
    /// Creates a test JWKS with the primary key (kid1).
    /// </summary>
    public static string CreateTestJwks()
    {
        var jwks = new
        {
            keys = new[]
            {
                CreateJwkFromRsaKey(TestRsa1.Value, TestKid1)
            }
        };

        return JsonSerializer.Serialize(jwks, JsonOptions);
    }

    /// <summary>
    /// Creates a rotated JWKS with both old and new keys.
    /// </summary>
    public static string CreateRotatedTestJwks()
    {
        var jwks = new
        {
            keys = new[]
            {
                CreateJwkFromRsaKey(TestRsa1.Value, TestKid1), // Keep old key
                CreateJwkFromRsaKey(TestRsa2.Value, TestKid2)  // Add new key
            }
        };

        return JsonSerializer.Serialize(jwks, JsonOptions);
    }

    /// <summary>
    /// Creates a JWKS with only the new key (old key removed).
    /// </summary>
    public static string CreateNewOnlyTestJwks()
    {
        var jwks = new
        {
            keys = new[]
            {
                CreateJwkFromRsaKey(TestRsa2.Value, TestKid2) // Only new key
            }
        };

        return JsonSerializer.Serialize(jwks, JsonOptions);
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Creates a JWT token with the specified claims and key ID.
    /// </summary>
    private static string CreateJwtToken(List<Claim> claims, string kid)
    {
        // Use the cached RSA key for consistent signing
        var rsa = kid == TestKid1 ? TestRsa1.Value : TestRsa2.Value;
        var rsaKey = new RsaSecurityKey(rsa) { KeyId = kid };
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);

        // Extract exp and iat claims to set on SecurityTokenDescriptor
        // This is critical - without this, JwtSecurityTokenHandler will generate its own exp/iat
        var expClaim = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
        var iatClaim = claims.FirstOrDefault(c => c.Type == "iat")?.Value;

        DateTime? expires = null;
        DateTime? issuedAt = null;

        if (!string.IsNullOrEmpty(expClaim) && long.TryParse(expClaim, out var expUnix))
        {
            expires = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
        }

        if (!string.IsNullOrEmpty(iatClaim) && long.TryParse(iatClaim, out var iatUnix))
        {
            issuedAt = DateTimeOffset.FromUnixTimeSeconds(iatUnix).UtcDateTime;
        }

        // Fix invalid timestamps for expired tokens where iat > exp
        // This can happen when CreateExpiredJwt is called with an expiration in the past
        // but iat is still set to TestTime.AddMinutes(-5)
        if (expires.HasValue && issuedAt.HasValue && issuedAt.Value >= expires.Value)
        {
            // Adjust iat to be before exp (1 hour before exp)
            issuedAt = expires.Value.AddHours(-1);

            // Update the iat claim in the claims list
            var iatClaimObj = claims.FirstOrDefault(c => c.Type == "iat");
            if (iatClaimObj != null)
            {
                claims.Remove(iatClaimObj);
                claims.Add(new Claim("iat",
                    new DateTimeOffset(issuedAt.Value).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64));
            }
        }

        // Set NotBefore to iat (standard practice)
        DateTime? notBefore = issuedAt;

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = credentials,
            TokenType = "JWT",
            Expires = expires,    // Critical: Honor the exp claim from input
            IssuedAt = issuedAt,  // Critical: Honor the iat claim from input
            NotBefore = notBefore // Critical: Set NotBefore to prevent auto-generation (must be < Expires)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Creates a JWK object from an RSA key and key ID.
    /// </summary>
    private static object CreateJwkFromRsaKey(RSA rsa, string kid)
    {
        // Export RSA parameters for JWK format
        var parameters = rsa.ExportParameters(false); // false = public key only

        return new
        {
            kty = "RSA",
            kid,
            use = "sig",
            alg = "RS256",
            n = Convert.ToBase64String(parameters.Modulus!).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            e = Convert.ToBase64String(parameters.Exponent!).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        };
    }

    #endregion

    #region Token Validation Helpers

    /// <summary>
    /// Gets the RSA signing keys for test token validation.
    /// </summary>
    public static IEnumerable<SecurityKey> GetTestSigningKeys()
    {
        yield return new RsaSecurityKey(TestRsa1.Value) { KeyId = TestKid1 };
        yield return new RsaSecurityKey(TestRsa2.Value) { KeyId = TestKid2 };
    }

    /// <summary>
    /// Validates that a JWT token has the expected structure without cryptographic verification.
    /// </summary>
    public static (bool isValid, Dictionary<string, object> claims) ValidateTokenStructure(string jwt)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);

            var claims = token.Claims.ToDictionary(c => c.Type, c => (object)c.Value);

            // Check required claims
            var hasRequiredClaims = claims.ContainsKey("sub") &&
                                   claims.ContainsKey("iss") &&
                                   claims.ContainsKey("aud") &&
                                   claims.ContainsKey("exp");

            return (hasRequiredClaims, claims);
        }
        catch
        {
            return (false, new Dictionary<string, object>());
        }
    }

    /// <summary>
    /// Extracts the key ID from a JWT header.
    /// </summary>
    public static string? ExtractKeyId(string jwt)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);
            return token.Header.Kid;
        }
        catch
        {
            return null;
        }
    }

    #endregion
}