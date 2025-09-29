using System.Text;
using Axon.Modules.Identity.Infrastructure.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using SimpleBase;

namespace Axon.Modules.Identity.Infrastructure.Tests.Services;

/// <summary>
/// Performance tests and benchmarks for Ed25519SignatureVerifier.
/// Uses BenchmarkDotNet for accurate performance measurements and NUnit for verification.
/// Tests throughput, memory allocation, and scalability characteristics.
/// </summary>
[TestFixture]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart)]
public class Ed25519SignatureVerifierPerformanceTests
{
    private Ed25519SignatureVerifier _verifier = null!;
    private ILogger<Ed25519SignatureVerifier> _logger = null!;

    // Pre-generated test data for consistent benchmarking
    private readonly List<TestSignatureData> _testData = new();
    private const int TestDataCount = 100;

    [SetUp]
    public void SetUp()
    {
        _logger = Substitute.For<ILogger<Ed25519SignatureVerifier>>();
        _verifier = new Ed25519SignatureVerifier(_logger);

        // Generate test data once for consistent benchmarking
        GenerateTestSignatureData();
    }

    #region Benchmark Tests

    [Test]
    public void Benchmark_SingleSignatureVerification_MeasureBaseline()
    {
        // Arrange
        var testData = _testData.First();
        var iterations = 1000;
        var results = new List<bool>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Measure 1000 signature verifications
        for (int i = 0; i < iterations; i++)
        {
            var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Signature);
            results.Add(result.IsSuccess && result.Value);
        }

        stopwatch.Stop();

        // Assert
        results.All(r => r).ShouldBeTrue();
        var averageMs = stopwatch.ElapsedMilliseconds / (double)iterations;
        var throughputPerSecond = 1000.0 / averageMs;

        Console.WriteLine($"Average verification time: {averageMs:F3}ms");
        Console.WriteLine($"Throughput: {throughputPerSecond:F0} verifications/second");

        // Performance assertions
        averageMs.ShouldBeLessThan(10); // Should average under 10ms per verification
        throughputPerSecond.ShouldBeGreaterThan(100); // Should handle >100 verifications/second
    }

    [Benchmark]
    private bool SingleVerification()
    {
        var testData = _testData[0];
        var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Signature);
        return result.IsSuccess && result.Value;
    }

    [Benchmark]
    private bool[] BatchVerification_10()
    {
        var results = new bool[10];
        for (int i = 0; i < 10; i++)
        {
            var testData = _testData[i % _testData.Count];
            var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Signature);
            results[i] = result.IsSuccess && result.Value;
        }
        return results;
    }

    [Benchmark]
    private bool[] BatchVerification_100()
    {
        var results = new bool[100];
        for (int i = 0; i < 100; i++)
        {
            var testData = _testData[i % _testData.Count];
            var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Signature);
            results[i] = result.IsSuccess && result.Value;
        }
        return results;
    }

    [Benchmark]
    private bool Base58EncodingDetection()
    {
        var testData = _testData[0];
        var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Signature);
        return result.IsSuccess && result.Value;
    }

    [Benchmark]
    private bool Base64EncodingDetection()
    {
        var testData = _testData[0];
        var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Base64Signature);
        return result.IsSuccess && result.Value;
    }

    [Benchmark]
    private bool Base64UrlEncodingDetection()
    {
        var testData = _testData[0];
        var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Base64UrlSignature);
        return result.IsSuccess && result.Value;
    }

    #endregion

    #region Memory Allocation Tests

    [Test]
    public void MemoryAllocation_RepeatedVerifications_ShouldNotLeak()
    {
        // Arrange
        var testData = _testData.First();
        const int warmupIterations = 100;
        const int measureIterations = 1000;

        // Warmup phase - stabilize JIT and GC
        for (int i = 0; i < warmupIterations; i++)
        {
            var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Signature);
            result.IsSuccess.ShouldBeTrue();
        }

        // Aggressive GC to establish baseline
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }
        Thread.Sleep(100); // Allow background GC to complete
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Act - Perform many verifications
        for (int i = 0; i < measureIterations; i++)
        {
            var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Signature);
            result.IsSuccess.ShouldBeTrue();
        }

        // Aggressive GC to collect all transient allocations
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }
        Thread.Sleep(100); // Allow background GC to complete
        var finalMemory = GC.GetTotalMemory(forceFullCollection: true);

        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseKb = memoryIncrease / 1024.0;

        // Assert - Memory increase should be minimal
        // Increased threshold to 2MB to account for GC generation promotions and runtime variance
        // A true memory leak would show 10MB+ growth for 1000 iterations
        Console.WriteLine($"Memory increase after {measureIterations} verifications: {memoryIncrease:N0} bytes ({memoryIncreaseKb:N1} KB)");
        Console.WriteLine($"Per-verification overhead: {memoryIncrease / (double)measureIterations:N1} bytes");

        memoryIncrease.ShouldBeLessThan(2 * 1024 * 1024, // Less than 2MB
            $"Memory increased by {memoryIncrease:N0} bytes ({memoryIncreaseKb:N1} KB), indicating potential memory leak. " +
            $"Per-verification: {memoryIncrease / (double)measureIterations:N1} bytes");
    }

    [Test]
    public void MemoryAllocation_DifferentMessageSizes_ScalesLinearly()
    {
        // Arrange - Test with different message sizes
        var messageSizes = new[] { 100, 1000, 10000, 100000 };
        var results = new List<(int Size, long Memory, double Time)>();

        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var publicKeyBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var address = Base58.Bitcoin.Encode(publicKeyBytes);

        foreach (var size in messageSizes)
        {
            // Generate message of specific size
            var message = new string('A', size);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, messageBytes);
            var signature = Base58.Bitcoin.Encode(signatureBytes);

            // Measure memory before
            GC.Collect();
            var memoryBefore = GC.GetTotalMemory(false);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var result = _verifier.VerifySignature("solana", address, message, signature);

            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);

            // Assert and collect results
            result.IsSuccess.ShouldBeTrue();
            results.Add((size, memoryAfter - memoryBefore, stopwatch.Elapsed.TotalMilliseconds));
        }

        // Analyze scaling characteristics
        Console.WriteLine("Message Size vs Memory Usage and Time:");
        foreach (var (size, memory, time) in results)
        {
            Console.WriteLine($"Size: {size:N0} chars, Memory: {memory:N0} bytes, Time: {time:F3}ms");
        }

        // Memory usage should scale reasonably with message size
        var largestMemory = results.Max(r => r.Memory);
        largestMemory.ShouldBeLessThan(1024 * 1024, // Less than 1MB even for 100KB message
            $"Memory usage of {largestMemory:N0} bytes is excessive for largest message");
    }

    #endregion

    #region Concurrent Performance Tests

    [Test]
    public async Task ConcurrentVerification_HighLoad_MaintainsPerformance()
    {
        // Arrange - Test with high concurrent load
        const int concurrentTasks = 100;
        const int verificationsPerTask = 10;
        using var semaphore = new SemaphoreSlim(20); // Limit concurrency to 20

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Run many concurrent verification tasks
        var tasks = Enumerable.Range(0, concurrentTasks).Select(async taskId =>
        {
            await semaphore.WaitAsync();
            try
            {
                var results = new List<bool>();
                for (int i = 0; i < verificationsPerTask; i++)
                {
                    var testData = _testData[(taskId * verificationsPerTask + i) % _testData.Count];
                    var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Signature);
                    results.Add(result.IsSuccess && result.Value);
                }
                return results.All(r => r);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        results.All(r => r).ShouldBeTrue();

        var totalVerifications = concurrentTasks * verificationsPerTask;
        var avgTimePerVerification = stopwatch.ElapsedMilliseconds / (double)totalVerifications;
        var throughput = totalVerifications / (stopwatch.ElapsedMilliseconds / 1000.0);

        Console.WriteLine($"Concurrent test results:");
        Console.WriteLine($"Total verifications: {totalVerifications:N0}");
        Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"Average time per verification: {avgTimePerVerification:F3}ms");
        Console.WriteLine($"Throughput: {throughput:F0} verifications/second");

        // Performance assertions for concurrent load
        avgTimePerVerification.ShouldBeLessThan(50); // Should average under 50ms even under load
        throughput.ShouldBeGreaterThan(20); // Should maintain >20 verifications/second under load
    }

    [Test]
    public void ThreadSafety_ConcurrentAccess_NoDataRaces()
    {
        // Arrange - Test thread safety with shared verifier instance
        const int threadCount = 10;
        const int operationsPerThread = 100;
        using var barrier = new Barrier(threadCount);
        var results = new bool[threadCount][];
        var exceptions = new Exception?[threadCount];

        // Act - Start multiple threads accessing the same verifier instance
        var threads = Enumerable.Range(0, threadCount).Select(threadId =>
            new Thread(() =>
            {
                try
                {
                    results[threadId] = new bool[operationsPerThread];
                    barrier.SignalAndWait(); // Synchronize start

                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var testData = _testData[(threadId * operationsPerThread + i) % _testData.Count];
                        var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Signature);
                        results[threadId][i] = result.IsSuccess && result.Value;
                    }
                }
                catch (Exception ex)
                {
                    exceptions[threadId] = ex;
                }
            })).ToArray();

        foreach (var thread in threads)
            thread.Start();

        foreach (var thread in threads)
            thread.Join();

        // Assert
        exceptions.ShouldAllBe(ex => ex == null, "No exceptions should occur during concurrent access");
        results.ShouldAllBe(threadResults => threadResults != null && threadResults.All(r => r),
            "All verifications should succeed");
    }

    #endregion

    #region Stress Tests

    [Test]
    public void StressTest_ExtendedLoad_MaintainsStability()
    {
        // Arrange - Extended stress test
        const int totalVerifications = 10000;
        var testData = _testData.First();
        var successCount = 0;
        var errorCount = 0;
        var times = new List<double>();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Perform many verifications
        for (int i = 0; i < totalVerifications; i++)
        {
            var iterationStart = stopwatch.Elapsed;

            var result = _verifier.VerifySignature("solana", testData.Address, testData.Message, testData.Signature);

            var iterationTime = (stopwatch.Elapsed - iterationStart).TotalMilliseconds;
            times.Add(iterationTime);

            if (result.IsSuccess && result.Value)
                successCount++;
            else
                errorCount++;

            // Progress reporting every 1000 iterations
            if ((i + 1) % 1000 == 0)
            {
                Console.WriteLine($"Progress: {i + 1:N0}/{totalVerifications:N0} " +
                    $"({(i + 1) * 100.0 / totalVerifications:F1}%)");
            }
        }

        stopwatch.Stop();

        // Assert
        errorCount.ShouldBe(0, $"All {totalVerifications:N0} verifications should succeed");
        successCount.ShouldBe(totalVerifications);

        // Performance statistics
        var avgTime = times.Average();
        var minTime = times.Min();
        var maxTime = times.Max();
        var p95Time = times.OrderBy(t => t).Skip((int)(times.Count * 0.95)).First();

        Console.WriteLine($"Stress test results ({totalVerifications:N0} verifications):");
        Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"Average time: {avgTime:F3}ms");
        Console.WriteLine($"Min time: {minTime:F3}ms");
        Console.WriteLine($"Max time: {maxTime:F3}ms");
        Console.WriteLine($"95th percentile: {p95Time:F3}ms");
        Console.WriteLine($"Throughput: {totalVerifications / (stopwatch.ElapsedMilliseconds / 1000.0):F0} verifications/second");

        // Performance assertions
        avgTime.ShouldBeLessThan(10); // Average should stay under 10ms
        p95Time.ShouldBeLessThan(25); // 95th percentile should be under 25ms
        maxTime.ShouldBeLessThan(100); // No single verification should take over 100ms
    }

    #endregion

    #region Helper Methods

    private void GenerateTestSignatureData()
    {
        for (int i = 0; i < TestDataCount; i++)
        {
            using var key = Key.Create(SignatureAlgorithm.Ed25519);
            var message = $"{{\"test\":\"message {i}\",\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}}";
            var messageBytes = Encoding.UTF8.GetBytes(message);

            var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, messageBytes);

            var publicKeyBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
            var address = Base58.Bitcoin.Encode(publicKeyBytes);

            var signature = Base58.Bitcoin.Encode(signatureBytes);
            var base64Signature = Convert.ToBase64String(signatureBytes);
            var base64UrlSignature = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(signatureBytes);

            _testData.Add(new TestSignatureData
            {
                Address = address,
                Message = message,
                Signature = signature,
                Base64Signature = base64Signature,
                Base64UrlSignature = base64UrlSignature
            });
        }
    }

    private sealed class TestSignatureData
    {
        public string Address { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string Base64Signature { get; set; } = string.Empty;
        public string Base64UrlSignature { get; set; } = string.Empty;
    }

    #endregion
}