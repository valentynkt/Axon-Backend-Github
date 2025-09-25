using System.Text;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Infrastructure.DependencyInjection;
using Axon.Modules.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using SimpleBase;

namespace Axon.Modules.Identity.Infrastructure.Services.Tests;

/// <summary>
/// Integration tests for Ed25519SignatureVerifier focusing on:
/// - Service registration and DI resolution
/// - Database integration scenarios
/// - Concurrent verification with shared resources
/// - Real-world authentication flow integration
/// </summary>
[TestFixture]
public class Ed25519SignatureVerifierIntegrationTests : IdentityPersistenceTestBase
{
    private ServiceProvider _serviceProvider = null!;
    private IWalletSignatureVerifier _verifier = null!;

    protected override async Task SetUpDerived()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Add database context with PostgreSQL
        services.AddDbContext<IdentityWriteDbContext>(options =>
        {
            options.UseNpgsql(ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(IdentityWriteDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            });
            options.EnableSensitiveDataLogging();
        });

        // Register Identity services including signature verifier
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString
            })
            .Build();
        services.AddIdentityInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _verifier = _serviceProvider.GetRequiredService<IWalletSignatureVerifier>();
        await Task.CompletedTask;
    }

    [TearDown]
    protected override async Task TearDownDerived()
    {
        _serviceProvider?.Dispose();
        await Task.CompletedTask;
    }

    #region Service Registration and DI Tests

    [Test]
    public void ServiceRegistration_ResolveIWalletSignatureVerifier_ReturnsEd25519Implementation()
    {
        // Act
        var resolvedService = _serviceProvider.GetRequiredService<IWalletSignatureVerifier>();

        // Assert
        resolvedService.ShouldNotBeNull();
        resolvedService.ShouldBeOfType<Ed25519SignatureVerifier>();
    }

    [Test]
    public void ServiceRegistration_SingletonLifetime_ReturnsSameInstance()
    {
        // Act
        var instance1 = _serviceProvider.GetRequiredService<IWalletSignatureVerifier>();
        var instance2 = _serviceProvider.GetRequiredService<IWalletSignatureVerifier>();

        // Assert
        ReferenceEquals(instance1, instance2).ShouldBeTrue();
    }

    [Test]
    public void ServiceRegistration_MultipleScopes_SharesSingletonInstance()
    {
        // Arrange
        using var scope1 = _serviceProvider.CreateScope();
        using var scope2 = _serviceProvider.CreateScope();

        // Act
        var instance1 = scope1.ServiceProvider.GetRequiredService<IWalletSignatureVerifier>();
        var instance2 = scope2.ServiceProvider.GetRequiredService<IWalletSignatureVerifier>();

        // Assert
        ReferenceEquals(instance1, instance2).ShouldBeTrue();
    }

    #endregion

    #region Database Integration Tests

    [Test]
    public async Task VerifySignature_WithDatabaseContext_DoesNotInterfereWithDbOperations()
    {
        // Arrange - Simulate database operations alongside signature verification
        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var message = "database integration test";
        var messageBytes = Encoding.UTF8.GetBytes(message);

        var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, messageBytes);
        var signature = Base58.Bitcoin.Encode(signatureBytes);

        var publicKeyBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var address = Base58.Bitcoin.Encode(publicKeyBytes);

        // Simulate some database work
        await DbContext.Database.ExecuteSqlRawAsync("SELECT 1");

        // Act
        var result = _verifier.VerifySignature("solana", address, message, signature);

        // Additional database work after verification
        await DbContext.Database.ExecuteSqlRawAsync("SELECT 1");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();

        // Verify database is still functional
        var dbWorks = await DbContext.Database.CanConnectAsync();
        dbWorks.ShouldBeTrue();
    }

    [Test]
    public async Task VerifySignature_ConcurrentWithDatabaseTransactions_MaintainsIsolation()
    {
        // Arrange - Test signature verification doesn't interfere with database transactions
        const int concurrentOperations = 5;
        var tasks = new List<Task<bool>>();

        for (int i = 0; i < concurrentOperations; i++)
        {
            var taskIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var scopedDbContext = scope.ServiceProvider.GetRequiredService<IdentityWriteDbContext>();
                var scopedVerifier = scope.ServiceProvider.GetRequiredService<IWalletSignatureVerifier>();

                // Start a database transaction
                using var transaction = await scopedDbContext.Database.BeginTransactionAsync();

                try
                {
                    // Perform signature verification during transaction
                    using var key = Key.Create(SignatureAlgorithm.Ed25519);
                    var message = $"concurrent test {taskIndex}";
                    var messageBytes = Encoding.UTF8.GetBytes(message);

                    var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, messageBytes);
                    var signature = Base58.Bitcoin.Encode(signatureBytes);

                    var publicKeyBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
                    var address = Base58.Bitcoin.Encode(publicKeyBytes);

                    var result = scopedVerifier.VerifySignature("solana", address, message, signature);

                    // Simulate some database work
                    await scopedDbContext.Database.ExecuteSqlRawAsync("SELECT 1");

                    await transaction.CommitAsync();

                    return result.IsSuccess && result.Value;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        results.All(r => r).ShouldBeTrue();
    }

    #endregion

    #region High-Volume Integration Tests

    [Test]
    public async Task VerifySignature_HighVolumeConcurrentVerifications_MaintainsPerformance()
    {
        // Arrange - Test with higher concurrency to simulate real-world load
        const int concurrentVerifications = 50;
        using var semaphore = new SemaphoreSlim(10); // Limit to 10 concurrent operations
        var tasks = new List<Task<(bool Success, TimeSpan Duration)>>();

        for (int i = 0; i < concurrentVerifications; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    using var key = Key.Create(SignatureAlgorithm.Ed25519);
                    var message = $"high volume test {taskId} - {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}";
                    var messageBytes = Encoding.UTF8.GetBytes(message);

                    var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, messageBytes);
                    var signature = Base58.Bitcoin.Encode(signatureBytes);

                    var publicKeyBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
                    var address = Base58.Bitcoin.Encode(publicKeyBytes);

                    var result = _verifier.VerifySignature("solana", address, message, signature);

                    stopwatch.Stop();
                    return (result.IsSuccess && result.Value, stopwatch.Elapsed);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        var successfulResults = results.Where(r => r.Success).ToList();
        var averageDuration = successfulResults.Average(r => r.Duration.TotalMilliseconds);
        var maxDuration = successfulResults.Max(r => r.Duration.TotalMilliseconds);

        successfulResults.Count.ShouldBe(concurrentVerifications);
        averageDuration.ShouldBeLessThan(100); // Average should be under 100ms
        maxDuration.ShouldBeLessThan(500); // Max should be under 500ms
    }

    #endregion

    #region Authentication Flow Integration Tests

    [Test]
    public void VerifySignature_MultipleChainValidation_HandlesSequentialCalls()
    {
        // Arrange - Test calling the verifier multiple times in sequence
        var results = new List<bool>();

        for (int i = 0; i < 5; i++)
        {
            using var key = Key.Create(SignatureAlgorithm.Ed25519);
            var message = $"sequential test {i}";
            var messageBytes = Encoding.UTF8.GetBytes(message);

            var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, messageBytes);
            var signature = Base58.Bitcoin.Encode(signatureBytes);

            var publicKeyBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
            var address = Base58.Bitcoin.Encode(publicKeyBytes);

            // Act
            var result = _verifier.VerifySignature("solana", address, message, signature);

            // Collect results
            results.Add(result.IsSuccess && result.Value);
        }

        // Assert
        results.All(r => r).ShouldBeTrue();
    }

    [Test]
    public void VerifySignature_MixedValidAndInvalidSignatures_ReturnsCorrectResults()
    {
        // Arrange - Test with a mix of valid and invalid signatures
        var testCases = new List<(string Name, bool ExpectedValid, Func<string> GetSignature)>();

        using var validKey = Key.Create(SignatureAlgorithm.Ed25519);
        var message = "mixed validation test";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var validSignatureBytes = SignatureAlgorithm.Ed25519.Sign(validKey, messageBytes);
        var publicKeyBytes = validKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var address = Base58.Bitcoin.Encode(publicKeyBytes);

        testCases.Add(("Valid Base58", true, () => Base58.Bitcoin.Encode(validSignatureBytes)));
        testCases.Add(("Valid Base64", true, () => Convert.ToBase64String(validSignatureBytes)));
        testCases.Add(("Valid Base64Url", true, () => Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(validSignatureBytes)));

        testCases.Add(("Invalid - Corrupted", false, () => {
            var corrupted = (byte[])validSignatureBytes.Clone();
            corrupted[0] ^= 0xFF;
            return Base58.Bitcoin.Encode(corrupted);
        }));

        testCases.Add(("Invalid - Wrong Length", false, () => Base58.Bitcoin.Encode(new byte[32])));
        testCases.Add(("Invalid - Bad Encoding", false, () => "invalid!@#$signature"));

        // Act & Assert
        foreach (var testCase in testCases)
        {
            var signature = testCase.GetSignature();
            var result = _verifier.VerifySignature("solana", address, message, signature);

            if (testCase.ExpectedValid)
            {
                result.IsSuccess.ShouldBeTrue($"Test case '{testCase.Name}' should succeed");
                result.Value.ShouldBeTrue($"Test case '{testCase.Name}' should return true");
            }
            else
            {
                result.IsFailure.ShouldBeTrue($"Test case '{testCase.Name}' should fail");
            }
        }
    }

    #endregion

    #region Error Handling Integration Tests

    [Test]
    public void VerifySignature_ServiceExceptionHandling_ReturnsGracefulErrors()
    {
        // Arrange - Test with various error conditions
        var errorTestCases = new[]
        {
            ("Empty chain", "", "address", "message", "signature", ErrorType.Validation),
            ("Unsupported chain", "ethereum", "address", "message", "signature", ErrorType.NotFound),
            ("Empty address", "solana", "", "message", "signature", ErrorType.Validation),
            ("Empty message", "solana", "address", "", "signature", ErrorType.Validation),
            ("Empty signature", "solana", "address", "message", "", ErrorType.Validation)
        };

        // Act & Assert
        foreach (var (name, chainId, address, message, signature, expectedErrorType) in errorTestCases)
        {
            var result = _verifier.VerifySignature(chainId, address, message, signature);

            result.IsFailure.ShouldBeTrue($"Test case '{name}' should fail");
            result.Error.Type.ShouldBe(expectedErrorType, $"Test case '{name}' should have error type {expectedErrorType}");
        }
    }

    #endregion

    #region Resource Management Tests

    [Test]
    public void VerifySignature_RepeatedDisposalAndCreation_DoesNotLeakMemory()
    {
        // Arrange - Test multiple service provider creation/disposal cycles
        var initialMemory = GC.GetTotalMemory(true);

        for (int cycle = 0; cycle < 10; cycle++)
        {
            using var tempServiceProvider = CreateTemporaryServiceProvider(ConnectionString);
            var tempVerifier = tempServiceProvider.GetRequiredService<IWalletSignatureVerifier>();

            // Perform some verifications
            for (int i = 0; i < 5; i++)
            {
                using var key = Key.Create(SignatureAlgorithm.Ed25519);
                var message = $"memory test {cycle}-{i}";
                var messageBytes = Encoding.UTF8.GetBytes(message);

                var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, messageBytes);
                var signature = Base58.Bitcoin.Encode(signatureBytes);

                var publicKeyBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
                var address = Base58.Bitcoin.Encode(publicKeyBytes);

                var result = tempVerifier.VerifySignature("solana", address, message, signature);
                result.IsSuccess.ShouldBeTrue();
            }
        }

        // Force garbage collection and check memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert - Memory increase should be reasonable (less than 10MB)
        memoryIncrease.ShouldBeLessThan(10 * 1024 * 1024,
            $"Memory increased by {memoryIncrease:N0} bytes, which may indicate a memory leak");
    }

    private static ServiceProvider CreateTemporaryServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDbContext<IdentityWriteDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(IdentityWriteDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            });
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();
        services.AddIdentityInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    #endregion
}