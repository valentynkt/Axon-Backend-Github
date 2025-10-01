using System.Net.Http.Headers;
using System.Text;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.E2E.Infrastructure;
using Axon.Modules.Identity.Infrastructure.Persistence;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace Axon.Modules.Chat.E2E.Infrastructure;

/// <summary>
/// Base class for Chat E2E tests using real HTTP server, PostgreSQL, and WireMock.
/// Sets up both Chat and Identity DbContexts since Chat module requires authentication.
/// </summary>
public abstract class ChatE2ETestBase : IAsyncDisposable
{
    protected WebApplicationFactory<Program> Factory { get; private set; } = null!;
    protected HttpClient HttpClient { get; private set; } = null!;
    protected WireMockServer JwksServer { get; private set; } = null!;
    protected FakeTimeProvider TimeProvider { get; private set; } = null!;
    protected MockAiProcessingService MockAiService { get; private set; } = null!;

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
        // Start PostgreSQL container (shared across all tests in fixture)
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("axon_chat_e2e_test")
            .WithUsername("e2e_user")
            .WithPassword("e2e_password")
            .WithCleanUp(true)
            .Build();

        await _postgreSqlContainer.StartAsync();
        _connectionString = _postgreSqlContainer.GetConnectionString();

        // Start WireMock server for JWKS endpoint (Dynamic JWT validation)
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
        _disposed = false;
        TimeProvider = new FakeTimeProvider(TestTime);
        MockAiService = new MockAiProcessingService();

        // Create WebApplicationFactory with test configuration
        #pragma warning disable CA2000 // Factory disposed in TearDownAsync
        Factory = new WebApplicationFactory<Program>()
        #pragma warning restore CA2000
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

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
                        ["Dynamic:Authority"] = "",
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

                    // Replace TimeProvider with deterministic one
                    services.AddSingleton<TimeProvider>(TimeProvider);

                    // Replace AI Processing Service with mock (must match application's Scoped lifetime)
                    var aiServiceDescriptor = services.FirstOrDefault(d =>
                        d.ServiceType == typeof(Axon.Modules.Chat.Application.Contracts.AI.IAiProcessingService));
                    if (aiServiceDescriptor != null)
                    {
                        services.Remove(aiServiceDescriptor);
                    }
                    services.AddScoped<Axon.Modules.Chat.Application.Contracts.AI.IAiProcessingService>(sp => MockAiService);

                    // Post-configure JWT authentication for tests
                    services.PostConfigure<JwtBearerOptions>("DynamicJwt", options =>
                    {
                        options.TokenValidationParameters.ValidIssuer = TestDataFixtures.DynamicIssuer;
                        options.TokenValidationParameters.IssuerSigningKeys = JwtTestTokenFactory.GetTestSigningKeys();

                        // CRITICAL FIX: Disable lifetime validation for E2E tests
                        // JWT middleware uses system clock (not FakeTimeProvider), so test tokens from 2024 appear expired in 2025
                        options.TokenValidationParameters.ValidateLifetime = false;
                        options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;

                        options.RefreshOnIssuerKeyNotFound = false;
                        options.RequireHttpsMetadata = false;
                    });

                    // Post-configure AxonJwt authentication scheme (for internal tokens)
                    services.PostConfigure<JwtBearerOptions>("AxonJwt", options =>
                    {
                        // Disable lifetime validation for AxonJwt as well
                        options.TokenValidationParameters.ValidateLifetime = false;
                        options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
                    });

                    // Configure DynamicAuthService validation
                    services.PostConfigure<Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration.DynamicValidationOptions>(options =>
                    {
                        options.EnvironmentMapping["dyn_test_env_12345"] = "test";
                        options.EnableBackgroundRefresh = false;
                        options.ValidateAudience = true;
                        options.DefaultAllowedAudiences = new List<string> { "axon-api", "axon-web" };

                        // CRITICAL FIX: Disable lifetime validation in DynamicAuthService
                        // This prevents SecurityTokenExpiredException when using test tokens from 2024 in 2025
                        options.ValidateLifetime = false;
                        options.ClockSkewSeconds = 0;
                    });

                    // Replace IJwksService with test implementation
                    services.AddSingleton<Axon.Modules.Identity.Application.Contracts.ExternalServices.IJwksService, TestJwksService>();

                    // Allow derived classes to configure additional services
                    ConfigureTestServices(services);
                });

                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Warning);
                });
            });

        HttpClient = Factory.CreateClient();

        // Setup database schemas (Chat + Identity)
        // EnsureDeletedAsync ensures clean state by dropping entire database first
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
    /// Ensures both Chat and Identity database schemas are properly set up.
    /// CRITICAL: Chat module requires Identity for authentication, so both must be initialized.
    ///
    /// HYBRID APPROACH (matching production):
    /// - IdentityContext (ASP.NET Identity): EnsureCreatedAsync (not migrated in production)
    /// - IdentityWriteDbContext (Domain): MigrateAsync (migrated in production)
    /// - ChatDbContext (Domain): MigrateAsync (migrated in production)
    /// </summary>
    private async Task EnsureDatabaseSetupAsync()
    {
        using var scope = Factory.Services.CreateScope();

        // Get all required DbContexts
        var identityWriteDbContext = scope.ServiceProvider.GetRequiredService<IdentityWriteDbContext>();
        var identityContext = scope.ServiceProvider.GetRequiredService<Axon.Modules.Identity.Infrastructure.Persistence.Context.IdentityContext>();
        var chatDbContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        // Delete entire database to ensure clean state
        await identityWriteDbContext.Database.EnsureDeletedAsync();

        // STEP 1: Create ASP.NET Identity schema using EnsureCreatedAsync
        // IdentityContext is NOT migrated in production (see Program.cs ApplyMigrationsAsync)
        // so we use EnsureCreatedAsync for test compatibility
        await identityContext.Database.EnsureCreatedAsync();

        // STEP 2: Apply Identity domain migrations (Principal, Wallet, etc.)
        // These ARE migrated in production
        await identityWriteDbContext.Database.MigrateAsync();

        // STEP 3: Apply Chat migrations (Conversation, Message, etc.)
        // These ARE migrated in production
        await chatDbContext.Database.MigrateAsync();
    }

    /// <summary>
    /// Cleans up database state between tests.
    /// Truncates both Chat and Identity tables to ensure test isolation.
    /// CRITICAL: Must truncate in correct order (children → parents) to avoid FK violations.
    /// </summary>
    private async Task CleanupDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var chatDbContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        try
        {
            // CORRECT TRUNCATION ORDER: Children first, then parents
            // This prevents foreign key constraint violations
            await chatDbContext.Database.ExecuteSqlRawAsync(@"
                -- Chat tables (children first)
                TRUNCATE TABLE chat.""Message"" CASCADE;
                TRUNCATE TABLE chat.""Conversation"" CASCADE;

                -- Identity tables - CRITICAL ORDER:
                -- 1. PrincipalChainDefault (references Principal + Wallet)
                TRUNCATE TABLE identity.""PrincipalChainDefault"" CASCADE;

                -- 2. WalletOwnership (references Principal + Wallet) - MUST come before Wallet/Principal
                TRUNCATE TABLE identity.""WalletOwnership"" CASCADE;

                -- 3. Credential (references Principal)
                TRUNCATE TABLE identity.""Credential"" CASCADE;

                -- 4. Wallet (parent table, no dependencies)
                TRUNCATE TABLE identity.""Wallet"" CASCADE;

                -- 5. Principal (parent table, no dependencies)
                TRUNCATE TABLE identity.""Principal"" CASCADE;

                -- 6. ASP.NET Identity tables
                TRUNCATE TABLE identity.""AspNetUserRoles"" CASCADE;
                TRUNCATE TABLE identity.""AspNetUsers"" CASCADE;
                TRUNCATE TABLE identity.""AspNetRoles"" CASCADE;
            ");
        }
        catch
        {
            // If truncate fails, recreate entire database using same hybrid approach as setup
            var identityWriteDbContext = scope.ServiceProvider.GetRequiredService<IdentityWriteDbContext>();
            var identityContext = scope.ServiceProvider.GetRequiredService<Axon.Modules.Identity.Infrastructure.Persistence.Context.IdentityContext>();

            await identityWriteDbContext.Database.EnsureDeletedAsync();
            await identityContext.Database.EnsureCreatedAsync();
            await identityWriteDbContext.Database.MigrateAsync();
            await chatDbContext.Database.MigrateAsync();
        }
    }

    #endregion

    #region WireMock JWKS Setup

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

    protected void SetAuthorizationHeader(string jwt)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
    }

    protected void ClearAuthorizationHeader()
    {
        HttpClient.DefaultRequestHeaders.Authorization = null;
    }

    protected static StringContent CreateJsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    #endregion

    #region Time Management

    protected void AdvanceTime(TimeSpan duration)
    {
        TimeProvider.Advance(duration);
    }

    protected void SetTime(DateTime time)
    {
        TimeProvider.SetUtcNow(time);
    }

    #endregion

    #region DbContext Registration Helpers

    /// <summary>
    /// Removes existing DbContext registrations to allow re-registration with test connection string.
    /// </summary>
    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        // Remove Chat DbContexts
        var chatDbContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ChatDbContext));
        if (chatDbContextDescriptor != null)
        {
            services.Remove(chatDbContextDescriptor);
        }

        var chatReadDbContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ChatReadDbContext));
        if (chatReadDbContextDescriptor != null)
        {
            services.Remove(chatReadDbContextDescriptor);
        }

        // Remove Identity DbContexts
        var identityWriteDbContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IdentityWriteDbContext));
        if (identityWriteDbContextDescriptor != null)
        {
            services.Remove(identityWriteDbContextDescriptor);
        }

        var identityContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Axon.Modules.Identity.Infrastructure.Persistence.Context.IdentityContext));
        if (identityContextDescriptor != null)
        {
            services.Remove(identityContextDescriptor);
        }

        var identityReadDbContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Axon.Modules.Identity.Infrastructure.Persistence.DbContexts.IdentityReadDbContext));
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
        // Register Chat DbContexts with test connection string
        services.AddDbContext<ChatDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
            });
        });

        services.AddDbContext<ChatReadDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
                npgsqlOptions.CommandTimeout(30);
            });
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        // Register Identity DbContexts with test connection string
        services.AddDbContext<IdentityWriteDbContext>(options =>
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

        // Register IdentityReadDbContext (required by some Identity services)
        services.AddDbContext<Axon.Modules.Identity.Infrastructure.Persistence.DbContexts.IdentityReadDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
                npgsqlOptions.CommandTimeout(60);
            });
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.EnableSensitiveDataLogging(false);
        });
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
