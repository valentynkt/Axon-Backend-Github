using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.CleanArchitecture;

/// <summary>
/// Validates that the Domain layer has no dependencies on outer layers and only uses Shared components.
/// </summary>
public sealed class DomainLayerIsolationRule : LayerBoundaryRule
{
    public DomainLayerIsolationRule() : base(
        sourceLayer: "Domain",
        allowedTargetLayers: new[] { "Shared" },
        forbiddenTargetLayers: new[] { "Application", "Infrastructure", "Api" })
    {
    }

    public override string RuleId => "CA004";
    public override string Name => "Domain Layer Isolation Rule";
    public override string Description => "Domain layer must be isolated and only depend on Shared components, never on outer layers";
    public override RuleSeverity Severity => RuleSeverity.Critical;

    protected override string? GetSuggestedFix(Type sourceType, Type dependency)
    {
        var dependencyNamespace = dependency.Namespace ?? string.Empty;
        
        if (dependencyNamespace.Contains("Application", StringComparison.OrdinalIgnoreCase))
            return "Move application logic out of domain layer or create domain services";
        
        if (dependencyNamespace.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase))
            return "Use domain interfaces and implement in Infrastructure layer";
        
        if (dependencyNamespace.Contains("Api", StringComparison.OrdinalIgnoreCase))
            return "Remove API dependencies from domain - use events or move logic to Application layer";
        
        return base.GetSuggestedFix(sourceType, dependency);
    }
}