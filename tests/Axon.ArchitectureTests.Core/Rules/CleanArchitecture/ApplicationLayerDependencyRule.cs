using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.CleanArchitecture;

/// <summary>
/// Validates that the Application layer only depends on Domain layer and Shared components.
/// </summary>
public sealed class ApplicationLayerDependencyRule : LayerBoundaryRule
{
    public ApplicationLayerDependencyRule() : base(
        sourceLayer: "Application",
        allowedTargetLayers: new[] { "Domain", "Shared" },
        forbiddenTargetLayers: new[] { "Infrastructure", "Api" })
    {
    }

    public override string RuleId => "CA002";
    public override string Name => "Application Layer Dependency Rule";
    public override string Description => "Application layer should only depend on Domain layer and Shared components, never on Infrastructure or API layer";

    protected override string? GetSuggestedFix(Type sourceType, Type dependency) =>
        dependency.Namespace?.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) == true
            ? "Use dependency injection and interfaces to access infrastructure services"
            : dependency.Namespace?.Contains("Api", StringComparison.OrdinalIgnoreCase) == true
                ? "Remove reverse dependency - API should depend on Application, not vice versa"
                : base.GetSuggestedFix(sourceType, dependency);
}