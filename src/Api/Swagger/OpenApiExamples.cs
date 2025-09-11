using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Axon.Api.Swagger;

/// <summary>
/// Comprehensive OpenAPI examples for all endpoints and error scenarios.
/// Provides realistic sample data for integration guidance.
/// </summary>
public static class OpenApiExamples
{
    #region Auth Exchange Examples

    /// <summary>
    /// Example request for /auth/exchange endpoint.
    /// </summary>
    public static readonly OpenApiObject ExchangeRequestExample = new()
    {
        // Empty body - JWT comes from Authorization header
    };

    /// <summary>
    /// Example success response for /auth/exchange endpoint.
    /// </summary>
    public static readonly OpenApiObject ExchangeSuccessResponseExample = new()
    {
        ["outcome"] = new OpenApiString("WALLET_LINKED"),
        ["principal"] = new OpenApiObject
        {
            ["id"] = new OpenApiString("01JKV2B9M8R5QF8XWHZ4F9D7TC"),
            ["email"] = new OpenApiString("user@example.com"),
            ["createdAt"] = new OpenApiString("2025-01-11T12:00:00Z"),
            ["updatedAt"] = new OpenApiString("2025-01-11T12:00:00Z")
        },
        ["walletDetails"] = new OpenApiObject
        {
            ["id"] = new OpenApiString("01JKV2B9M8R5QF8XWHZ4F9D7TD"),
            ["chainId"] = new OpenApiString("solana"),
            ["address"] = new OpenApiString("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM"),
            ["ownership"] = new OpenApiString("VERIFIED"),
            ["linkedAt"] = new OpenApiString("2025-01-11T12:00:00Z")
        }
    };

    #endregion

    #region Auth Me Examples

    /// <summary>
    /// Example success response for /auth/me endpoint.
    /// </summary>
    public static readonly OpenApiObject MeSuccessResponseExample = new()
    {
        ["principal"] = new OpenApiObject
        {
            ["id"] = new OpenApiString("01JKV2B9M8R5QF8XWHZ4F9D7TC"),
            ["email"] = new OpenApiString("user@example.com"),
            ["createdAt"] = new OpenApiString("2025-01-11T12:00:00Z"),
            ["updatedAt"] = new OpenApiString("2025-01-11T12:00:00Z")
        },
        ["wallets"] = new OpenApiArray
        {
            new OpenApiObject
            {
                ["id"] = new OpenApiString("01JKV2B9M8R5QF8XWHZ4F9D7TD"),
                ["chainId"] = new OpenApiString("solana"),
                ["address"] = new OpenApiString("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM"),
                ["ownership"] = new OpenApiString("VERIFIED"),
                ["linkedAt"] = new OpenApiString("2025-01-11T12:00:00Z")
            }
        },
        ["lastLogin"] = new OpenApiString("2025-01-11T12:00:00Z"),
        ["etag"] = new OpenApiString("W/\"123456789\"")
    };

    #endregion

    #region Error Examples

    /// <summary>
    /// 400 Bad Request - Validation error example.
    /// </summary>
    public static readonly OpenApiObject ValidationErrorExample = new()
    {
        ["code"] = new OpenApiString("VALIDATION_ERROR"),
        ["message"] = new OpenApiString("Authorization header with Bearer token is required"),
        ["details"] = new OpenApiObject
        {
            ["field"] = new OpenApiString("authorization"),
            ["expected"] = new OpenApiString("Bearer <jwt-token>")
        }
    };

    /// <summary>
    /// 401 Unauthorized - Authentication error example.
    /// </summary>
    public static readonly OpenApiObject UnauthorizedErrorExample = new()
    {
        ["code"] = new OpenApiString("UNAUTHORIZED"),
        ["message"] = new OpenApiString("Invalid or expired JWT token")
    };

    /// <summary>
    /// 409 Conflict - Wallet ownership conflict example (privacy-safe).
    /// </summary>
    public static readonly OpenApiObject ConflictErrorExample = new()
    {
        ["code"] = new OpenApiString("WALLET_OWNERSHIP_CONFLICT"),
        ["message"] = new OpenApiString("wallet already owned"),
        ["details"] = new OpenApiObject
        {
            ["chainId"] = new OpenApiString("solana"),
            ["address"] = new OpenApiString("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM")
        }
    };

    /// <summary>
    /// 422 Unprocessable Entity - Business rule violation example.
    /// </summary>
    public static readonly OpenApiObject BusinessRuleErrorExample = new()
    {
        ["code"] = new OpenApiString("BUSINESS_RULE_VIOLATION"),
        ["message"] = new OpenApiString("Wallet verification failed - invalid signature"),
        ["details"] = new OpenApiObject
        {
            ["rule"] = new OpenApiString("WalletVerificationRule"),
            ["violation"] = new OpenApiString("Signature verification failed for provided wallet address")
        }
    };

    /// <summary>
    /// 429 Too Many Requests - Rate limit exceeded example.
    /// </summary>
    public static readonly OpenApiObject RateLimitErrorExample = new()
    {
        ["code"] = new OpenApiString("RATE_LIMIT_EXCEEDED"),
        ["message"] = new OpenApiString("Rate limit exceeded - 10 requests per minute per IP"),
        ["details"] = new OpenApiObject
        {
            ["retryAfter"] = new OpenApiInteger(45),
            ["limit"] = new OpenApiInteger(10),
            ["window"] = new OpenApiString("1 minute")
        }
    };

    /// <summary>
    /// 500 Internal Server Error - Internal error example.
    /// </summary>
    public static readonly OpenApiObject InternalErrorExample = new()
    {
        ["code"] = new OpenApiString("INTERNAL_ERROR"),
        ["message"] = new OpenApiString("An internal error occurred")
    };

    #endregion

    #region cURL Examples

    /// <summary>
    /// Gets cURL example for /auth/exchange endpoint.
    /// </summary>
    public static string GetExchangeCurlExample(string baseUrl = "https://api.axon.dev") => $$"""
        curl -X POST "{{baseUrl}}/auth/exchange" \
             -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
             -H "Content-Type: application/json" \
             -d '{}'
        """;

    /// <summary>
    /// Gets cURL example for /auth/me endpoint.
    /// </summary>
    public static string GetMeCurlExample(string baseUrl = "https://api.axon.dev") => $$"""
        curl -X GET "{{baseUrl}}/auth/me" \
             -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
             -H "Accept: application/json"
        """;

    #endregion

    #region JWT Token Examples

    /// <summary>
    /// Example JWT payload structure for documentation.
    /// </summary>
    public static readonly OpenApiObject JwtPayloadExample = new()
    {
        ["iss"] = new OpenApiString("https://app.dynamic.xyz"),
        ["sub"] = new OpenApiString("user_123456789"),
        ["aud"] = new OpenApiString("axon-identity-service"),
        ["exp"] = new OpenApiInteger(1736604000), // 2025-01-11T12:00:00Z
        ["iat"] = new OpenApiInteger(1736600400), // 2025-01-11T11:00:00Z
        ["email"] = new OpenApiString("user@example.com"),
        ["verified_credentials"] = new OpenApiArray
        {
            new OpenApiObject
            {
                ["address"] = new OpenApiString("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM"),
                ["chain"] = new OpenApiString("SOL"),
                ["public_key"] = new OpenApiString("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM"),
                ["wallet_name"] = new OpenApiString("phantom"),
                ["format"] = new OpenApiString("blockchain")
            }
        }
    };

    /// <summary>
    /// Sample JWT Bearer token for testing (shortened for readability).
    /// </summary>
    public static readonly string SampleJwtToken = 
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJodHRwczovL2FwcC5keW5hbWljLnh5eiIsInN1YiI6InVzZXJfMTIzNDU2Nzg5IiwiYXVkIjoiYXhvbi1pZGVudGl0eS1zZXJ2aWNlIn0.example_signature";

    #endregion

    #region ULID Examples

    /// <summary>
    /// Sample ULID values for consistent documentation examples.
    /// </summary>
    public static class SampleULIDs
    {
        public const string PrincipalId = "01JKV2B9M8R5QF8XWHZ4F9D7TC";
        public const string WalletId = "01JKV2B9M8R5QF8XWHZ4F9D7TD";
        public const string CredentialId = "01JKV2B9M8R5QF8XWHZ4F9D7TE";
    }

    /// <summary>
    /// Sample Solana addresses for consistent documentation examples.
    /// </summary>
    public static class SampleAddresses
    {
        public const string Solana = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";
        public const string SolanaSecondary = "4fYNw3dojWmQ3dXtSGZ9epjRSy9XDvKkXE3WdJohqGxu";
    }

    #endregion
}