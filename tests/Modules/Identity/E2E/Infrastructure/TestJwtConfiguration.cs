using Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Axon.Modules.Identity.E2E.Infrastructure;

/// <summary>
/// Centralized JWT configuration for E2E tests.
/// Provides reusable configuration methods for test JWT authentication setup.
/// </summary>
/// <remarks>
/// DESIGN DECISIONS:
/// 1. WireMock for JWKS - External Dynamic.xyz API simulation (we don't control it)
/// 2. FakeTimeProvider - Deterministic testing for application logic
/// 3. Real JWT Middleware - Full ASP.NET Core authentication pipeline
/// 4. HYBRID TIME: JWT tokens use real system time (middleware validation), app logic uses FakeTimeProvider
/// </remarks>
public static class TestJwtConfiguration
{
    /// <summary>
    /// Configures DynamicJwt authentication scheme for E2E tests.
    /// This is the PRIMARY validation layer - validates Dynamic.xyz JWTs from frontend.
    /// </summary>
    /// <remarks>
    /// WHY WE CONFIGURE THIS:
    /// - Enable full JWT validation (issuer, audience, lifetime, signature)
    /// - Provide test RSA keys for signature validation
    /// - Disable token replay cache (safe in tests, enables token reuse)
    /// - Configure test-friendly clock skew
    ///
    /// PRODUCTION DIFFERENCE:
    /// - Production: Fetches keys from real Dynamic.xyz JWKS endpoint
    /// - Tests: Uses test RSA keys from JwtTestTokenFactory (same keys in WireMock)
    /// </remarks>
    public static void ConfigureDynamicJwtScheme(
        JwtBearerOptions options,
        IEnumerable<SecurityKey> testSigningKeys)
    {
        // Enable all JWT validations for proper security testing
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidIssuer = TestDataFixtures.DynamicIssuer;

        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidAudience = "axon-api";

        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.ValidateLifetime = true;

        // CRITICAL: Provide test RSA keys for signature validation
        // This ensures JWT middleware can validate tokens signed by JwtTestTokenFactory
        // WITHOUT needing to fetch JWKS from WireMock on every request
        options.TokenValidationParameters.IssuerSigningKeys = testSigningKeys;

        // HYBRID TIME APPROACH:
        // - JWT tokens use real system time (DateTime.UtcNow) for proper middleware validation
        // - Application logic uses FakeTimeProvider for deterministic testing
        // - This allows real JWT validation while keeping deterministic testing for business logic
        // - Default ClockSkew (5 minutes) allows for reasonable clock drift tolerance

        // Disable JWKS refresh since we're providing keys directly
        options.RefreshOnIssuerKeyNotFound = false;
        options.RequireHttpsMetadata = false; // Allow HTTP for testing
    }

    /// <summary>
    /// Configures DynamicJwt authentication scheme WITHOUT direct key injection.
    /// Lets JWT middleware fetch JWKS from WireMock endpoint (truly E2E).
    /// </summary>
    /// <remarks>
    /// PHASE 3 REFACTORING:
    /// - Removed: Direct key injection via IssuerSigningKeys
    /// - Now: JWT middleware fetches from JwksUri (WireMock endpoint)
    /// - Benefit: Both JWT middleware AND DynamicAuthService use same key source
    /// - Result: Truly E2E testing of JWKS fetch/cache logic
    /// </remarks>
    public static void ConfigureDynamicJwtSchemeWithoutKeyInjection(JwtBearerOptions options, string jwksUri)
    {
        // Enable all JWT validations for proper security testing
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidIssuer = TestDataFixtures.DynamicIssuer;

        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidAudience = "axon-api";

        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.ValidateLifetime = true;

        // CRITICAL: Configure JWT middleware to fetch JWKS from WireMock
        // This makes the middleware fetch keys just like DynamicAuthService does
        options.MetadataAddress = jwksUri;
        options.Configuration = null; // Clear any cached configuration

        // HYBRID TIME APPROACH (same as before)

        // Enable JWKS refresh from endpoint (opposite of old approach)
        options.RefreshOnIssuerKeyNotFound = true; // Will refetch if key not found
        options.RequireHttpsMetadata = false; // Allow HTTP for testing
    }

    /// <summary>
    /// Configures AxonJwt authentication scheme for E2E tests.
    /// This is the SECONDARY validation layer - validates our internal Axon JWTs.
    /// </summary>
    /// <remarks>
    /// WHY WE CONFIGURE THIS:
    /// - Re-enable lifetime validation (tokens use real system time)
    ///
    /// PRODUCTION DIFFERENCE:
    /// - Minimal - this scheme is internal and uses signing key from configuration
    /// </remarks>
    public static void ConfigureAxonJwtScheme(JwtBearerOptions options)
    {
        // HYBRID TIME APPROACH: Re-enable lifetime validation (tokens use real system time)
        options.TokenValidationParameters.ValidateLifetime = true;
    }

    /// <summary>
    /// Configures DynamicValidationOptions for E2E tests.
    /// This is used by DynamicAuthService for SECONDARY JWT validation.
    /// </summary>
    /// <remarks>
    /// WHY WE CONFIGURE THIS:
    /// - Ensure test environment mapping is present
    /// - Disable background JWKS refresh (not needed in tests)
    /// - Configure test-friendly validation settings
    ///
    /// DUAL VALIDATION LAYERS:
    /// 1. JWT Middleware (ConfigureDynamicJwtScheme) - First validation
    /// 2. DynamicAuthService (ConfigureDynamicValidationOptions) - Second validation + claim extraction
    ///
    /// FUTURE IMPROVEMENT:
    /// Consider removing IJwksService mock to let DynamicAuthService fetch from WireMock
    /// This would make tests truly E2E by testing real JWKS fetch/cache logic
    /// </remarks>
    public static void ConfigureDynamicValidationOptions(
        Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration.DynamicValidationOptions options)
    {
        // Ensure test environment mapping is present
        options.EnvironmentMapping["dyn_test_env_12345"] = "test";

        // Disable background JWKS refresh during tests (not needed, reduces noise)
        options.EnableBackgroundRefresh = false;

        // Use test-friendly validation settings
        options.ValidateAudience = true;
        options.DefaultAllowedAudiences = new List<string> { "axon-api", "axon-web" };

        // HYBRID TIME APPROACH: Re-enable lifetime validation (tokens use real system time)
        options.ValidateLifetime = true;
        options.ClockSkewSeconds = 300; // 5 minutes clock skew tolerance (default)
    }

    /// <summary>
    /// Creates test configuration dictionary for WebApplicationFactory.
    /// </summary>
    /// <remarks>
    /// WHY WE OVERRIDE CONFIG:
    /// - Point to Testcontainers PostgreSQL (not local dev DB)
    /// - Point to WireMock JWKS endpoint (not real Dynamic.xyz)
    /// - Use test signing key for Axon JWTs
    /// - Configure environment mappings for test data
    /// </remarks>
    public static Dictionary<string, string?> CreateTestConfiguration(
        string connectionString,
        int jwksServerPort)
    {
        return new Dictionary<string, string?>
        {
            // PostgreSQL connection string override
            ["ConnectionStrings:DefaultConnection"] = connectionString,

            // Dynamic JWT configuration - CRITICAL: Must match DynamicXyzOptions.JwksUri computation
            // DynamicXyzOptions.JwksUri computes: {BaseUrl}/sdk/{EnvironmentId}/.well-known/jwks.json
            // We configure: BaseUrl=http://localhost:PORT, EnvironmentId=test-env-id
            // Result JWKS path: /sdk/test-env-id/.well-known/jwks.json (matches WireMock setup)
            ["DynamicXyz:BaseUrl"] = $"http://localhost:{jwksServerPort}",
            ["DynamicXyz:EnvironmentId"] = "test-env-id",
            ["DynamicXyz:ApiToken"] = "test-api-token",
            ["Dynamic:Authority"] = "", // Clear Authority to force JwksUri usage
            ["Dynamic:Issuer"] = TestDataFixtures.DynamicIssuer,
            ["Dynamic:Audience"] = "axon-api",
            ["DynamicValidation:EnvironmentMapping:dyn_test_env_12345"] = "test",

            // Axon JWT configuration (internal tokens)
            ["Axon:Issuer"] = "https://api.axon.test",
            ["Axon:Audience"] = "axon-api",
            ["Axon:SigningKey"] = "dGVzdC1zaWduaW5nLWtleS1mb3ItZTJlLXRlc3RzLW1pbmltdW0tMjU2LWJpdHMtcmVxdWlyZWQtaGVyZS1wYWRkaW5n"
        };
    }

    /// <summary>
    /// Explains the mocking strategy used in E2E tests.
    /// </summary>
    /// <remarks>
    /// WHAT'S MOCKED AND WHY:
    ///
    /// 1. ✅ JWKS Endpoint (WireMock) - CORRECT TO MOCK
    ///    - External Dynamic.xyz API we don't control
    ///    - Enables testing JWKS rotation, failures, network issues
    ///    - Standard E2E practice for external dependencies
    ///
    /// 2. ⚠️ IJwksService (TestJwksService) - QUESTIONABLE
    ///    - Currently mocked to return test RSA keys
    ///    - Bypasses real JWKS fetch/cache logic in DynamicAuthService
    ///    - Creates dual validation paths (middleware vs service)
    ///    - FUTURE: Remove mock, let real service fetch from WireMock
    ///
    /// 3. ✅ TimeProvider (FakeTimeProvider) - CORRECT TO INJECT
    ///    - Enables deterministic time-based testing
    ///    - Standard DI practice for testability
    ///    - Hybrid approach: JWTs use real time, app logic uses fake time
    ///
    /// 4. ✅ DbContexts - CORRECT TO OVERRIDE
    ///    - Points to Testcontainers PostgreSQL
    ///    - Standard E2E practice for infrastructure
    ///
    /// TRULY E2E vs PARTIALLY E2E:
    /// - Current state: Partially E2E (IJwksService mocked)
    /// - Goal state: Truly E2E (all services use real implementations, only external APIs mocked)
    /// </remarks>
    public const string MockingStrategy = """
        E2E Testing Mocking Strategy:

        MOCKED (External Dependencies):
        - WireMock JWKS Endpoint → Simulates Dynamic.xyz API

        INJECTED (Test Infrastructure):
        - FakeTimeProvider → Deterministic time testing
        - Testcontainers PostgreSQL → Isolated database per fixture

        CURRENTLY MOCKED (Should be Real):
        - IJwksService → Should fetch from WireMock, not return hardcoded keys

        REAL (Production Code Paths):
        - HTTP Server (Kestrel)
        - ASP.NET Core Middleware
        - JWT Middleware Validation
        - MediatR Pipeline
        - EF Core
        - All Application Services
        """;
}
