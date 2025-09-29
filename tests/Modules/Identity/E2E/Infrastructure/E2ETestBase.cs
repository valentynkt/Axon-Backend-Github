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
    private bool _disposed;

    /// <summary>
    /// Test timestamp for deterministic time-based testing.
    /// </summary>
    public static readonly DateTime TestTime = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        // Start PostgreSQL container
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("axon_identity_e2e_test")
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
                    // Clear existing sources to ensure clean slate
                    config.Sources.Clear();

                    // Re-add base configuration
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false);
                    config.AddEnvironmentVariables();

                    // Add test-specific overrides LAST (highest priority)
                    var dynamicConfig = new Dictionary<string, string?>
                    {
                        ["Dynamic:Authority"] = "", // Clear Authority to force JwksUri usage
                        ["Dynamic:JwksUri"] = $"http://localhost:{JwksServer.Port}/.well-known/jwks.json",
                        ["Dynamic:Issuer"] = TestDataFixtures.DynamicIssuer,
                        ["Dynamic:Audience"] = "axon-api",
                        // Add test environment mapping for Dynamic validation
                        ["DynamicValidation:EnvironmentMapping:dyn_test_env_12345"] = "test"
                    };

                    config.AddInMemoryCollection(dynamicConfig);
                });

                builder.ConfigureServices(services =>
                {
                    // Replace connection string for test database
                    services.Configure<ConnectionStrings>(options =>
                    {
                        options.DefaultConnection = _connectionString;
                    });

                    // Replace time provider with deterministic one
                    services.AddSingleton<TimeProvider>(TimeProvider);

                    // CRITICAL FIX: Post-configure DynamicJwt options to use test issuer and signing keys
                    // This runs AFTER IdentityApiModule registers JWT authentication
                    services.PostConfigure<JwtBearerOptions>("DynamicJwt", options =>
                    {
                        options.TokenValidationParameters.ValidIssuer = TestDataFixtures.DynamicIssuer;

                        // Directly provide test RSA keys instead of relying on JWKS discovery
                        // This ensures signature validation works in tests
                        var testKeys = JwtTestTokenFactory.GetTestSigningKeys();
                        options.TokenValidationParameters.IssuerSigningKeys = testKeys;

                        // Disable JWKS refresh since we're providing keys directly
                        options.RefreshOnIssuerKeyNotFound = false;
                        options.RequireHttpsMetadata = false; // Allow HTTP for testing
                    });

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
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityWriteDbContext>();

        await dbContext.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Cleans up database state between tests.
    /// </summary>
    private async Task CleanupDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityWriteDbContext>();

        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                TRUNCATE TABLE identity.principal_chain_default CASCADE;
                TRUNCATE TABLE identity.wallet_ownership CASCADE;
                TRUNCATE TABLE identity.credential CASCADE;
                TRUNCATE TABLE identity.wallet CASCADE;
                TRUNCATE TABLE identity.axon_principal CASCADE;
            ");
        }
        catch
        {
            // If truncate fails, recreate database
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
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

#pragma warning disable NUnit1028 // Only test methods should be public
    public async ValueTask DisposeAsync()
#pragma warning restore NUnit1028
    {
        await TearDownAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration for connection strings in tests.
/// </summary>
internal sealed class ConnectionStrings
{
    public string DefaultConnection { get; set; } = string.Empty;
}