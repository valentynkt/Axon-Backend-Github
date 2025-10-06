using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;
using Axon.Modules.Identity.Infrastructure.Persistence;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using WireMock.Logging;

namespace Axon.Modules.Identity.E2E.Infrastructure;

/// <summary>
/// Base class for E2E tests using real HTTP server, PostgreSQL, and WireMock for external services.
/// Implements modern ASP.NET Core testing practices with Testcontainers and deterministic time.
/// </summary>
public abstract class E2ETestBase : IAsyncDisposable
{
    protected WebApplicationFactory<Program> Factory { get; private set; } = null!;
    protected HttpClient HttpClient { get; private set; } = null!;
    protected WireMockServer JwksServer { get; private set; } = null!;
    protected FakeTimeProvider TimeProvider { get; private set; } = null!;

    private PostgreSqlContainer _postgreSqlContainer = null!;
    private string _connectionString = null!;
    private string _databaseName = null!;
    private bool _disposed;

    /// <summary>
    /// Test timestamp for deterministic time-based testing.
    /// </summary>
    public static readonly DateTime TestTime = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        // CRITICAL FIX: Generate unique database name per test fixture to ensure complete isolation
        // This prevents test fixtures from interfering with each other when running in parallel
        // Each fixture gets its own database: axon_e2e_authme_abc123, axon_e2e_tokenvalidation_def456, etc.
        var fixtureTypeName = GetType().Name.ToLowerInvariant().Replace("tests", "").Replace("e2e", "");
        var fullDatabaseName = $"axon_e2e_{fixtureTypeName}_{Guid.NewGuid():N}";
        _databaseName = fullDatabaseName.Length > 63 ? fullDatabaseName.Substring(0, 63) : fullDatabaseName;

        // Start PostgreSQL container
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase(_databaseName)
            .WithUsername("e2e_user")
            .WithPassword("e2e_password")
            .WithCleanUp(true)
            .Build();

        await _postgreSqlContainer.StartAsync();
        _connectionString = _postgreSqlContainer.GetConnectionString();

        // Start WireMock server for JWKS endpoint
        JwksServer = WireMockServer.Start(new WireMockServerSettings
        {
            Port = 0, // Use random available port
            StartAdminInterface = false
        });

        SetupDefaultJwksEndpoint();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        JwksServer?.Stop();
        JwksServer?.Dispose();

        if (_postgreSqlContainer != null)
        {
            await _postgreSqlContainer.DisposeAsync();
        }
    }

    [SetUp]
    public async Task SetUpAsync()
    {
        // Reset disposal flag for new test
        _disposed = false;

        // Initialize deterministic time provider
        TimeProvider = new FakeTimeProvider(TestTime);

        // Create WebApplicationFactory with test configuration
        // CA2000 suppressed: Factory is disposed in TearDownAsync
        #pragma warning disable CA2000 // Dispose objects before losing scope
        Factory = new WebApplicationFactory<Program>()
        #pragma warning restore CA2000 // Dispose objects before losing scope
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                // CRITICAL FIX: Configure Dynamic Auth settings to OVERRIDE appsettings
                // AddInMemoryCollection must be LAST to ensure it overrides file-based config
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Clear and rebuild configuration with test overrides
                    config.Sources.Clear();
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false);
                    config.AddEnvironmentVariables();

                    // Test-specific overrides (highest priority)
                    var testConfig = new Dictionary<string, string?>
                    {
                        // PostgreSQL connection string override
                        ["ConnectionStrings:DefaultConnection"] = _connectionString,

                        // Dynamic JWT configuration
                        ["Dynamic:Authority"] = "", // Clear Authority to force JwksUri usage
                        ["Dynamic:JwksUri"] = $"http://localhost:{JwksServer.Port}/.well-known/jwks.json",
                        ["Dynamic:Issuer"] = TestDataFixtures.DynamicIssuer,
                        ["Dynamic:Audience"] = "axon-api",
                        ["DynamicValidation:EnvironmentMapping:dyn_test_env_12345"] = "test",

                        // Axon JWT configuration
                        ["Axon:Issuer"] = "https://api.axon.test",
                        ["Axon:Audience"] = "axon-api",
                        ["Axon:SigningKey"] = "dGVzdC1zaWduaW5nLWtleS1mb3ItZTJlLXRlc3RzLW1pbmltdW0tMjU2LWJpdHMtcmVxdWlyZWQtaGVyZS1wYWRkaW5n"
                    };

                    config.AddInMemoryCollection(testConfig);
                });

                builder.ConfigureServices(services =>
                {
                    // CRITICAL: Remove existing DbContext registrations and re-register with test connection string
                    // This must be done BEFORE replacing any services
                    RemoveDbContextRegistrations(services);
                    RegisterDbContextsWithTestConnectionString(services, _connectionString);

                    // Replace time provider with deterministic one
                    services.AddSingleton<TimeProvider>(TimeProvider);

                    // CRITICAL FIX: Post-configure DynamicJwt options to use test issuer and signing keys
                    // This runs AFTER IdentityApiModule registers JWT authentication
                    services.PostConfigure<JwtBearerOptions>("DynamicJwt", options =>
                    {
                        // Enable all JWT validations for proper security testing
                        options.TokenValidationParameters.ValidateIssuer = true;
                        options.TokenValidationParameters.ValidIssuer = TestDataFixtures.DynamicIssuer;

                        options.TokenValidationParameters.ValidateAudience = true;
                        options.TokenValidationParameters.ValidAudience = "axon-api";

                        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                        options.TokenValidationParameters.ValidateLifetime = true; // Re-enabled for real validation

                        // Directly provide test RSA keys instead of relying on JWKS discovery
                        // This ensures signature validation works in tests
                        var testKeys = JwtTestTokenFactory.GetTestSigningKeys();
                        options.TokenValidationParameters.IssuerSigningKeys = testKeys;

                        // HYBRID TIME APPROACH:
                        // - JWT tokens use real system time (DateTime.UtcNow) for proper middleware validation
                        // - Application logic uses FakeTimeProvider for deterministic testing
                        // - This allows real JWT validation while keeping deterministic testing for business logic
                        // - Default ClockSkew (5 minutes) allows for reasonable clock drift tolerance

                        // CRITICAL FIX: Disable token replay cache for E2E tests
                        // TokenReplayCache prevents the same JWT from being used multiple times
                        // In E2E tests, we reuse the same token across multiple requests within a single test
                        // This is safe in tests since we control the token generation and don't need replay protection
                        options.TokenValidationParameters.TokenReplayCache = null;

                        // Disable JWKS refresh since we're providing keys directly
                        options.RefreshOnIssuerKeyNotFound = false;
                        options.RequireHttpsMetadata = false; // Allow HTTP for testing
                    });

                    // Post-configure AxonJwt authentication scheme (for internal tokens)
                    services.PostConfigure<JwtBearerOptions>("AxonJwt", options =>
                    {
                        // HYBRID TIME APPROACH: Re-enable lifetime validation (tokens use real system time)
                        options.TokenValidationParameters.ValidateLifetime = true;

                        // CRITICAL FIX: Disable token replay cache for E2E tests
                        // TokenReplayCache prevents the same JWT from being used multiple times
                        // In E2E tests, we reuse the same token across multiple requests within a single test
                        // This is safe in tests since we control the token generation and don't need replay protection
                        options.TokenValidationParameters.TokenReplayCache = null;
                    });

                    // CRITICAL FIX: Configure DynamicAuthService validation for E2E tests
                    // This is the SECOND validation layer that runs within DynamicAuthenticationProvider
                    services.PostConfigure<Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration.DynamicValidationOptions>(options =>
                    {
                        // Ensure test environment mapping is present
                        options.EnvironmentMapping["dyn_test_env_12345"] = "test";

                        // Disable background JWKS refresh during tests
                        options.EnableBackgroundRefresh = false;

                        // Use test-friendly validation settings
                        options.ValidateAudience = true;
                        options.DefaultAllowedAudiences = new List<string> { "axon-api", "axon-web" };

                        // HYBRID TIME APPROACH: Re-enable lifetime validation (tokens use real system time)
                        options.ValidateLifetime = true;
                        options.ClockSkewSeconds = 300; // 5 minutes clock skew tolerance (default)
                    });

                    // Replace IJwksService with test implementation that returns test RSA keys
                    // This prevents DynamicAuthService from fetching production keys on startup
                    services.AddSingleton<Axon.Modules.Identity.Application.Contracts.ExternalServices.IJwksService, TestJwksService>();

                    // Disable authentication for some E2E tests when needed
                    ConfigureTestServices(services);
                });

                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Warning); // Reduce noise in tests
                });
            });

        // CRITICAL FIX: Clear memory cache AFTER Factory is created
        // This prevents authentication state leakage between tests
        ClearMemoryCacheIfExists();

        HttpClient = Factory.CreateClient();

        // Ensure database is migrated and clean
        await EnsureDatabaseSetupAsync();

        // Allow derived classes to perform additional setup
        await SetUpDerivedAsync();
    }

    [TearDown]
    public async Task TearDownAsync()
    {
        if (_disposed) return;

        try
        {
            await TearDownDerivedAsync();
        }
        finally
        {
            // CRITICAL FIX: Clear memory cache before disposing to prevent token validation cache pollution
            // DynamicAuthService caches validated tokens which can leak between tests
            ClearMemoryCacheIfExists();

            // CRITICAL FIX: Clear DbContext change trackers before disposing
            // This ensures EF Core tracked entities don't leak between tests
            ClearDbContextChangeTrackers();

            await CleanupDatabaseAsync();

            HttpClient?.Dispose();

            if (Factory != null)
            {
                await Factory.DisposeAsync();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Override this method to configure additional test services.
    /// </summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
        // Default implementation does nothing
    }

    /// <summary>
    /// Override this method to perform additional setup in derived test classes.
    /// </summary>
    protected virtual Task SetUpDerivedAsync() => Task.CompletedTask;

    /// <summary>
    /// Override this method to perform additional cleanup in derived test classes.
    /// </summary>
    protected virtual Task TearDownDerivedAsync() => Task.CompletedTask;

    #region Database Management

    /// <summary>
    /// Ensures the test database is properly migrated and set up.
    /// </summary>
    private async Task EnsureDatabaseSetupAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var writeDbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var identityContext = scope.ServiceProvider.GetRequiredService<Axon.Modules.Identity.Infrastructure.Persistence.Context.IdentityContext>();

        // Delete entire database to ensure clean state
        await writeDbContext.Database.EnsureDeletedAsync();

        // CRITICAL FIX: IdentityContext (ASP.NET Identity) has NO migrations - use EnsureCreatedAsync()
        // IdentityDbContext (Axon tables) HAS migrations - use MigrateAsync()

        // Create ASP.NET Identity tables first (no migrations available)
        await identityContext.Database.EnsureCreatedAsync();

        // Apply Axon Identity migrations (Principal, Wallet, Credential, etc.)
        await writeDbContext.Database.MigrateAsync();
    }

    /// <summary>
    /// Cleans up database state between tests.
    /// </summary>
    private async Task CleanupDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var writeDbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var identityContext = scope.ServiceProvider.GetRequiredService<Axon.Modules.Identity.Infrastructure.Persistence.Context.IdentityContext>();

        try
        {
            await writeDbContext.Database.ExecuteSqlRawAsync(@"
                TRUNCATE TABLE identity.""PrincipalChainDefault"" CASCADE;
                TRUNCATE TABLE identity.""WalletOwnership"" CASCADE;
                TRUNCATE TABLE identity.""Credential"" CASCADE;
                TRUNCATE TABLE identity.""Wallet"" CASCADE;
                TRUNCATE TABLE identity.""Principal"" CASCADE;
                TRUNCATE TABLE identity.""AspNetUsers"" CASCADE;
                TRUNCATE TABLE identity.""AspNetRoles"" CASCADE;
                TRUNCATE TABLE identity.""AspNetUserRoles"" CASCADE;
            ");
        }
        catch
        {
            // If truncate fails, recreate database
            await writeDbContext.Database.EnsureDeletedAsync();
            await identityContext.Database.EnsureCreatedAsync();
            await writeDbContext.Database.EnsureCreatedAsync();
        }
    }

    #endregion

    #region WireMock JWKS Setup

    /// <summary>
    /// Sets up default JWKS endpoint with test keys.
    /// </summary>
    private void SetupDefaultJwksEndpoint()
    {
        var jwks = JwtTestTokenFactory.CreateTestJwks();

        JwksServer
            .Given(Request.Create().WithPath("/.well-known/jwks.json").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(jwks));
    }

    /// <summary>
    /// Simulates JWKS key rotation by updating the endpoint.
    /// </summary>
    protected void SimulateJwksKeyRotation()
    {
        var rotatedJwks = JwtTestTokenFactory.CreateRotatedTestJwks();

        JwksServer.Reset();
        JwksServer
            .Given(Request.Create().WithPath("/.well-known/jwks.json").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(rotatedJwks));
    }

    /// <summary>
    /// Simulates JWKS endpoint failure.
    /// </summary>
    protected void SimulateJwksEndpointFailure()
    {
        JwksServer.Reset();
        JwksServer
            .Given(Request.Create().WithPath("/.well-known/jwks.json").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBody("Service Temporarily Unavailable"));
    }

    #endregion

    #region HTTP Helper Methods

    /// <summary>
    /// Creates an authenticated HTTP request with Bearer token.
    /// </summary>
    protected void SetAuthorizationHeader(string jwt)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
    }

    /// <summary>
    /// Clears the authorization header.
    /// </summary>
    protected void ClearAuthorizationHeader()
    {
        HttpClient.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Sets the If-None-Match header for ETag testing.
    /// </summary>
    protected void SetIfNoneMatchHeader(string etag)
    {
        HttpClient.DefaultRequestHeaders.IfNoneMatch.Clear();
        HttpClient.DefaultRequestHeaders.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{etag}\""));
    }

    /// <summary>
    /// Clears the If-None-Match header.
    /// </summary>
    protected void ClearIfNoneMatchHeader()
    {
        HttpClient.DefaultRequestHeaders.IfNoneMatch.Clear();
    }

    /// <summary>
    /// Clears all request headers to prevent pollution between requests.
    /// </summary>
    protected void ClearAllHeaders()
    {
        HttpClient.DefaultRequestHeaders.Clear();
    }

    /// <summary>
    /// Creates a JSON content for POST requests.
    /// </summary>
    protected static StringContent CreateJsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    #endregion

    #region Time Management

    /// <summary>
    /// Advances the test time by the specified duration.
    /// </summary>
    protected void AdvanceTime(TimeSpan duration)
    {
        TimeProvider.Advance(duration);
    }

    /// <summary>
    /// Sets the test time to a specific moment.
    /// </summary>
    protected void SetTime(DateTime time)
    {
        TimeProvider.SetUtcNow(time);
    }

    #endregion

    #region DbContext Registration Helpers

    /// <summary>
    /// Removes existing DbContext registrations to prepare for test-specific ones.
    /// </summary>
    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        // Remove Identity DbContexts
        var identityWriteDbContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IdentityDbContext));
        if (identityWriteDbContextDescriptor != null)
        {
            services.Remove(identityWriteDbContextDescriptor);
        }

        var identityContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Axon.Modules.Identity.Infrastructure.Persistence.Context.IdentityContext));
        if (identityContextDescriptor != null)
        {
            services.Remove(identityContextDescriptor);
        }

        var identityReadDbContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Axon.Modules.Identity.Infrastructure.Persistence.DbContexts.IdentityDbContext));
        if (identityReadDbContextDescriptor != null)
        {
            services.Remove(identityReadDbContextDescriptor);
        }

        // Remove DbContextOptions registrations
        var dbContextOptionsList = services
            .Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>))
            .ToList();

        foreach (var descriptor in dbContextOptionsList)
        {
            services.Remove(descriptor);
        }
    }

    /// <summary>
    /// Registers all required DbContexts with the test connection string.
    /// </summary>
    private static void RegisterDbContextsWithTestConnectionString(IServiceCollection services, string connectionString)
    {
        // Register Identity DbContexts with test connection string
        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            });
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        services.AddDbContext<Axon.Modules.Identity.Infrastructure.Persistence.Context.IdentityContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            });
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        services.AddDbContext<Axon.Modules.Identity.Infrastructure.Persistence.DbContexts.IdentityDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
                npgsqlOptions.CommandTimeout(30);
            });
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });
    }

    #endregion

    #region Test Isolation Helpers

    /// <summary>
    /// Clears memory cache to prevent state leakage between tests.
    /// IMemoryCache caches authentication state (user IDs, principals) with 15-30 minute expiration.
    /// Without clearing, cached auth from previous tests causes conflicts.
    /// </summary>
    private void ClearMemoryCacheIfExists()
    {
        try
        {
            // Check if Factory exists and has services (only after Factory creation)
            if (Factory?.Services == null) return;

            var cache = Factory.Services.GetService<IMemoryCache>();
            if (cache is Microsoft.Extensions.Caching.Memory.MemoryCache memCache)
            {
                // Compact(1.0) removes ALL cache entries by setting eviction threshold to 100%
                memCache.Compact(1.0);
            }
        }
        catch
        {
            // Silently ignore - cache clearing is best-effort for test isolation
        }
    }

    /// <summary>
    /// Clears EF Core DbContext change trackers to prevent entity tracking conflicts.
    /// PrincipalResolutionService reloads tracked entities for modifications.
    /// Without clearing, tracked entities from previous tests cause conflicts.
    /// </summary>
    private void ClearDbContextChangeTrackers()
    {
        try
        {
            if (Factory?.Services == null) return;

            using var scope = Factory.Services.CreateScope();

            // Clear Identity Write DbContext
            var identityWriteDb = scope.ServiceProvider.GetService<IdentityDbContext>();
            identityWriteDb?.ChangeTracker.Clear();

            // Clear Identity Read DbContext (if needed)
            var identityReadDb = scope.ServiceProvider.GetService<Axon.Modules.Identity.Infrastructure.Persistence.DbContexts.IdentityDbContext>();
            identityReadDb?.ChangeTracker.Clear();

            // Clear Identity Context
            var identityContext = scope.ServiceProvider.GetService<Axon.Modules.Identity.Infrastructure.Persistence.Context.IdentityContext>();
            identityContext?.ChangeTracker.Clear();
        }
        catch
        {
            // Silently ignore - change tracker clearing is best-effort for test isolation
        }
    }

    #endregion

#pragma warning disable NUnit1028 // Only test methods should be public
    public async ValueTask DisposeAsync()
#pragma warning restore NUnit1028
    {
        await TearDownAsync();
        GC.SuppressFinalize(this);
    }
}