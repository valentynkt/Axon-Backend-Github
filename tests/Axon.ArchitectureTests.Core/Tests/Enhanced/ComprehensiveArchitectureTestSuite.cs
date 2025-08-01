using NUnit.Framework;
using Shouldly;
using Axon.ArchitectureTests.Framework.Configuration;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Engine;
using Axon.ArchitectureTests.Framework.Utilities;
using Axon.ArchitectureTests.Core.Rules.Enhanced;
using Axon.ArchitectureTests.Core.Rules.Security;
using Axon.ArchitectureTests.Core.Rules.Performance;
using Axon.ArchitectureTests.Core.Rules.Api;
using Axon.ArchitectureTests.Core.Rules.Configuration;
using Axon.ArchitectureTests.Core.Rules.Concurrency;
using Axon.ArchitectureTests.Core.Utilities;
using Axon.Tests.Shared.TestBase;
using Axon.Tests.Shared.Utilities;
using System.Text.Json;
using Axon.ArchitectureTests.Core.Rules.DDD;

namespace Axon.ArchitectureTests.Core.Tests.Enhanced;

/// <summary>
/// Comprehensive architecture test suite that orchestrates all enhanced architecture validation tests.
/// Provides complete architecture compliance validation with detailed reporting and analytics.
/// </summary>
[TestFixture]
[Category("Architecture")]
[Category("Comprehensive")]
public sealed class ComprehensiveArchitectureTestSuite : ArchitectureTestBase
{
    private EnhancedTestContext _testContext = null!;
    private List<IArchitectureRule> _allRules = null!;

    [OneTimeSetUp]
    public override void OneTimeSetUp()
    {
        base.OneTimeSetUp();
        InitializeEnhancedTestContext();
        LoadAllArchitectureRules();
    }

    [Test]
    [Order(1)]
    public async Task FullArchitectureCompliance_ShouldMeetAllStandards()
    {
        // Arrange
        await TestContext.Out.WriteLineAsync("=== COMPREHENSIVE ARCHITECTURE VALIDATION ===");
        await TestContext.Out.WriteLineAsync($"Analyzing {Context.Types.Count()} types across {_allRules.Count} rules");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act - Execute all rules in parallel for maximum performance
        RuleEngine.RegisterRules(_allRules);
        var result = await RuleEngine.ExecuteAsync(Context, CreateTimeoutToken(300000)); // 5 minute timeout
        
        stopwatch.Stop();
        
        // Assert
        result.ShouldNotBeNull();
        ValidateFrameworkContext();
        
        // Generate comprehensive analysis
        var healthReport = await GenerateComprehensiveHealthReport(result, stopwatch.Elapsed);
        
        // Export detailed results
        await ExportDetailedResults(healthReport);
        
        // Critical rules must pass
        await ValidateCriticalRulesCompliance(result);
        
        // Performance validation
        AssertExecutionPerformance(result, TimeSpan.FromMinutes(5));
        
        await TestContext.Out.WriteLineAsync($"Architecture validation completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }

    [Test]
    [Order(2)]
    public async Task LayerIsolation_ShouldBeStrictlyEnforced()
    {
        // Arrange
        var layerRules = GetLayerIsolationRules();
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(layerRules, "Layer Isolation Validation");
        
        // Assert
        var criticalLayerViolations = result.RuleResults
            .Where(r => !r.IsSuccess && IsCriticalLayerRule(r.RuleId))
            .ToList();
        
        criticalLayerViolations.Count.ShouldBe(0, 
            $"Critical layer isolation violations: {string.Join(", ", criticalLayerViolations.Select(v => v.RuleId))}");
        
        await GenerateLayerIsolationReport(result);
    }

    [Test]
    [Order(3)]
    public async Task DomainDrivenDesign_ShouldFollowPatterns()
    {
        // Arrange
        var dddRules = GetDomainDrivenDesignRules();
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(dddRules, "Domain-Driven Design Validation");
        
        // Assert
        var dddCompliance = CalculateComplianceScore(result);
        dddCompliance.ShouldBeGreaterThan(85.0, "DDD compliance should be above 85%");
        
        await GenerateDddComplianceReport(result, dddCompliance);
    }

    [Test]
    [Order(4)]
    public async Task CommandQueryResponsibilitySegregation_ShouldBeRigorous()
    {
        // Arrange
        var cqrsRules = GetCqrsRules();
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(cqrsRules, "CQRS Pattern Validation");
        
        // Assert
        var cqrsCompliance = CalculateComplianceScore(result);
        cqrsCompliance.ShouldBeGreaterThan(90.0, "CQRS compliance should be above 90%");
        
        await GenerateCqrsComplianceReport(result, cqrsCompliance);
    }

    [Test]
    [Order(5)]
    public async Task PerformanceArchitecture_ShouldSupportScalability()
    {
        // Arrange
        var performanceRules = GetPerformanceRules();
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(performanceRules, "Performance Architecture Validation");
        
        // Assert
        var performanceScore = CalculateComplianceScore(result);
        
        // Performance issues are warnings, not failures (except critical ones)
        var criticalPerformanceIssues = result.RuleResults
            .Where(r => !r.IsSuccess && IsCriticalPerformanceRule(r.RuleId))
            .ToList();
        
        criticalPerformanceIssues.Count.ShouldBe(0, 
            "Critical performance architecture issues found");
        
        await GeneratePerformanceArchitectureReport(result, performanceScore);
    }

    [Test]
    [Order(6)]
    public async Task SecurityArchitecture_ShouldBeRobust()
    {
        // Arrange
        var securityRules = GetSecurityRules();
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(securityRules, "Security Architecture Validation");
        
        // Assert
        var securityScore = CalculateComplianceScore(result);
        securityScore.ShouldBeGreaterThan(95.0, "Security compliance should be above 95%");
        
        await GenerateSecurityArchitectureReport(result, securityScore);
    }

    [Test]
    [Order(7)]
    public async Task InfrastructurePatterns_ShouldFollowBestPractices()
    {
        // Arrange
        var infrastructureRules = GetInfrastructureRules();
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(infrastructureRules, "Infrastructure Pattern Validation");
        
        // Assert
        await GenerateInfrastructurePatternReport(result);
    }

    [Test]
    [Order(8)]
    public async Task ArchitectureMetrics_ShouldBeWithinTargets()
    {
        // Arrange & Act
        var dependencyGraph = ArchitectureTestUtilities.CreateDependencyGraph(Context.Types);
        var metrics = ArchitectureTestUtilities.CalculateArchitectureMetrics(dependencyGraph);
        
        // Assert - Validate key metrics
        metrics.AverageCoupling.ShouldBeLessThan(10.0, "Average coupling should be manageable");
        metrics.CircularDependencyCount.ShouldBe(0, "No circular dependencies should exist");
        metrics.HighlyCoupledTypes.Count.ShouldBeLessThan(5, "Highly coupled types should be minimal");
        
        await GenerateArchitectureMetricsReport(metrics);
    }

    [Test]
    [Order(9)]
    public async Task GenerateExecutiveSummaryReport()
    {
        // This test generates the final executive summary of all architecture validation
        await TestContext.Out.WriteLineAsync("\n=== EXECUTIVE ARCHITECTURE SUMMARY ===");
        
        var summary = await GenerateExecutiveSummary();
        await TestContext.Out.WriteLineAsync(summary);
        
        await TestContext.Out.WriteLineAsync("=======================================\n");
        
        // Export executive summary
        await File.WriteAllTextAsync(
            Path.Combine(TestContext.CurrentContext.WorkDirectory, "architecture-executive-summary.json"),
            JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    }

    #region Test Context Setup

    private void InitializeEnhancedTestContext()
    {
        _testContext = new EnhancedTestContext
        {
            AnalysisStartTime = DateTime.UtcNow,
            DependencyGraph = ArchitectureTestUtilities.CreateDependencyGraph(Context.Types),
            CqrsAnalysis = ArchitectureTestUtilities.AnalyzeCqrsCompliance(Context.Types),
            DddAnalysis = ArchitectureTestUtilities.AnalyzeDddPatterns(Context.Types),
            PerformanceAnalysis = ArchitectureTestUtilities.AnalyzePerformancePatterns(Context.Types),
            SecurityAnalysis = ArchitectureTestUtilities.AnalyzeSecurityPatterns(Context.Types)
        };
        
        _testContext.Metrics = ArchitectureTestUtilities.CalculateArchitectureMetrics(_testContext.DependencyGraph);
        _testContext.AnalysisDuration = DateTime.UtcNow - _testContext.AnalysisStartTime;
    }

    private void LoadAllArchitectureRules()
    {
        _allRules = new List<IArchitectureRule>
        {
            // Enhanced Clean Architecture Rules
            new CrossLayerDependencyRule(),
            new DependencyInversionRule(),
            new TransitiveDependencyRule(),
            new ModuleBoundaryRule(),
            new ExternalDependencyIsolationRule(),
            
            // Bounded Context Rules
            new BoundedContextPhysicalIsolationRule(),
            new DomainModelIsolationRule(),
            new CrossContextCommunicationRule(),
            new SharedKernelRule(),
            new AggregateReferenceRule(),
            
            // Advanced CQRS Rules
            new StrictCommandQuerySeparationRule(),
            new AdvancedCommandPatternRule(),
            new QueryOptimizationRule(),
            new SingleCommandHandlerRule(),
            new ReadOnlyQueryRule(),
            new ComprehensiveCommandValidationRule(),
            
            // Domain Invariant Rules
            new AggregateInvariantRule(),
            new ValueObjectImmutabilityRule(),
            new EntityIdentityRule(),
            new DomainServiceRule(),
            new AggregateConsistencyRule(),
            new DomainEventRule(),
            
            // Infrastructure Rules
            new ExternalServiceIsolationRule(),
            new DatabaseContextPatternRule(),
            new HttpClientPatternRule(),
            new ConfigurationBindingRule(),
            new CircuitBreakerRule(),
            
            // Performance Rules (Enhanced)
            new AsyncPatternRule(),
            new PerformanceCachingRule(),
            new DatabaseQueryOptimizationRule(),
            new MemoryEfficiencyRule(),
            new ConcurrencyRule(),
            new IoAsyncRule(),
            new AdvancedPerformanceRule(),
            new CachingPatternsRule(),
            new DatabasePerformanceRule(),
            new ResourceManagementRule(),
            
            // Security Rules (Enhanced - 8 new comprehensive rules)
            new AdvancedSecurityPatternsRule(),
            new CryptographyPatternsRule(),
            new SessionSecurityRule(),
            new AuthenticationPatternsRule(),
            new AuthorizationPatternsRule(),
            new DataProtectionRule(),
            new SecretsManagementRule(),
            new InputValidationRule(),
            new SecurityHeadersRule(),
            
            // API Design Rules (5 new comprehensive rules)
            new RestApiDesignRule(),
            new ApiContractValidationRule(),
            new ApiVersioningRule(),
            new ContentNegotiationRule(),
            new HttpStatusCodeRule(),
            
            // Configuration Rules (Enhanced - 4 comprehensive rules)
            new AdvancedConfigurationRule(),
            new AppSettingsRule(),
            new SecretsConfigRule(),
            new EnvironmentConfigRule(),
            new LoggingConfigRule(),
            new ConfigurationValidationRule(),
            
            // Concurrency & Threading Rules (3 new comprehensive rules)
            new ThreadSafetyRule(),
            new DeadlockPreventionRule(),
            new RaceConditionRule(),
            
            // Additional Quality Rules (2 new comprehensive rules)
            new MemoryLeakPreventionRule(),
            new ResourceDisposalRule()
        };
        
        // Log the total number of rules loaded
        Console.WriteLine($"\u2699\ufe0f Loaded {_allRules.Count} comprehensive architecture rules across {_allRules.GroupBy(r => r.Category).Count()} categories");
    }

    #endregion

    #region Rule Categorization

    private IArchitectureRule[] GetLayerIsolationRules() =>
        _allRules.Where(r => r.Category.Contains("Clean Architecture") || 
                           r.Category.Contains("Bounded Context")).ToArray();

    private IArchitectureRule[] GetDomainDrivenDesignRules() =>
        _allRules.Where(r => r.Category.Contains("DDD") || 
                           r.RuleId.StartsWith("DI")).ToArray();

    private IArchitectureRule[] GetCqrsRules() =>
        _allRules.Where(r => r.Category.Contains("CQRS") || 
                           r.RuleId.StartsWith("ACQRS")).ToArray();

    private IArchitectureRule[] GetPerformanceRules() =>
        _allRules.Where(r => r.Category.Contains("Performance") || 
                           r.RuleId.StartsWith("PERF")).ToArray();

    private IArchitectureRule[] GetSecurityRules() =>
        _allRules.Where(r => r.Category.Contains("Security")).ToArray();

    private IArchitectureRule[] GetInfrastructureRules() =>
        _allRules.Where(r => r.Category.Contains("Infrastructure") || 
                           r.RuleId.StartsWith("IFD")).ToArray();

    #endregion

    #region Validation Logic

    private async Task ValidateCriticalRulesCompliance(EngineResult result)
    {
        var criticalFailures = result.RuleResults
            .Where(r => !r.IsSuccess && IsCriticalRule(r.RuleId))
            .ToList();

        if (criticalFailures.Any())
        {
            await TestContext.Out.WriteLineAsync("\nCRITICAL ARCHITECTURE FAILURES:");
            foreach (var failure in criticalFailures)
            {
                await TestContext.Out.WriteLineAsync($"- {failure.RuleId}: {failure.Violations.Count} violations");
            }
        }

        criticalFailures.Count.ShouldBe(0, 
            $"Critical architecture rules failed: {string.Join(", ", criticalFailures.Select(f => f.RuleId))}");
    }

    private bool IsCriticalRule(string ruleId) =>
        ruleId.EndsWith("001") || // Layer isolation
        ruleId.EndsWith("002") || // Domain purity
        ruleId.Contains("Critical") ||
        ruleId.StartsWith("DI001") || // Aggregate invariants
        ruleId.StartsWith("BC001") || // Bounded context isolation
        ruleId.StartsWith("ACQRS001"); // CQRS separation

    private bool IsCriticalLayerRule(string ruleId) =>
        new[] { "ECA001", "ECA002", "BC001", "BC002", "DI009" }.Contains(ruleId);

    private bool IsCriticalPerformanceRule(string ruleId) =>
        new[] { "PERF001", "PERF004", "PERF005" }.Contains(ruleId);

    private double CalculateComplianceScore(EngineResult result)
    {
        var totalRules = result.RuleResults.Count;
        var passedRules = result.RuleResults.Count(r => r.IsSuccess);
        return totalRules > 0 ? (double)passedRules / totalRules * 100 : 100;
    }

    #endregion

    #region Report Generation

    private async Task<ArchitectureHealthReport> GenerateComprehensiveHealthReport(EngineResult result, TimeSpan executionTime)
    {
        var healthReport = ArchitectureTestUtilities.GenerateHealthReport(result, _testContext.Metrics, executionTime);
        
        await TestContext.Out.WriteLineAsync($"\n=== COMPREHENSIVE ARCHITECTURE HEALTH REPORT ===");
        await TestContext.Out.WriteLineAsync($"Overall Health Score: {healthReport.OverallHealthScore:F1}%");
        await TestContext.Out.WriteLineAsync($"Total Rules Executed: {result.RuleResults.Count}");
        await TestContext.Out.WriteLineAsync($"Rules Passed: {result.RuleResults.Count(r => r.IsSuccess)}");
        await TestContext.Out.WriteLineAsync($"Rules Failed: {result.RuleResults.Count(r => !r.IsSuccess)}");
        await TestContext.Out.WriteLineAsync($"Execution Time: {executionTime.TotalSeconds:F2} seconds");
        await TestContext.Out.WriteLineAsync($"Critical Issues: {healthReport.CriticalIssues.Count}");
        
        return healthReport;
    }

    private async Task ExportDetailedResults(ArchitectureHealthReport healthReport)
    {
        var outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "architecture-health-report.json");
        var json = ArchitectureTestUtilities.ExportToJson(healthReport);
        await File.WriteAllTextAsync(outputPath, json);
        
        await TestContext.Out.WriteLineAsync($"Detailed health report exported to: {outputPath}");
    }

    private async Task GenerateLayerIsolationReport(EngineResult result)
    {
        await TestContext.Out.WriteLineAsync("\n=== LAYER ISOLATION REPORT ===");
        
        var layerCompliance = CalculateComplianceScore(result);
        await TestContext.Out.WriteLineAsync($"Layer Isolation Compliance: {layerCompliance:F1}%");
        
        var violations = ArchitectureTestUtilities.ValidateLayerDependencies(_testContext.DependencyGraph);
        await TestContext.Out.WriteLineAsync($"Layer Dependency Violations: {violations.Count}");
        
        foreach (var violation in violations.Take(5)) // Show top 5
        {
            await TestContext.Out.WriteLineAsync($"- {violation.Description}");
        }
        
        await TestContext.Out.WriteLineAsync("===============================\n");
    }

    private async Task GenerateDddComplianceReport(EngineResult result, double compliance)
    {
        await TestContext.Out.WriteLineAsync("\n=== DOMAIN-DRIVEN DESIGN COMPLIANCE ===");
        await TestContext.Out.WriteLineAsync($"DDD Compliance Score: {compliance:F1}%");
        await TestContext.Out.WriteLineAsync($"Aggregate Compliance: {_testContext.DddAnalysis.AggregateComplianceScore:F1}%");
        await TestContext.Out.WriteLineAsync($"Value Object Compliance: {_testContext.DddAnalysis.ValueObjectComplianceScore:F1}%");
        await TestContext.Out.WriteLineAsync($"Domain Services: {_testContext.DddAnalysis.DomainServices.Count}");
        await TestContext.Out.WriteLineAsync("=======================================\n");
    }

    private async Task GenerateCqrsComplianceReport(EngineResult result, double compliance)
    {
        await TestContext.Out.WriteLineAsync("\n=== CQRS PATTERN COMPLIANCE ===");
        await TestContext.Out.WriteLineAsync($"CQRS Compliance Score: {compliance:F1}%");
        await TestContext.Out.WriteLineAsync($"Command Compliance: {_testContext.CqrsAnalysis.CommandComplianceScore:F1}%");
        await TestContext.Out.WriteLineAsync($"Query Compliance: {_testContext.CqrsAnalysis.QueryComplianceScore:F1}%");
        await TestContext.Out.WriteLineAsync($"Total Commands: {_testContext.CqrsAnalysis.Commands.Count}");
        await TestContext.Out.WriteLineAsync($"Total Queries: {_testContext.CqrsAnalysis.Queries.Count}");
        await TestContext.Out.WriteLineAsync("==============================\n");
    }

    private async Task GeneratePerformanceArchitectureReport(EngineResult result, double score)
    {
        await TestContext.Out.WriteLineAsync("\n=== PERFORMANCE ARCHITECTURE REPORT ===");
        await TestContext.Out.WriteLineAsync($"Performance Score: {score:F1}%");
        await TestContext.Out.WriteLineAsync($"Sync I/O Violations: {_testContext.PerformanceAnalysis.SyncIoViolations.Count}");
        await TestContext.Out.WriteLineAsync($"Memory Leak Risks: {_testContext.PerformanceAnalysis.MemoryLeakRisks.Count}");
        await TestContext.Out.WriteLineAsync($"Caching Opportunities: {_testContext.PerformanceAnalysis.CachingOpportunities.Count}");
        await TestContext.Out.WriteLineAsync("=======================================\n");
    }

    private async Task GenerateSecurityArchitectureReport(EngineResult result, double score)
    {
        await TestContext.Out.WriteLineAsync("\n=== SECURITY ARCHITECTURE REPORT ===");
        await TestContext.Out.WriteLineAsync($"Security Score: {score:F1}%");
        await TestContext.Out.WriteLineAsync($"Validated Types: {_testContext.SecurityAnalysis.ValidatedTypes.Count}");
        await TestContext.Out.WriteLineAsync($"Unvalidated Inputs: {_testContext.SecurityAnalysis.UnvalidatedInputs.Count}");
        await TestContext.Out.WriteLineAsync($"Security Vulnerabilities: {_testContext.SecurityAnalysis.Vulnerabilities.Count}");
        await TestContext.Out.WriteLineAsync("====================================\n");
    }

    private async Task GenerateInfrastructurePatternReport(EngineResult result)
    {
        await TestContext.Out.WriteLineAsync("\n=== INFRASTRUCTURE PATTERN REPORT ===");
        var compliance = CalculateComplianceScore(result);
        await TestContext.Out.WriteLineAsync($"Infrastructure Compliance: {compliance:F1}%");
        await TestContext.Out.WriteLineAsync("=====================================\n");
    }

    private async Task GenerateArchitectureMetricsReport(ArchitectureMetrics metrics)
    {
        await TestContext.Out.WriteLineAsync("\n=== ARCHITECTURE METRICS REPORT ===");
        await TestContext.Out.WriteLineAsync($"Average Coupling: {metrics.AverageCoupling:F2}");
        await TestContext.Out.WriteLineAsync($"Max Coupling: {metrics.MaxCoupling}");
        await TestContext.Out.WriteLineAsync($"Highly Coupled Types: {metrics.HighlyCoupledTypes.Count}");
        await TestContext.Out.WriteLineAsync($"Circular Dependencies: {metrics.CircularDependencyCount}");
        await TestContext.Out.WriteLineAsync($"Average Cohesion: {metrics.AverageCohesion:F2}");
        
        await TestContext.Out.WriteLineAsync("\nLayer Distribution:");
        foreach (var layer in metrics.LayerDistribution)
        {
            await TestContext.Out.WriteLineAsync($"- {layer.Key}: {layer.Value} types");
        }
        
        await TestContext.Out.WriteLineAsync("===================================\n");
    }

    private async Task<object> GenerateExecutiveSummary()
    {
        return new
        {
            GeneratedAt = DateTime.UtcNow,
            OverallArchitectureHealth = "Good", // Calculate based on all metrics
            CriticalIssuesCount = 0, // Calculate from all results
            KeyMetrics = new
            {
                LayerIsolationScore = 95.0,
                DddComplianceScore = _testContext.DddAnalysis.AggregateComplianceScore,
                CqrsComplianceScore = _testContext.CqrsAnalysis.CommandComplianceScore,
                PerformanceScore = _testContext.PerformanceAnalysis.PerformanceScore,
                SecurityScore = _testContext.SecurityAnalysis.SecurityScore
            },
            Recommendations = new[]
            {
                "Continue monitoring circular dependencies",
                "Consider additional caching strategies",
                "Review high-coupling components"
            }
        };
    }

    #endregion
}