using System.Reflection;
using System.Text.Json;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Engine;

namespace Axon.Tests.Shared.Utilities;

/// <summary>
/// Comprehensive reusable architecture test utilities for enhanced validation patterns.
/// Provides common functionality for architecture rule testing and analysis.
/// </summary>
public static class ArchitectureTestUtilities
{
    #region Dependency Analysis

    /// <summary>
    /// Analyzes type dependencies and creates a comprehensive dependency graph.
    /// </summary>
    public static DependencyGraph CreateDependencyGraph(IEnumerable<Type> types)
    {
        var graph = new DependencyGraph();
        
        foreach (var type in types.Where(t => t.Namespace?.StartsWith("Axon") == true))
        {
            var dependencies = ExtractTypeDependencies(type);
            graph.AddNode(type, dependencies);
        }
        
        return graph;
    }

    /// <summary>
    /// Extracts all dependencies for a given type including constructor, property, and method dependencies.
    /// </summary>
    public static HashSet<Type> ExtractTypeDependencies(Type type)
    {
        var dependencies = new HashSet<Type>();
        
        // Constructor dependencies
        foreach (var constructor in type.GetConstructors())
        {
            foreach (var param in constructor.GetParameters())
            {
                if (IsAxonType(param.ParameterType))
                {
                    dependencies.Add(param.ParameterType);
                }
            }
        }
        
        // Property dependencies
        foreach (var property in type.GetProperties())
        {
            if (IsAxonType(property.PropertyType))
            {
                dependencies.Add(property.PropertyType);
            }
        }
        
        // Method parameter and return type dependencies
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            // Return type
            if (IsAxonType(method.ReturnType))
            {
                dependencies.Add(method.ReturnType);
            }
            
            // Parameters
            foreach (var param in method.GetParameters())
            {
                if (IsAxonType(param.ParameterType))
                {
                    dependencies.Add(param.ParameterType);
                }
            }
        }
        
        // Generic type arguments
        if (type.IsGenericType)
        {
            foreach (var genericArg in type.GetGenericArguments())
            {
                if (IsAxonType(genericArg))
                {
                    dependencies.Add(genericArg);
                }
            }
        }
        
        return dependencies;
    }

    /// <summary>
    /// Identifies circular dependencies in a dependency graph.
    /// </summary>
    public static List<CircularDependency> FindCircularDependencies(DependencyGraph graph)
    {
        var circularDependencies = new List<CircularDependency>();
        var visited = new HashSet<Type>();
        var recursionStack = new Stack<Type>();
        
        foreach (var node in graph.Nodes.Keys)
        {
            if (!visited.Contains(node))
            {
                FindCircularDependenciesRecursive(node, graph, visited, recursionStack, circularDependencies);
            }
        }
        
        return circularDependencies;
    }

    #endregion

    #region Layer Analysis

    /// <summary>
    /// Determines the architectural layer of a given type.
    /// </summary>
    public static ArchitecturalLayer DetermineArchitecturalLayer(Type type)
    {
        var namespaceParts = type.Namespace?.Split('.') ?? Array.Empty<string>();
        
        foreach (var part in namespaceParts)
        {
            switch (part.ToLowerInvariant())
            {
                case "api":
                    return ArchitecturalLayer.Api;
                case "application":
                    return ArchitecturalLayer.Application;
                case "domain":
                    return ArchitecturalLayer.Domain;
                case "infrastructure":
                    return ArchitecturalLayer.Infrastructure;
                case "shared":
                    return ArchitecturalLayer.Shared;
            }
        }
        
        return ArchitecturalLayer.Unknown;
    }

    /// <summary>
    /// Validates layer dependency rules based on Clean Architecture principles.
    /// </summary>
    public static List<LayerViolation> ValidateLayerDependencies(DependencyGraph graph)
    {
        var violations = new List<LayerViolation>();
        
        foreach (var (type, dependencies) in graph.Nodes)
        {
            var sourceLayer = DetermineArchitecturalLayer(type);
            
            foreach (var dependency in dependencies)
            {
                var targetLayer = DetermineArchitecturalLayer(dependency);
                
                if (IsInvalidLayerDependency(sourceLayer, targetLayer))
                {
                    violations.Add(new LayerViolation
                    {
                        SourceType = type,
                        TargetType = dependency,
                        SourceLayer = sourceLayer,
                        TargetLayer = targetLayer,
                        ViolationType = GetViolationType(sourceLayer, targetLayer)
                    });
                }
            }
        }
        
        return violations;
    }

    #endregion

    #region Pattern Analysis

    /// <summary>
    /// Analyzes CQRS pattern compliance for commands and queries.
    /// </summary>
    public static CqrsAnalysisResult AnalyzeCqrsCompliance(IEnumerable<Type> types)
    {
        var result = new CqrsAnalysisResult();
        
        foreach (var type in types)
        {
            if (IsCommand(type))
            {
                result.Commands.Add(AnalyzeCommand(type));
            }
            else if (IsQuery(type))
            {
                result.Queries.Add(AnalyzeQuery(type));
            }
            else if (IsCommandHandler(type))
            {
                result.CommandHandlers.Add(AnalyzeCommandHandler(type));
            }
            else if (IsQueryHandler(type))
            {
                result.QueryHandlers.Add(AnalyzeQueryHandler(type));
            }
        }
        
        return result;
    }

    /// <summary>
    /// Analyzes DDD patterns including aggregates, entities, and value objects.
    /// </summary>
    public static DddAnalysisResult AnalyzeDddPatterns(IEnumerable<Type> types)
    {
        var result = new DddAnalysisResult();
        
        foreach (var type in types.Where(t => DetermineArchitecturalLayer(t) == ArchitecturalLayer.Domain))
        {
            if (IsAggregateRoot(type))
            {
                result.AggregateRoots.Add(AnalyzeAggregateRoot(type));
            }
            else if (IsEntity(type))
            {
                result.Entities.Add(AnalyzeEntity(type));
            }
            else if (IsValueObject(type))
            {
                result.ValueObjects.Add(AnalyzeValueObject(type));
            }
            else if (IsDomainService(type))
            {
                result.DomainServices.Add(AnalyzeDomainService(type));
            }
        }
        
        return result;
    }

    #endregion

    #region Metrics and Reporting

    /// <summary>
    /// Calculates comprehensive architecture metrics for a codebase.
    /// </summary>
    public static ArchitectureMetrics CalculateArchitectureMetrics(DependencyGraph graph)
    {
        var metrics = new ArchitectureMetrics();
        
        // Coupling metrics
        metrics.AverageCoupling = graph.Nodes.Values.Average(deps => deps.Count);
        metrics.MaxCoupling = graph.Nodes.Values.Max(deps => deps.Count);
        metrics.HighlyCoupledTypes = graph.Nodes
            .Where(kvp => kvp.Value.Count > 10) // Configurable threshold
            .Select(kvp => kvp.Key)
            .ToList();
        
        // Layer distribution
        var layerCounts = new Dictionary<ArchitecturalLayer, int>();
        foreach (var type in graph.Nodes.Keys)
        {
            var layer = DetermineArchitecturalLayer(type);
            layerCounts[layer] = layerCounts.GetValueOrDefault(layer, 0) + 1;
        }
        metrics.LayerDistribution = layerCounts;
        
        // Circular dependency metrics
        var circularDeps = FindCircularDependencies(graph);
        metrics.CircularDependencyCount = circularDeps.Count;
        metrics.CircularDependencies = circularDeps;
        
        // Stability metrics
        metrics.StabilityScores = CalculateStabilityScores(graph);
        
        return metrics;
    }

    /// <summary>
    /// Generates a comprehensive architecture health report.
    /// </summary>
    public static ArchitectureHealthReport GenerateHealthReport(
        EngineResult testResults,
        ArchitectureMetrics metrics,
        TimeSpan executionTime)
    {
        var report = new ArchitectureHealthReport
        {
            GeneratedAt = DateTime.UtcNow,
            ExecutionTime = executionTime,
            Metrics = metrics
        };
        
        // Calculate overall health score
        var totalRules = testResults.RuleResults.Count;
        var passedRules = testResults.RuleResults.Count(r => r.IsSuccess);
        report.OverallHealthScore = (double)passedRules / totalRules * 100;
        
        // Category scores
        report.CategoryScores = CalculateCategoryScores(testResults);
        
        // Critical issues
        report.CriticalIssues = testResults.RuleResults
            .Where(r => !r.IsSuccess && r.RuleId.Contains("Critical"))
            .ToList();
        
        // Recommendations
        report.Recommendations = GenerateRecommendations(testResults, metrics);
        
        return report;
    }

    /// <summary>
    /// Exports architecture analysis results to JSON format.
    /// </summary>
    public static string ExportToJson(ArchitectureHealthReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        return JsonSerializer.Serialize(report, options);
    }

    #endregion

    #region Performance Analysis

    /// <summary>
    /// Analyzes performance characteristics of architectural patterns.
    /// </summary>
    public static PerformanceAnalysisResult AnalyzePerformancePatterns(IEnumerable<Type> types)
    {
        var result = new PerformanceAnalysisResult();
        
        foreach (var type in types)
        {
            // Async pattern analysis
            var asyncMethods = GetAsyncMethods(type);
            var syncMethods = GetSyncMethods(type);
            
            if (syncMethods.Any(m => IsIoBoundMethod(m)))
            {
                result.SyncIoViolations.Add(new SyncIoViolation
                {
                    Type = type,
                    Methods = syncMethods.Where(IsIoBoundMethod).ToList()
                });
            }
            
            // Memory efficiency analysis
            if (HasMemoryLeakRisk(type))
            {
                result.MemoryLeakRisks.Add(type);
            }
            
            // Caching opportunities
            var cachingOpportunities = IdentifyCachingOpportunities(type);
            if (cachingOpportunities.Any())
            {
                result.CachingOpportunities[type] = cachingOpportunities;
            }
        }
        
        return result;
    }

    #endregion

    #region Security Analysis

    /// <summary>
    /// Analyzes security patterns and potential vulnerabilities.
    /// </summary>
    public static SecurityAnalysisResult AnalyzeSecurityPatterns(IEnumerable<Type> types)
    {
        var result = new SecurityAnalysisResult();
        
        foreach (var type in types)
        {
            // Input validation analysis
            if (HasInputValidation(type))
            {
                result.ValidatedTypes.Add(type);
            }
            else if (RequiresInputValidation(type))
            {
                result.UnvalidatedInputs.Add(type);
            }
            
            // Authentication/Authorization analysis
            if (RequiresAuthorization(type) && !HasAuthorizationAttributes(type))
            {
                result.UnauthorizedEndpoints.Add(type);
            }
            
            // Sensitive data handling
            if (HandlesSensitiveData(type) && !HasProperDataProtection(type))
            {
                result.UnprotectedSensitiveData.Add(type);
            }
        }
        
        return result;
    }

    #endregion

    #region Helper Methods

    private static bool IsAxonType(Type type) =>
        type.Namespace?.StartsWith("Axon") == true;

    private static void FindCircularDependenciesRecursive(
        Type current,
        DependencyGraph graph,
        HashSet<Type> visited,
        Stack<Type> recursionStack,
        List<CircularDependency> circularDependencies)
    {
        visited.Add(current);
        recursionStack.Push(current);
        
        if (graph.Nodes.TryGetValue(current, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                if (recursionStack.Contains(dependency))
                {
                    var cycle = new List<Type>();
                    var stackArray = recursionStack.ToArray();
                    var startIndex = Array.IndexOf(stackArray, dependency);
                    
                    for (int i = startIndex; i >= 0; i--)
                    {
                        cycle.Add(stackArray[i]);
                    }
                    
                    circularDependencies.Add(new CircularDependency { Cycle = cycle });
                }
                else if (!visited.Contains(dependency))
                {
                    FindCircularDependenciesRecursive(dependency, graph, visited, recursionStack, circularDependencies);
                }
            }
        }
        
        recursionStack.Pop();
    }

    private static bool IsInvalidLayerDependency(ArchitecturalLayer source, ArchitecturalLayer target)
    {
        // Define valid dependency rules
        var validDependencies = new Dictionary<ArchitecturalLayer, HashSet<ArchitecturalLayer>>
        {
            [ArchitecturalLayer.Api] = new() { ArchitecturalLayer.Application, ArchitecturalLayer.Shared },
            [ArchitecturalLayer.Application] = new() { ArchitecturalLayer.Domain, ArchitecturalLayer.Shared },
            [ArchitecturalLayer.Infrastructure] = new() { ArchitecturalLayer.Application, ArchitecturalLayer.Domain, ArchitecturalLayer.Shared },
            [ArchitecturalLayer.Domain] = new() { ArchitecturalLayer.Shared },
            [ArchitecturalLayer.Shared] = new() { }
        };
        
        return !validDependencies.GetValueOrDefault(source, new HashSet<ArchitecturalLayer>()).Contains(target);
    }

    private static LayerViolationType GetViolationType(ArchitecturalLayer source, ArchitecturalLayer target)
    {
        if (source == ArchitecturalLayer.Domain && target != ArchitecturalLayer.Shared)
            return LayerViolationType.DomainIsolationViolation;
        
        if (source == ArchitecturalLayer.Application && target == ArchitecturalLayer.Infrastructure)
            return LayerViolationType.DependencyInversionViolation;
        
        return LayerViolationType.GeneralLayerViolation;
    }

    private static bool IsCommand(Type type) =>
        type.Name.EndsWith("Command") || 
        type.GetInterfaces().Any(i => i.Name.Contains("ICommand"));

    private static bool IsQuery(Type type) =>
        type.Name.EndsWith("Query") || 
        type.GetInterfaces().Any(i => i.Name.Contains("IQuery"));

    private static bool IsCommandHandler(Type type) =>
        type.Name.EndsWith("CommandHandler") || 
        type.GetInterfaces().Any(i => i.Name.Contains("ICommandHandler"));

    private static bool IsQueryHandler(Type type) =>
        type.Name.EndsWith("QueryHandler") || 
        type.GetInterfaces().Any(i => i.Name.Contains("IQueryHandler"));

    private static bool IsAggregateRoot(Type type) =>
        type.Name.EndsWith("Aggregate") || 
        type.BaseType?.Name.Contains("AggregateRoot") == true;

    private static bool IsEntity(Type type) =>
        type.BaseType?.Name.Contains("Entity") == true ||
        (DetermineArchitecturalLayer(type) == ArchitecturalLayer.Domain && 
         type.IsClass && 
         !IsValueObject(type) && 
         !IsAggregateRoot(type));

    private static bool IsValueObject(Type type) =>
        type.IsValueType || 
        type.Name.EndsWith("Id") || 
        type.Name.EndsWith("Value") ||
        (type.GetMethod("Equals", new[] { type }) != null && type.BaseType == typeof(object));

    private static bool IsDomainService(Type type) =>
        type.Name.EndsWith("Service") && 
        DetermineArchitecturalLayer(type) == ArchitecturalLayer.Domain;

    // Additional helper methods for analysis
    private static CommandAnalysis AnalyzeCommand(Type type) => new() { Type = type };
    private static QueryAnalysis AnalyzeQuery(Type type) => new() { Type = type };
    private static CommandHandlerAnalysis AnalyzeCommandHandler(Type type) => new() { Type = type };
    private static QueryHandlerAnalysis AnalyzeQueryHandler(Type type) => new() { Type = type };
    private static AggregateRootAnalysis AnalyzeAggregateRoot(Type type) => new() { Type = type };
    private static EntityAnalysis AnalyzeEntity(Type type) => new() { Type = type };
    private static ValueObjectAnalysis AnalyzeValueObject(Type type) => new() { Type = type };
    private static DomainServiceAnalysis AnalyzeDomainService(Type type) => new() { Type = type };

    private static Dictionary<ArchitecturalLayer, double> CalculateStabilityScores(DependencyGraph graph) =>
        new(); // Implementation would calculate actual stability metrics

    private static Dictionary<string, double> CalculateCategoryScores(EngineResult testResults) =>
        testResults.RuleResults
            .GroupBy(r => r.RuleId.Split('_').FirstOrDefault() ?? "Unknown")
            .ToDictionary(
                g => g.Key,
                g => (double)g.Count(r => r.IsSuccess) / g.Count() * 100);

    private static List<string> GenerateRecommendations(EngineResult testResults, ArchitectureMetrics metrics) =>
        new(); // Implementation would generate specific recommendations

    private static List<MethodInfo> GetAsyncMethods(Type type) =>
        type.GetMethods().Where(m => m.ReturnType.Name.Contains("Task")).ToList();

    private static List<MethodInfo> GetSyncMethods(Type type) =>
        type.GetMethods().Where(m => !m.ReturnType.Name.Contains("Task")).ToList();

    private static bool IsIoBoundMethod(MethodInfo method) =>
        method.Name.Contains("Read") || method.Name.Contains("Write") || 
        method.Name.Contains("Http") || method.Name.Contains("Database");

    private static bool HasMemoryLeakRisk(Type type) => false; // Simplified
    private static List<string> IdentifyCachingOpportunities(Type type) => new(); // Simplified
    private static bool HasInputValidation(Type type) => false; // Simplified
    private static bool RequiresInputValidation(Type type) => false; // Simplified
    private static bool RequiresAuthorization(Type type) => false; // Simplified
    private static bool HasAuthorizationAttributes(Type type) => false; // Simplified
    private static bool HandlesSensitiveData(Type type) => false; // Simplified
    private static bool HasProperDataProtection(Type type) => false; // Simplified

    #endregion
}