using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.CleanArchitecture;

/// <summary>
/// Validates that the Infrastructure layer only depends on Application layer and Shared components.
/// </summary>
public sealed class InfrastructureLayerDependencyRule : LayerBoundaryRule
{
    public InfrastructureLayerDependencyRule() : base(
        sourceLayer: "Infrastructure",
        allowedTargetLayers: new[] { "Application", "Shared" },
        forbiddenTargetLayers: new[] { "Api" })
    {
    }

    public override string RuleId => "CA003";
    public override string Name => "Infrastructure Layer Dependency Rule";
    public override string Description => "Infrastructure layer should only depend on Application layer and Shared components, never on API layer";

    protected override string? GetSuggestedFix(Type sourceType, Type dependency) =>
        dependency.Namespace?.Contains("Api", StringComparison.OrdinalIgnoreCase) == true
            ? "Remove reverse dependency - API should depend on Infrastructure via DI, not directly"
            : base.GetSuggestedFix(sourceType, dependency);
}