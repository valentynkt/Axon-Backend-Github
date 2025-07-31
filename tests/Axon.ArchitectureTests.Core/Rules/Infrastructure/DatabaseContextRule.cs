using System.Reflection;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.Infrastructure;

/// <summary>
/// Rule for validating database context organization and patterns.
/// </summary>
public sealed class DatabaseContextRule : PatternComplianceRule
{
    public override string RuleId => "INFRA003";
    public override string Name => "Database Context Rule";
    public override string Description => "Database contexts must follow established patterns for data access and organization";
    public override string Category => "Infrastructure";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var contextTypes = context.Types
                .Where(t => IsDatabaseContextType(t))
                .ToList();

            foreach (var contextType in contextTypes)
            {
                ValidateContextNaming(contextType, violations);
                ValidateContextStructure(contextType, violations);
                ValidateContextLocation(contextType, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsDatabaseContextType(Type type) =>
        type.Name.EndsWith("Context", StringComparison.OrdinalIgnoreCase) ||
        type.Name.EndsWith("DbContext", StringComparison.OrdinalIgnoreCase) ||
        IsInDataNamespace(type);

    private static bool IsInDataNamespace(Type type) =>
        type.Namespace?.Contains(".Data", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.Contains(".Persistence", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.Contains(".Infrastructure", StringComparison.OrdinalIgnoreCase) == true;

    private void ValidateContextNaming(Type contextType, List<RuleViolation> violations)
    {
        if (!contextType.Name.EndsWith("Context", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                contextType,
                "Database context types must end with 'Context' suffix",
                $"Rename '{contextType.Name}' to include 'Context' suffix"));
        }
    }

    private void ValidateContextStructure(Type contextType, List<RuleViolation> violations)
    {
        // Context should be sealed unless designed for inheritance
        if (!contextType.IsSealed && !contextType.IsAbstract)
        {
            violations.Add(CreateViolation(
                contextType,
                "Database contexts should be sealed unless designed for inheritance",
                $"Make '{contextType.Name}' sealed"));
        }
    }

    private void ValidateContextLocation(Type contextType, List<RuleViolation> violations)
    {
        if (!IsInDataNamespace(contextType))
        {
            violations.Add(CreateViolation(
                contextType,
                "Database context must be placed in appropriate data namespace",
                $"Move '{contextType.Name}' to .Data, .Persistence, or .Infrastructure namespace"));
        }
    }
}