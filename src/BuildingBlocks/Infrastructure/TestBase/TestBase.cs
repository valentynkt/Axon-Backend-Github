using System.Globalization;
using System.Net;
using System.Security.Claims;
using Ardalis.GuardClauses;
using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using BuildingBlocks.Infrastructure.Persistence.Infrastructure;
using BuildingBlocks.Infrastructure.Persistence.PersistMessageProcessor;
using BuildingBlocks.Infrastructure.Persistence.Postgres;
using BuildingBlocks.Web;
using Duende.IdentityServer.EntityFramework.Entities;
using EasyNetQ.Management.Client;
using Grpc.Net.Client;
using MassTransit;
using MassTransit.Testing;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;
using Respawn;
using Testcontainers.EventStoreDb;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using WebMotions.Fake.Authentication.JwtBearer;
using Xunit;
using Xunit.Abstractions;

namespace BuildingBlocks.Infrastructure.TestBase;

// using Testcontainers.MongoDb;

public class TestFixture<TEntryPoint> : IAsyncLifetime, IDisposable
where TEntryPoint : class
{
    private readonly WebApplicationFactory<TEntryPoint> _factory;
    private static int Timeout => 120; // Second // Second
    private ITestHarness? TestHarness => ServiceProvider?.GetTestHarness();
    private Action<IServiceCollection>? TestRegistrationServices { get; set; }
    private PostgreSqlContainer? PostgresTestcontainer;
    private PostgreSqlContainer? PostgresPersistTestContainer;
    public RabbitMqContainer? RabbitMqTestContainer { get; private set; }
    // public MongoDbContainer MongoDbTestContainer;
    public EventStoreDbContainer? EventStoreDbTestContainer { get; private set; }
    public CancellationTokenSource? CancellationTokenSource { get; private set; }
    private bool _disposed;

    public PersistMessageBackgroundService PersistMessageBackgroundService =>
        ServiceProvider.GetRequiredService<PersistMessageBackgroundService>();

    public HttpClient HttpClient
    {
        get
        {
            var claims = new Dictionary<string, object>
                         {
                             { ClaimTypes.Name, "test@sample.com" },
                             { ClaimTypes.Role, "admin" },
                             { "scope", "flight-api" }
                         };

            var httpClient = _factory.CreateClient();
            httpClient.SetFakeBearerToken(claims); // Uses FakeJwtBearer
            return httpClient;
        }
    }

    public GrpcChannel Channel =>
        GrpcChannel.ForAddress(
            HttpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = HttpClient });

    public IServiceProvider ServiceProvider => _factory?.Services ?? throw new InvalidOperationException("Factory not initialized");
    public IConfiguration Configuration => _factory?.Services.GetRequiredService<IConfiguration>() ?? throw new InvalidOperationException("Factory not initialized");
    public ILogger? Logger { get; set; }

    protected TestFixture()
    {
        try
        {
#pragma warning disable CA2000 // Dispose objects before losing scope - WebApplicationFactory is properly disposed in Dispose() and DisposeAsync() methods
            _factory = new WebApplicationFactory<TEntryPoint>()
                .WithWebHostBuilder(
                    builder =>
                    {
                        builder.ConfigureAppConfiguration(AddCustomAppSettings);

                        builder.UseEnvironment("test");

                        builder.ConfigureServices(
                            services =>
                            {
                                TestRegistrationServices?.Invoke(services);
                                services.ReplaceSingleton(AddHttpContextAccessorMock);

                                services.AddSingleton<PersistMessageBackgroundService>();
                                services.RemoveHostedService<PersistMessageBackgroundService>();

                                // Register all ITestDataSeeder implementations dynamically
                                services.Scan(scan => scan
                                                  .FromApplicationDependencies() // Scan the current app and its dependencies
                                                  .AddClasses(classes => classes.AssignableTo<ITestDataSeeder>()) // Find classes that implement ITestDataSeeder
                                                  .AsImplementedInterfaces()
                                                  .WithScopedLifetime());

                                // Add Fake JWT Authentication - we can use SetAdminUser method to set authenticate user to existing HttContextAccessor
                                // https://github.com/webmotions/fake-authentication-jwtbearer
                                // https://github.com/webmotions/fake-authentication-jwtbearer/issues/14
                                services.AddAuthentication(
                                        options =>
                                        {
                                            options.DefaultAuthenticateScheme = FakeJwtBearerDefaults.AuthenticationScheme;

                                            options.DefaultChallengeScheme = FakeJwtBearerDefaults.AuthenticationScheme;
                                        })
                                    .AddFakeJwtBearer();

                                // Mock Authorization Policies
                                services.AddAuthorizationBuilder()
                                    .AddPolicy(nameof(ApiScope), policy =>
                                    {
                                        policy.AddAuthenticationSchemes(FakeJwtBearerDefaults.AuthenticationScheme);
                                        policy.RequireAuthenticatedUser();
                                        policy.RequireClaim("scope", "flight-api"); // Test-specific scope
                                    });
                            });
                    });
#pragma warning restore CA2000 // Dispose objects before losing scope
        }
        catch
        {
            // CA2000: Ensure factory is disposed if constructor fails
            _factory?.Dispose();
            throw;
        }
    }

    public async Task InitializeAsync()
    {
        CancellationTokenSource = new CancellationTokenSource();
        await StartTestContainerAsync();
    }

    public async Task DisposeAsync()
    {
        await StopTestContainerAsync();
        await _factory.DisposeAsync();
        if (CancellationTokenSource != null)
        {
            await CancellationTokenSource.CancelAsync();
        }
        Dispose();
        // CA1816: Do not call GC.SuppressFinalize in DisposeAsync - only in Dispose()
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            CancellationTokenSource?.Dispose();
            _factory?.Dispose();
            _disposed = true;
        }
    }

    public virtual void RegisterServices(Action<IServiceCollection> services)
    {
        TestRegistrationServices += services;
    }

    // ref: https://github.com/trbenning/serilog-sinks-xunit
    public ILogger? CreateLogger(ITestOutputHelper? output)
    {
        if (output == null)
            return null;

        using var loggerFactory = LoggerFactory.Create(builder =>
                                                 {
                                                     builder.AddXunit(output);
                                                     builder.SetMinimumLevel(LogLevel.Debug);
                                                 });
        return loggerFactory.CreateLogger("TestLogger");
    }

    protected async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = ServiceProvider.CreateScope();
        await action(scope.ServiceProvider);
    }

    protected async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = ServiceProvider.CreateScope();

        var result = await action(scope.ServiceProvider);

        return result;
    }


    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        return ExecuteScopeAsync(
            sp =>
            {
                var mediator = sp.GetRequiredService<IMediator>();

                return mediator.Send(request);
            });
    }

    public Task SendAsync(IRequest request)
    {
        return ExecuteScopeAsync(
            sp =>
            {
                var mediator = sp.GetRequiredService<IMediator>();
                return mediator.Send(request);
            });
    }

    public async Task Publish<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
    where TMessage : class, IEvent
    {
        if (TestHarness?.Bus != null)
        {
            await TestHarness.Bus.Publish(message, cancellationToken);
        }
    }

    public async Task<bool> WaitForPublishing<TMessage>(
        CancellationToken cancellationToken = default
    )
    where TMessage : class, IEvent
    {
        var result = await WaitUntilConditionMet(
                         async () =>
                         {
                             if (TestHarness?.Published != null)
                             {
                                 var published =
                                     await TestHarness.Published.Any<TMessage>(cancellationToken);

                                 return published;
                             }
                             return false;
                         });

        return result;
    }

    public async Task<bool> WaitForConsuming<TMessage>(
        CancellationToken cancellationToken = default
    )
    where TMessage : class, IEvent
    {
        var result = await WaitUntilConditionMet(
                         async () =>
                         {
                             if (TestHarness?.Consumed != null)
                             {
                                 var consumed =
                                     await TestHarness.Consumed.Any<TMessage>(cancellationToken);

                                 return consumed;
                             }
                             return false;
                         });

        return result;
    }

    public async Task<bool> ShouldProcessedPersistInternalCommand<TInternalCommand>(
        CancellationToken cancellationToken = default
    )
    where TInternalCommand : class, IInternalCommand
    {
        _ = cancellationToken; // Parameter reserved for future use
        
        var result = await WaitUntilConditionMet(
                         async () =>
                         {
                             return await ExecuteScopeAsync(
                                        async sp =>
                                        {
                                            var persistMessageProcessor =
                                                sp.GetService<IPersistMessageProcessor>();

                                            Guard.Against.Null(
                                                persistMessageProcessor,
                                                nameof(persistMessageProcessor));

                                            var filter =
                                                await persistMessageProcessor.GetByFilterAsync(
                                                    x =>
                                                        x.DeliveryType ==
                                                        MessageDeliveryType.Internal &&
                                                        typeof(TInternalCommand).ToString() ==
                                                        x.DataType);

                                            var res = filter.Any(
                                                x => x.MessageStatus == MessageStatus.Processed);

                                            return res;
                                        });
                         });

        return result;
    }

    // Ref: https://tech.energyhelpline.com/in-memory-testing-with-masstransit/
    private static async Task<bool> WaitUntilConditionMet(
        Func<Task<bool>> conditionToMet,
        int? timeoutSecond = null
    )
    {
        var time = timeoutSecond ?? Timeout;

        var startTime = DateTime.Now;
        var timeoutExpired = false;
        var meet = await conditionToMet.Invoke();

        while (!meet)
        {
            if (timeoutExpired)
            {
                return false;
            }

            await Task.Delay(100);
            meet = await conditionToMet.Invoke();
            timeoutExpired = DateTime.Now - startTime > TimeSpan.FromSeconds(time);
        }

        return true;
    }

    private async Task StartTestContainerAsync()
    {
        PostgresTestcontainer = TestContainers.PostgresTestContainer();
        PostgresPersistTestContainer = TestContainers.PostgresPersistTestContainer();
        RabbitMqTestContainer = TestContainers.RabbitMqTestContainer();
        // MongoDbTestContainer = TestContainers.MongoTestContainer();
        EventStoreDbTestContainer = TestContainers.EventStoreTestContainer();

        // await MongoDbTestContainer.StartAsync();
        if (PostgresTestcontainer != null)
            await PostgresTestcontainer.StartAsync();
        if (PostgresPersistTestContainer != null)
            await PostgresPersistTestContainer.StartAsync();
        if (RabbitMqTestContainer != null)
            await RabbitMqTestContainer.StartAsync();
        if (EventStoreDbTestContainer != null)
            await EventStoreDbTestContainer.StartAsync();
    }

    private async Task StopTestContainerAsync()
    {
        if (PostgresTestcontainer != null)
            await PostgresTestcontainer.StopAsync();
        if (PostgresPersistTestContainer != null)
            await PostgresPersistTestContainer.StopAsync();
        if (RabbitMqTestContainer != null)
            await RabbitMqTestContainer.StopAsync();
        // await MongoDbTestContainer.StopAsync();
        if (EventStoreDbTestContainer != null)
            await EventStoreDbTestContainer.StopAsync();
    }

    private void AddCustomAppSettings(IConfigurationBuilder configuration)
    {
        //todo: provide better approach for reading `PostgresOptions`
        configuration.AddInMemoryCollection(
            new KeyValuePair<string, string?>[]
            {
                new(
                    "PostgresOptions:ConnectionString",
                    PostgresTestcontainer?.GetConnectionString() ?? string.Empty),
                new(
                    "PostgresOptions:ConnectionString:Flight",
                    PostgresTestcontainer?.GetConnectionString() ?? string.Empty),
                 new(
                    "PostgresOptions:ConnectionString:Identity",
                    PostgresTestcontainer?.GetConnectionString() ?? string.Empty),
                 new(
                    "PostgresOptions:ConnectionString:Passenger",
                    PostgresTestcontainer?.GetConnectionString() ?? string.Empty),
                new(
                    "PersistMessageOptions:ConnectionString",
                    PostgresPersistTestContainer?.GetConnectionString() ?? string.Empty),
                new("RabbitMqOptions:HostName", RabbitMqTestContainer?.Hostname ?? "localhost"),
                new(
                    "RabbitMqOptions:UserName",
                    TestContainers.RabbitMqContainerConfiguration.UserName),
                new(
                    "RabbitMqOptions:Password",
                    TestContainers.RabbitMqContainerConfiguration.Password),
                new(
                    "RabbitMqOptions:Port",
                    (RabbitMqTestContainer?.GetMappedPublicPort(
                            TestContainers.RabbitMqContainerConfiguration.Port) ?? TestContainers.RabbitMqContainerConfiguration.Port)
                        .ToString(NumberFormatInfo.InvariantInfo)),
                // new("MongoOptions:ConnectionString", MongoDbTestContainer.GetConnectionString()),
                new("MongoOptions:DatabaseName", TestContainers.MongoContainerConfiguration.Name),
                new(
                    "EventStoreOptions:ConnectionString",
                    EventStoreDbTestContainer?.GetConnectionString() ?? string.Empty)
            });
    }

    private static IHttpContextAccessor AddHttpContextAccessorMock(IServiceProvider serviceProvider)
    {
        var httpContextAccessorMock = Substitute.For<IHttpContextAccessor>();
        using var scope = serviceProvider.CreateScope();

        httpContextAccessorMock.HttpContext = new DefaultHttpContext
        { RequestServices = scope.ServiceProvider };

        httpContextAccessorMock.HttpContext.Request.Host = new HostString("localhost", 6012);
        httpContextAccessorMock.HttpContext.Request.Scheme = "http";

        return httpContextAccessorMock;
    }
}

public class TestWriteFixture<TEntryPoint, TWContext> : TestFixture<TEntryPoint>
where TEntryPoint : class
where TWContext : DbContext
{
    public Task ExecuteDbContextAsync(Func<TWContext, Task> action)
    {
        return ExecuteScopeAsync(sp => action(sp.GetRequiredService<TWContext>()));
    }

    public Task ExecuteDbContextAsync(Func<TWContext, ValueTask> action)
    {
        return ExecuteScopeAsync(sp => action(sp.GetRequiredService<TWContext>()).AsTask());
    }

    public Task ExecuteDbContextAsync(Func<TWContext, IMediator, Task> action)
    {
        return ExecuteScopeAsync(
            sp => action(sp.GetRequiredService<TWContext>(), sp.GetRequiredService<IMediator>()));
    }

    public Task<T> ExecuteDbContextAsync<T>(Func<TWContext, Task<T>> action)
    {
        return ExecuteScopeAsync(sp => action(sp.GetRequiredService<TWContext>()));
    }

    public Task<T> ExecuteDbContextAsync<T>(Func<TWContext, ValueTask<T>> action)
    {
        return ExecuteScopeAsync(sp => action(sp.GetRequiredService<TWContext>()).AsTask());
    }

    public Task<T> ExecuteDbContextAsync<T>(Func<TWContext, IMediator, Task<T>> action)
    {
        return ExecuteScopeAsync(
            sp => action(sp.GetRequiredService<TWContext>(), sp.GetRequiredService<IMediator>()));
    }

    public Task InsertAsync<T>(params T[] entities)
    where T : class
    {
        return ExecuteDbContextAsync(
            db =>
            {
                foreach (var entity in entities)
                {
                    db.Set<T>().Add(entity);
                }

                return db.SaveChangesAsync();
            });
    }

    public async Task InsertAsync<TEntity>(TEntity entity)
    where TEntity : class
    {
        await ExecuteDbContextAsync(
            db =>
            {
                db.Set<TEntity>().Add(entity);

                return db.SaveChangesAsync();
            });
    }

    public Task InsertAsync<TEntity, TEntity2>(TEntity entity, TEntity2 entity2)
    where TEntity : class
    where TEntity2 : class
    {
        return ExecuteDbContextAsync(
            db =>
            {
                db.Set<TEntity>().Add(entity);
                db.Set<TEntity2>().Add(entity2);

                return db.SaveChangesAsync();
            });
    }

    public Task InsertAsync<TEntity, TEntity2, TEntity3>(
        TEntity entity,
        TEntity2 entity2,
        TEntity3 entity3
    )
    where TEntity : class
    where TEntity2 : class
    where TEntity3 : class
    {
        return ExecuteDbContextAsync(
            db =>
            {
                db.Set<TEntity>().Add(entity);
                db.Set<TEntity2>().Add(entity2);
                db.Set<TEntity3>().Add(entity3);

                return db.SaveChangesAsync();
            });
    }

    public Task InsertAsync<TEntity, TEntity2, TEntity3, TEntity4>(
        TEntity entity,
        TEntity2 entity2,
        TEntity3 entity3,
        TEntity4 entity4
    )
    where TEntity : class
    where TEntity2 : class
    where TEntity3 : class
    where TEntity4 : class
    {
        return ExecuteDbContextAsync(
            db =>
            {
                db.Set<TEntity>().Add(entity);
                db.Set<TEntity2>().Add(entity2);
                db.Set<TEntity3>().Add(entity3);
                db.Set<TEntity4>().Add(entity4);

                return db.SaveChangesAsync();
            });
    }

    public Task<T?> FindAsync<T, TKey>(TKey id)
    where T : class, IEntity<IStrongId>
    {
        return ExecuteDbContextAsync(db => db.Set<T>().FindAsync(id).AsTask());
    }

    public Task<T?> FirstOrDefaultAsync<T>()
    where T : class, IEntity<IStrongId>
    {
        return ExecuteDbContextAsync(db => db.Set<T>().FirstOrDefaultAsync());
    }
}

public class TestReadFixture<TEntryPoint, TRContext> : TestFixture<TEntryPoint>
where TEntryPoint : class
where TRContext : DbContext, IDbContext
{
    public Task ExecuteReadContextAsync(Func<TRContext, Task> action)
    {
        return ExecuteScopeAsync(sp => action(sp.GetRequiredService<TRContext>()));
    }

    public Task<T> ExecuteReadContextAsync<T>(Func<TRContext, Task<T>> action)
    {
        return ExecuteScopeAsync(sp => action(sp.GetRequiredService<TRContext>()));
    }

    [Obsolete("MongoDB support has been removed. Use PostgreSQL-based testing instead.")]
    public Task InsertMongoDbContextAsync<T>(string collectionName, params T[] entities)
        where T : class
    {
        throw new NotSupportedException("MongoDB support has been removed from the test framework. Use PostgreSQL-based testing instead.");
    }
}

public class TestFixture<TEntryPoint, TWContext, TRContext>
    : TestWriteFixture<TEntryPoint, TWContext>
where TEntryPoint : class
where TWContext : DbContext
where TRContext : DbContext, IDbContext
{
    public Task ExecuteReadContextAsync(Func<TRContext, Task> action)
    {
        return ExecuteScopeAsync(sp => action(sp.GetRequiredService<TRContext>()));
    }

    public Task<T> ExecuteReadContextAsync<T>(Func<TRContext, Task<T>> action)
    {
        return ExecuteScopeAsync(sp => action(sp.GetRequiredService<TRContext>()));
    }

    [Obsolete("MongoDB support has been removed. Use PostgreSQL-based testing instead.")]
    public Task InsertMongoDbContextAsync<T>(string collectionName, params T[] entities)
        where T : class
    {
        throw new NotSupportedException("MongoDB support has been removed from the test framework. Use PostgreSQL-based testing instead.");
    }
}

public class TestFixtureCore<TEntryPoint> : IAsyncLifetime
where TEntryPoint : class
{
    private Respawner? _reSpawnerDefaultDb;
    private Respawner? _reSpawnerPersistDb;
    private NpgsqlConnection? DefaultDbConnection { get; set; }
    private NpgsqlConnection? PersistDbConnection { get; set; }


    public TestFixtureCore(
        TestFixture<TEntryPoint> integrationTestFixture,
        ITestOutputHelper? outputHelper
    )
    {
        Fixture = integrationTestFixture;
        integrationTestFixture.RegisterServices(RegisterTestsServices);
        integrationTestFixture.Logger = integrationTestFixture.CreateLogger(outputHelper);
    }

    public TestFixture<TEntryPoint> Fixture { get; }


    public async Task InitializeAsync()
    {
        await InitPostgresAsync();
    }

    public async Task DisposeAsync()
    {
        await ResetPostgresAsync();
        await ResetMongoAsync();
        await ResetRabbitMqAsync();
        
        // CA2000: Dispose database connections
        if (DefaultDbConnection != null)
        {
            await DefaultDbConnection.DisposeAsync();
            DefaultDbConnection = null;
        }
        
        if (PersistDbConnection != null)
        {
            await PersistDbConnection.DisposeAsync();
            PersistDbConnection = null;
        }
    }

    private async Task InitPostgresAsync()
    {
        var postgresOptions = Fixture.ServiceProvider.GetService<PostgresOptions>();
        var persistOptions = Fixture.ServiceProvider.GetService<PersistMessageOptions>();

        if (!string.IsNullOrEmpty(persistOptions?.ConnectionString))
        {
            await Fixture.PersistMessageBackgroundService.StartAsync(
                Fixture.CancellationTokenSource?.Token ?? CancellationToken.None);

            PersistDbConnection = new NpgsqlConnection(persistOptions.ConnectionString);
            await PersistDbConnection.OpenAsync();

            _reSpawnerPersistDb = await Respawner.CreateAsync(
                                      PersistDbConnection,
                                      new RespawnerOptions { DbAdapter = DbAdapter.Postgres });
        }

        if (!string.IsNullOrEmpty(postgresOptions?.ConnectionString))
        {
            DefaultDbConnection = new NpgsqlConnection(postgresOptions.ConnectionString);
            await DefaultDbConnection.OpenAsync();

            _reSpawnerDefaultDb = await Respawner.CreateAsync(
                                      DefaultDbConnection,
                                      new RespawnerOptions { DbAdapter = DbAdapter.Postgres });

            await SeedDataAsync();
        }
    }

    private async Task ResetPostgresAsync()
    {
        if (PersistDbConnection is not null && _reSpawnerPersistDb is not null)
        {
            await _reSpawnerPersistDb.ResetAsync(PersistDbConnection);

            await Fixture.PersistMessageBackgroundService.StopAsync(
                Fixture.CancellationTokenSource?.Token ?? CancellationToken.None);
        }

        if (DefaultDbConnection is not null && _reSpawnerDefaultDb is not null)
        {
            await _reSpawnerDefaultDb.ResetAsync(DefaultDbConnection);
        }
    }

    private static async Task ResetMongoAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken; // Parameter reserved for future use
        
        //https://stackoverflow.com/questions/3366397/delete-everything-in-a-mongodb-database
        // var dbClient = new MongoClient(Fixture.MongoDbTestContainer?.GetConnectionString());

        // var collections = await dbClient
        //                       .GetDatabase(TestContainers.MongoContainerConfiguration.Name)
        //                       .ListCollectionsAsync(cancellationToken: cancellationToken);

        // foreach (var collection in collections.ToList())
        // {
        //     await dbClient.GetDatabase(TestContainers.MongoContainerConfiguration.Name)
        //         .DropCollectionAsync(collection["name"].AsString, cancellationToken);
        // }
        
        await Task.CompletedTask; // No-op for now - MongoDB functionality disabled
    }

    private async Task ResetRabbitMqAsync(CancellationToken cancellationToken = default)
    {
        var port = Fixture.RabbitMqTestContainer?.GetMappedPublicPort(
                       TestContainers.RabbitMqContainerConfiguration
                           .ApiPort) ??
                   TestContainers.RabbitMqContainerConfiguration.ApiPort;

        using var managementClient = new ManagementClient(
            Fixture.RabbitMqTestContainer?.Hostname ?? "localhost",
            TestContainers.RabbitMqContainerConfiguration?.UserName ?? "guest",
            TestContainers.RabbitMqContainerConfiguration?.Password ?? "guest", 
            port);

        var bd = await managementClient.GetBindingsAsync(cancellationToken);

        var bindings = bd.Where(
            x => !string.IsNullOrEmpty(x.Source) && !string.IsNullOrEmpty(x.Destination));

        foreach (var binding in bindings)
        {
            await managementClient.DeleteBindingAsync(binding, cancellationToken);
        }

        var queues = await managementClient.GetQueuesAsync(cancellationToken: cancellationToken);

        foreach (var queue in queues)
        {
            await managementClient.PurgeAsync(queue, cancellationToken);
        }
    }

    protected virtual void RegisterTestsServices(IServiceCollection services)
    {
    }

    private async Task SeedDataAsync()
    {
        using var scope = Fixture.ServiceProvider.CreateScope();

        var seedManager = scope.ServiceProvider.GetService<ISeedManager>();
        if (seedManager != null)
        {
            await seedManager.ExecuteTestSeedAsync();
        }
    }
}

public abstract class TestReadBase<TEntryPoint, TRContext> : TestFixtureCore<TEntryPoint>
// ,IClassFixture<IntegrationTestFactory<TEntryPoint, TWContext>>
where TEntryPoint : class
where TRContext : DbContext, IDbContext
{
    protected TestReadBase(
        TestReadFixture<TEntryPoint, TRContext> integrationTestFixture,
        ITestOutputHelper? outputHelper = null
    ) : base(integrationTestFixture, outputHelper)
    {
        Fixture = integrationTestFixture;
    }

    public new TestReadFixture<TEntryPoint, TRContext> Fixture { get; }
}

public abstract class TestWriteBase<TEntryPoint, TWContext> : TestFixtureCore<TEntryPoint>
//,IClassFixture<IntegrationTestFactory<TEntryPoint, TWContext>>
where TEntryPoint : class
where TWContext : DbContext
{
    protected TestWriteBase(
        TestWriteFixture<TEntryPoint, TWContext> integrationTestFixture,
        ITestOutputHelper? outputHelper = null
    ) : base(integrationTestFixture, outputHelper)
    {
        Fixture = integrationTestFixture;
    }

    public new TestWriteFixture<TEntryPoint, TWContext> Fixture { get; }
}

public abstract class TestBase<TEntryPoint, TWContext, TRContext> : TestFixtureCore<TEntryPoint>
//,IClassFixture<IntegrationTestFactory<TEntryPoint, TWContext, TRContext>>
where TEntryPoint : class
where TWContext : DbContext
where TRContext : DbContext, IDbContext
{
    protected TestBase(
        TestFixture<TEntryPoint, TWContext, TRContext> integrationTestFixture,
        ITestOutputHelper? outputHelper = null
    ) :
        base(integrationTestFixture, outputHelper)
    {
        Fixture = integrationTestFixture;
    }

    public new TestFixture<TEntryPoint, TWContext, TRContext> Fixture { get; }
}