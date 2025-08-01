using Axon.ArchitectureTests.Framework.Contracts;

namespace Axon.Tests.Shared.Utilities;

#region Core Models

/// <summary>
/// Represents a dependency graph of types and their relationships.
/// </summary>
public class DependencyGraph
{
    public Dictionary<Type, HashSet<Type>> Nodes { get; } = new();
    
    public void AddNode(Type type, HashSet<Type> dependencies)
    {
        Nodes[type] = dependencies;
    }
    
    public HashSet<Type> GetDependencies(Type type)
    {
        return Nodes.GetValueOrDefault(type, new HashSet<Type>());
    }
    
    public IEnumerable<Type> GetDependents(Type type)
    {
        return Nodes.Where(kvp => kvp.Value.Contains(type)).Select(kvp => kvp.Key);
    }
}

/// <summary>
/// Represents a circular dependency in the codebase.
/// </summary>
public class CircularDependency
{
    public List<Type> Cycle { get; set; } = new();
    public string Description => string.Join(" -> ", Cycle.Select(t => t.Name));
}

/// <summary>
/// Architectural layers as defined by Clean Architecture.
/// </summary>
public enum ArchitecturalLayer
{
    Unknown,
    Api,
    Application,
    Domain,
    Infrastructure,
    Shared
}

/// <summary>
/// Types of layer violations.
/// </summary>
public enum LayerViolationType
{
    GeneralLayerViolation,
    DomainIsolationViolation,
    DependencyInversionViolation,
    CircularDependency
}

/// <summary>
/// Represents a layer dependency violation.
/// </summary>
public class LayerViolation
{
    public Type SourceType { get; set; } = null!;
    public Type TargetType { get; set; } = null!;
    public ArchitecturalLayer SourceLayer { get; set; }
    public ArchitecturalLayer TargetLayer { get; set; }
    public LayerViolationType ViolationType { get; set; }
    public string Description => $"{SourceLayer} -> {TargetLayer}: {SourceType.Name} depends on {TargetType.Name}";
}

#endregion

#region CQRS Analysis Models

/// <summary>
/// Complete CQRS pattern analysis result.
/// </summary>
public class CqrsAnalysisResult
{
    public List<CommandAnalysis> Commands { get; } = new();
    public List<QueryAnalysis> Queries { get; } = new();
    public List<CommandHandlerAnalysis> CommandHandlers { get; } = new();
    public List<QueryHandlerAnalysis> QueryHandlers { get; } = new();
    
    public double CommandComplianceScore => 
        Commands.Count > 0 ? Commands.Count(c => c.IsCompliant) * 100.0 / Commands.Count : 100;
    
    public double QueryComplianceScore => 
        Queries.Count > 0 ? Queries.Count(q => q.IsCompliant) * 100.0 / Queries.Count : 100;
}

public class CommandAnalysis
{
    public Type Type { get; set; } = null!;
    public bool HasValidation { get; set; }
    public bool HasProperNaming { get; set; }
    public bool HasSingleHandler { get; set; }
    public bool ReturnsVoid { get; set; }
    public List<string> Issues { get; } = new();
    
    public bool IsCompliant => HasValidation && HasProperNaming && HasSingleHandler && ReturnsVoid;
}

public class QueryAnalysis
{
    public Type Type { get; set; } = null!;
    public bool HasProperNaming { get; set; }
    public bool IsReadOnly { get; set; }
    public bool HasPagination { get; set; }
    public bool UsesCaching { get; set; }
    public List<string> Issues { get; } = new();
    
    public bool IsCompliant => HasProperNaming && IsReadOnly;
}

public class CommandHandlerAnalysis
{
    public Type Type { get; set; } = null!;
    public bool HandlesOnlyOneCommand { get; set; }
    public bool UsesAsyncPattern { get; set; }
    public bool HasProperErrorHandling { get; set; }
    public List<string> Issues { get; } = new();
    
    public bool IsCompliant => HandlesOnlyOneCommand && UsesAsyncPattern;
}

public class QueryHandlerAnalysis
{
    public Type Type { get; set; } = null!;
    public bool HandlesOnlyOneQuery { get; set; }
    public bool UsesAsyncPattern { get; set; }
    public bool HasNoSideEffects { get; set; }
    public List<string> Issues { get; } = new();
    
    public bool IsCompliant => HandlesOnlyOneQuery && UsesAsyncPattern && HasNoSideEffects;
}

#endregion

#region DDD Analysis Models

/// <summary>
/// Complete DDD pattern analysis result.
/// </summary>
public class DddAnalysisResult
{
    public List<AggregateRootAnalysis> AggregateRoots { get; } = new();
    public List<EntityAnalysis> Entities { get; } = new();
    public List<ValueObjectAnalysis> ValueObjects { get; } = new();
    public List<DomainServiceAnalysis> DomainServices { get; } = new();
    
    public double AggregateComplianceScore => 
        AggregateRoots.Count > 0 ? AggregateRoots.Count(a => a.IsCompliant) * 100.0 / AggregateRoots.Count : 100;
    
    public double ValueObjectComplianceScore => 
        ValueObjects.Count > 0 ? ValueObjects.Count(v => v.IsCompliant) * 100.0 / ValueObjects.Count : 100;
}

public class AggregateRootAnalysis
{
    public Type Type { get; set; } = null!;
    public bool EnforcesInvariants { get; set; }
    public bool HasProperIdentity { get; set; }
    public bool PublishesDomainEvents { get; set; }
    public bool ControlsConsistencyBoundary { get; set; }
    public List<string> Issues { get; } = new();
    
    public bool IsCompliant => EnforcesInvariants && HasProperIdentity && ControlsConsistencyBoundary;
}

public class EntityAnalysis
{
    public Type Type { get; set; } = null!;
    public bool HasStronglyTypedId { get; set; }
    public bool HasProperEquality { get; set; }
    public bool MaintainsInvariants { get; set; }
    public List<string> Issues { get; } = new();
    
    public bool IsCompliant => HasStronglyTypedId && HasProperEquality;
}

public class ValueObjectAnalysis
{
    public Type Type { get; set; } = null!;
    public bool IsImmutable { get; set; }
    public bool HasStructuralEquality { get; set; }
    public bool ValidatesInvariants { get; set; }
    public bool HasProperFactoryMethod { get; set; }
    public List<string> Issues { get; } = new();
    
    public bool IsCompliant => IsImmutable && HasStructuralEquality && ValidatesInvariants;
}

public class DomainServiceAnalysis
{
    public Type Type { get; set; } = null!;
    public bool EncapsulatesBusinessLogic { get; set; }
    public bool IsStateless { get; set; }
    public bool HasClearPurpose { get; set; }
    public List<string> Issues { get; } = new();
    
    public bool IsCompliant => EncapsulatesBusinessLogic && IsStateless && HasClearPurpose;
}

#endregion

#region Metrics Models

/// <summary>
/// Comprehensive architecture metrics.
/// </summary>
public class ArchitectureMetrics
{
    public double AverageCoupling { get; set; }
    public int MaxCoupling { get; set; }
    public List<Type> HighlyCoupledTypes { get; set; } = new();
    public Dictionary<ArchitecturalLayer, int> LayerDistribution { get; set; } = new();
    public int CircularDependencyCount { get; set; }
    public List<CircularDependency> CircularDependencies { get; set; } = new();
    public Dictionary<ArchitecturalLayer, double> StabilityScores { get; set; } = new();
    
    // Complexity metrics
    public double AverageComplexity { get; set; }
    public List<Type> HighComplexityTypes { get; set; } = new();
    
    // Cohesion metrics
    public Dictionary<Type, double> CohesionScores { get; set; } = new();
    public double AverageCohesion { get; set; }
}

/// <summary>
/// Complete architecture health report.
/// </summary>
public class ArchitectureHealthReport
{  
    public DateTime GeneratedAt { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public double OverallHealthScore { get; set; }
    public ArchitectureMetrics Metrics { get; set; } = new();
    public Dictionary<string, double> CategoryScores { get; set; } = new();
    public List<RuleResult> CriticalIssues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    
    // Trend data (for historical comparison)
    public List<HealthTrend> Trends { get; set; } = new();
}

public class HealthTrend
{
    public DateTime Date { get; set; }
    public double HealthScore { get; set; }
    public int TotalViolations { get; set; }
    public int CriticalIssues { get; set; }
}

#endregion

#region Performance Analysis Models

/// <summary>
/// Performance pattern analysis result.
/// </summary>
public class PerformanceAnalysisResult
{
    public List<SyncIoViolation> SyncIoViolations { get; } = new();
    public List<Type> MemoryLeakRisks { get; } = new();
    public Dictionary<Type, List<string>> CachingOpportunities { get; } = new();
    public List<ConcurrencyIssue> ConcurrencyIssues { get; } = new();
    public List<DatabasePerformanceIssue> DatabaseIssues { get; } = new();
    
    public double PerformanceScore
    {
        get
        {
            var totalIssues = SyncIoViolations.Count + MemoryLeakRisks.Count + ConcurrencyIssues.Count + DatabaseIssues.Count;
            return totalIssues == 0 ? 100.0 : Math.Max(0, 100.0 - (totalIssues * 5)); // 5 points per issue
        }
    }
}

public class SyncIoViolation
{
    public Type Type { get; set; } = null!;
    public List<System.Reflection.MethodInfo> Methods { get; set; } = new();
    public string Description => $"{Type.Name} has {Methods.Count} synchronous I/O operations";
}

public class ConcurrencyIssue
{
    public Type Type { get; set; } = null!;
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
}

public class DatabasePerformanceIssue
{
    public Type Type { get; set; } = null!;
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
}

#endregion

#region Security Analysis Models

/// <summary>
/// Security pattern analysis result.
/// </summary>
public class SecurityAnalysisResult
{
    public List<Type> ValidatedTypes { get; } = new();
    public List<Type> UnvalidatedInputs { get; } = new();
    public List<Type> UnauthorizedEndpoints { get; } = new();
    public List<Type> UnprotectedSensitiveData { get; } = new();
    public List<SecurityVulnerability> Vulnerabilities { get; } = new();
    
    public double SecurityScore
    {
        get
        {
            var totalTypes = ValidatedTypes.Count + UnvalidatedInputs.Count + UnauthorizedEndpoints.Count + UnprotectedSensitiveData.Count;
            if (totalTypes == 0) return 100.0;
            
            var secureCount = ValidatedTypes.Count;
            return (double)secureCount / totalTypes * 100.0;
        }
    }
}

public class SecurityVulnerability
{
    public Type Type { get; set; } = null!;
    public string VulnerabilityType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

#endregion

#region Test Execution Models

/// <summary>
/// Enhanced test execution context with metrics.
/// </summary>
public class EnhancedTestContext
{
    public DependencyGraph DependencyGraph { get; set; } = new();
    public ArchitectureMetrics Metrics { get; set; } = new();
    public CqrsAnalysisResult CqrsAnalysis { get; set; } = new();
    public DddAnalysisResult DddAnalysis { get; set; } = new();
    public PerformanceAnalysisResult PerformanceAnalysis { get; set; } = new();
    public SecurityAnalysisResult SecurityAnalysis { get; set; } = new();
    
    public DateTime AnalysisStartTime { get; set; }
    public TimeSpan AnalysisDuration { get; set; }
}

/// <summary>
/// Test execution summary with detailed insights.
/// </summary>
public class TestExecutionSummary
{
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public List<string> CriticalFailures { get; set; } = new();
    public Dictionary<string, int> CategoryResults { get; set; } = new();
    public List<PerformanceAlert> PerformanceAlerts { get; set; } = new();
    
    public double SuccessRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
}

public class PerformanceAlert
{
    public string Component { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}

#endregion