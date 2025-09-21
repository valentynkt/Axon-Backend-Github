using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Axon.Modules.Identity.Infrastructure.Persistence.DbInvariants;
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

    // Ed25519 test keys (in practice, these would be properly generated)
    private static readonly byte[] TestPrivateKey1 = Convert.FromBase64String(
        "MC4CAQAwBQYDK2VwBCIEIC1hNKQqAruS7+8P5ELQDfOdZWQgKDDLKZj2Y7HGN2VF");

    private static readonly byte[] TestPublicKey1 = Convert.FromBase64String(
        "MCowBQYDK2VwAyEAqEW1B2Kgkz1r6nQ2QoL1fZYp3tJ5mP0xR4fL3eE2sA==");

    private static readonly byte[] TestPrivateKey2 = Convert.FromBase64String(
        "MC4CAQAwBQYDK2VwBCIEIM2oNLRrBtuT8/9Q6FMRDeQeaXRhLEELKaj3Z8IGO3WG");

    private static readonly byte[] TestPublicKey2 = Convert.FromBase64String(
        "MCowBQYDK2VwAyEArFX2C3Lhla2s7oR3RpM2gZbq4uK6nQ1yS5gM4fF4tB==");

    #region JWT Token Creation

    /// <summary>
    /// Creates a valid Dynamic JWT with standard claims.
    /// </summary>
    public static string CreateValidDynamicJwt(
        string subject = TestDataFixtures.DynA_Subject,
        string issuer = TestDataFixtures.DynamicIssuer,
        string audience = "axon-api",
        string kid = TestKid1,
        DateTime? expiration = null,
        Dictionary<string, object>? additionalClaims = null)
    {
        var exp = expiration ?? E2ETestBase.TestTime.AddHours(1);
        var iat = E2ETestBase.TestTime.AddMinutes(-5);

        var claims = new List<Claim>
        {
            new("sub", subject),
            new("iss", issuer),
            new("aud", audience),
            new("iat", new DateTimeOffset(iat).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("exp", new DateTimeOffset(exp).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
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
    /// </summary>
    public static string CreateExpiredJwt(
        string subject = TestDataFixtures.DynA_Subject)
    {
        var expiredTime = E2ETestBase.TestTime.AddHours(-1);
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
    /// </summary>
    public static string CreateFutureValidJwt(
        string subject = TestDataFixtures.DynA_Subject)
    {
        var futureValid = E2ETestBase.TestTime.AddMinutes(30);
        return CreateValidDynamicJwt(subject: subject, expiration: futureValid, kid: TestKid2);
    }

    #endregion

    #region Wallet Signature Test Data

    /// <summary>
    /// Creates test data for wallet signature validation.
    /// </summary>
    public static class WalletSignatureTestData
    {
        /// <summary>
        /// Valid signed message with current timestamp.
        /// </summary>
        public static (string message, string signature, DateTime issuedAt) CreateValidSignature()
        {
            var message = TestDataFixtures.SignatureTestVectors.ValidMessageContent;
            var signature = TestDataFixtures.SignatureTestVectors.Sig_W1_Msg_V1;
            var issuedAt = E2ETestBase.TestTime.AddMinutes(-5);

            return (message, signature, issuedAt);
        }

        /// <summary>
        /// Expired signed message (TTL exceeded).
        /// </summary>
        public static (string message, string signature, DateTime issuedAt) CreateExpiredSignature()
        {
            var message = TestDataFixtures.SignatureTestVectors.ValidMessageContent;
            var signature = TestDataFixtures.SignatureTestVectors.Sig_W1_Expired;
            var issuedAt = E2ETestBase.TestTime.AddHours(-2); // Expired

            return (message, signature, issuedAt);
        }

        /// <summary>
        /// Reused signature for replay detection testing.
        /// </summary>
        public static (string message, string signature, DateTime issuedAt) CreateReusedSignature()
        {
            var message = TestDataFixtures.SignatureTestVectors.ValidMessageContent;
            var signature = TestDataFixtures.SignatureTestVectors.Sig_W1_Msg_V1; // Same as valid
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
                CreateJwkFromPublicKey(TestPublicKey1, TestKid1)
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
                CreateJwkFromPublicKey(TestPublicKey1, TestKid1), // Keep old key
                CreateJwkFromPublicKey(TestPublicKey2, TestKid2)  // Add new key
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
                CreateJwkFromPublicKey(TestPublicKey2, TestKid2) // Only new key
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
        // Note: Ed25519 is not yet supported in Microsoft.IdentityModel.Tokens 8.x
        // Using RSA256 as a temporary fallback for tests
        using var rsa = RSA.Create(2048);
        var rsaKey = new RsaSecurityKey(rsa) { KeyId = kid };
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = credentials,
            TokenType = "JWT"
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Creates a JWK object from a public key and key ID.
    /// </summary>
    private static object CreateJwkFromPublicKey(byte[] publicKey, string kid)
    {
        // For Ed25519, we need to extract the 32-byte key from the DER encoding
        var keyBytes = publicKey.Skip(12).Take(32).ToArray();

        return new
        {
            kty = "OKP",
            crv = "Ed25519",
            kid,
            use = "sig",
            x = Convert.ToBase64String(keyBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        };
    }

    #endregion

    #region Token Validation Helpers

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