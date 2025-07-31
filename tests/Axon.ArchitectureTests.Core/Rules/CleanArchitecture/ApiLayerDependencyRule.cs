using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.CleanArchitecture;

/// <summary>
/// Validates that the API layer only depends on Application layer and Shared components.
/// </summary>
public sealed class ApiLayerDependencyRule : LayerBoundaryRule
{
    public ApiLayerDependencyRule() : base(
        sourceLayer: "Api",
        allowedTargetLayers: new[] { "Application", "Shared" },
        forbiddenTargetLayers: new[] { "Domain", "Infrastructure" })
    {
    }

    public override string RuleId => "CA001";
    public override string Name => "API Layer Dependency Rule";
    public override string Description => "API layer should only depend on Application layer and Shared components, never directly on Domain or Infrastructure";

    protected override string? GetSuggestedFix(Type sourceType, Type dependency) =>
        dependency.Namespace?.Contains("Domain", StringComparison.OrdinalIgnoreCase) == true
            ? "Move domain logic to Application layer or inject via interface"
            : dependency.Namespace?.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) == true
                ? "Use dependency injection to access infrastructure services through interfaces"
                : base.GetSuggestedFix(sourceType, dependency);
}