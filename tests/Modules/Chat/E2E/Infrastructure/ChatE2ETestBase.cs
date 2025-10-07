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
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
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
    protected AuthHeaderDelegatingHandler AuthHandler { get; private set; } = null!;

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

                    // Test-specific overrides using centralized configuration helper
                    // This ensures consistency with Identity E2E tests
                    var testConfig = TestJwtConfiguration.CreateTestConfiguration(_connectionString, JwksServer.Port);
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

                    // CRITICAL FIX: Remove IdempotencyBehavior for E2E tests to prevent cached responses
                    // This prevents idempotency from interfering with multi-request test scenarios
                    var idempotencyBehaviors = services
                        .Where(d => d.ServiceType.IsGenericType &&
                                    d.ServiceType.GetGenericTypeDefinition() == typeof(MediatR.IPipelineBehavior<,>) &&
                                    d.ImplementationType?.Name.Contains("Idempotency") == true)
                        .ToList();
                    foreach (var descriptor in idempotencyBehaviors)
                    {
                        services.Remove(descriptor);
                    }

                    // Replace AI Processing Service with mock
                    // CRITICAL: Must be Singleton so all scopes get the same instance and configuration changes are visible
                    var aiServiceDescriptor = services.FirstOrDefault(d =>
                        d.ServiceType == typeof(Axon.Modules.Chat.Application.Contracts.AI.IAiProcessingService));
                    if (aiServiceDescriptor != null)
                    {
                        services.Remove(aiServiceDescriptor);
                    }
                    services.AddSingleton<Axon.Modules.Chat.Application.Contracts.AI.IAiProcessingService>(MockAiService);

                    // Configure JWT authentication using centralized TestJwtConfiguration helper
                    // This ensures consistency with Identity E2E tests

                    // Configure DynamicJwt authentication scheme (for Dynamic.xyz JWT tokens)
                    services.PostConfigure<JwtBearerOptions>("DynamicJwt", options =>
                    {
                        // Let JWT middleware fetch JWKS from WireMock endpoint (truly E2E)
                        // Path must match DynamicXyzOptions computation: /sdk/{EnvironmentId}/.well-known/jwks.json
                        var jwksUri = $"http://localhost:{JwksServer.Port}/sdk/test-env-id/.well-known/jwks.json";
                        TestJwtConfiguration.ConfigureDynamicJwtSchemeWithoutKeyInjection(options, jwksUri);
                    });

                    // Configure AxonJwt authentication scheme (for internal Axon tokens)
                    services.PostConfigure<JwtBearerOptions>("AxonJwt", options =>
                    {
                        TestJwtConfiguration.ConfigureAxonJwtScheme(options);
                    });

                    // Configure DynamicValidationOptions using centralized helper
                    services.PostConfigure<Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration.DynamicValidationOptions>(options =>
                    {
                        TestJwtConfiguration.ConfigureDynamicValidationOptions(options);
                    });

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

        // CRITICAL FIX: Clear memory cache AFTER Factory is created
        // This prevents authentication state leakage between tests
        ClearMemoryCacheIfExists();

        // Create auth header delegating handler
        // This ensures authorization headers persist across all HTTP requests
        AuthHandler = new AuthHeaderDelegatingHandler
        {
            InnerHandler = Factory.Server.CreateHandler()
        };

        // Create HttpClient with our delegating handler
        HttpClient = new HttpClient(AuthHandler)
        {
            BaseAddress = new Uri("http://localhost")
        };

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
    /// Ensures both Chat and Identity database schemas are properly set up.
    /// CRITICAL: Chat module requires Identity for authentication, so both must be initialized.
    ///
    /// HYBRID APPROACH (matching production):
    /// - IdentityContext (ASP.NET Identity): EnsureCreatedAsync (not migrated in production)
    /// - IdentityDbContext (Domain): MigrateAsync (migrated in production)
    /// - ChatDbContext (Domain): MigrateAsync (migrated in production)
    /// </summary>
    private async Task EnsureDatabaseSetupAsync()
    {
        using var scope = Factory.Services.CreateScope();

        // Get all required DbContexts
        var identityWriteDbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var identityContext = scope.ServiceProvider.GetRequiredService<Axon.Modules.Identity.Infrastructure.Persistence.Context.AspNetIdentityContext>();
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
            var identityWriteDbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var identityContext = scope.ServiceProvider.GetRequiredService<Axon.Modules.Identity.Infrastructure.Persistence.Context.AspNetIdentityContext>();

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

        // CRITICAL: Path must match DynamicXyzOptions.JwksUri computation
        // DynamicXyzOptions.JwksUri: {BaseUrl}/sdk/{EnvironmentId}/.well-known/jwks.json
        // We configure: BaseUrl=http://localhost:PORT, EnvironmentId=test-env-id
        // Result: /sdk/test-env-id/.well-known/jwks.json
        JwksServer
            .Given(Request.Create().WithPath("/sdk/test-env-id/.well-known/jwks.json").UsingGet())
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
        // Use the delegating handler to ensure the token is injected into ALL requests
        AuthHandler.SetBearerToken(jwt);
    }

    protected void ClearAuthorizationHeader()
    {
        // Clear the token from the delegating handler
        AuthHandler.ClearBearerToken();
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

        var chatReadDbContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ChatDbContext));
        if (chatReadDbContextDescriptor != null)
        {
            services.Remove(chatReadDbContextDescriptor);
        }

        // Remove Identity DbContexts
        var identityWriteDbContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IdentityDbContext));
        if (identityWriteDbContextDescriptor != null)
        {
            services.Remove(identityWriteDbContextDescriptor);
        }

        var identityContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Axon.Modules.Identity.Infrastructure.Persistence.Context.AspNetIdentityContext));
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
        // Register Chat DbContexts with test connection string
        services.AddDbContext<ChatDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
            });
        });

        services.AddDbContext<ChatDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
                npgsqlOptions.CommandTimeout(30);
            });
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

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

        services.AddDbContext<Axon.Modules.Identity.Infrastructure.Persistence.Context.AspNetIdentityContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            });
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        // Register IdentityDbContext (required by some Identity services)
        services.AddDbContext<Axon.Modules.Identity.Infrastructure.Persistence.DbContexts.IdentityDbContext>(options =>
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
    /// Clears distributed cache to prevent idempotency cache leakage between tests and within tests.
    /// IDistributedCache (MemoryDistributedCache in tests) caches command responses for idempotency.
    /// Without clearing, idempotent commands return cached responses from previous requests.
    /// CRITICAL FIX: This prevents ChatTurn E2E tests from returning cached responses across multiple requests.
    /// </summary>
    protected void ClearDistributedCacheIfExists()
    {
        try
        {
            if (Factory?.Services == null) return;

            // Get all IMemoryCache instances and compact them all
            // This clears both direct IMemoryCache usage AND the backing cache for IDistributedCache
            var allMemoryCaches = Factory.Services.GetServices<IMemoryCache>().ToList();
            Console.WriteLine($"[CACHE CLEAR] Found {allMemoryCaches.Count} IMemoryCache instances");

            foreach (var cache in allMemoryCaches)
            {
                if (cache is Microsoft.Extensions.Caching.Memory.MemoryCache memCache)
                {
                    Console.WriteLine($"[CACHE CLEAR] Compacting MemoryCache instance: {cache.GetType().FullName}");
                    // Compact(1.0) removes ALL cache entries
                    memCache.Compact(1.0);
                    Console.WriteLine($"[CACHE CLEAR] Successfully compacted cache");
                }
                else
                {
                    Console.WriteLine($"[CACHE CLEAR] Skipping non-MemoryCache instance: {cache.GetType().FullName}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CACHE CLEAR] Exception during cache clearing: {ex.Message}");
            // Silently ignore - cache clearing is best-effort for test isolation
        }
    }

    /// <summary>
    /// Clears EF Core DbContext change trackers to prevent entity tracking conflicts.
    /// PrincipalResolutionService reloads tracked entities for modifications.
    /// Without clearing, tracked entities from previous tests cause conflicts.
    /// </summary>
    protected void ClearDbContextChangeTrackers()
    {
        try
        {
            if (Factory?.Services == null) return;

            using var scope = Factory.Services.CreateScope();

            // Clear Chat Write DbContext
            var chatDb = scope.ServiceProvider.GetService<ChatDbContext>();
            chatDb?.ChangeTracker.Clear();

            // CRITICAL FIX: Also clear Chat Read DbContext
            // GetByIdAsync in ConversationWriteRepository can track entities even from read context
            var chatReadDb = scope.ServiceProvider.GetService<ChatDbContext>();
            chatReadDb?.ChangeTracker.Clear();

            // Clear Identity Write DbContext
            var identityWriteDb = scope.ServiceProvider.GetService<IdentityDbContext>();
            identityWriteDb?.ChangeTracker.Clear();

            // Clear Identity Read DbContext (if tracked)
            var identityReadDb = scope.ServiceProvider.GetService<Axon.Modules.Identity.Infrastructure.Persistence.DbContexts.IdentityDbContext>();
            identityReadDb?.ChangeTracker.Clear();
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
