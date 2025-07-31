using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.CQRS;

/// <summary>
/// Validates that CQRS components follow proper folder structure organization.
/// </summary>
public sealed class FolderStructureRule : PatternComplianceRule
{
    public override string RuleId => "CQRS004";
    public override string Name => "CQRS Folder Structure Rule";
    public override string Description => "Commands, Queries, and Handlers must be organized in appropriate folder structures";
    public override string Category => "CQRS";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var applicationTypes = context.Types
                .Where(t => IsInNamespace(t, "Application"))
                .ToList();

            foreach (var type in applicationTypes)
            {
                ValidateCQRSStructure(type, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private void ValidateCQRSStructure(Type type, List<RuleViolation> violations)
    {
        var typeName = type.Name;
        var @namespace = type.Namespace ?? string.Empty;

        // Commands should be in Commands folder/namespace
        if (typeName.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
        {
            if (!@namespace.Contains(".Commands.", StringComparison.OrdinalIgnoreCase) &&
                !@namespace.EndsWith(".Commands", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(CreateViolation(
                    type,
                    "Commands must be placed in a Commands folder/namespace",
                    $"Move '{typeName}' to Application/Commands/{GetFeatureName(typeName)}/ folder"));
            }
        }

        // Queries should be in Queries folder/namespace
        if (typeName.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
        {
            if (!@namespace.Contains(".Queries.", StringComparison.OrdinalIgnoreCase) &&
                !@namespace.EndsWith(".Queries", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(CreateViolation(
                    type,
                    "Queries must be placed in a Queries folder/namespace",
                    $"Move '{typeName}' to Application/Queries/{GetFeatureName(typeName)}/ folder"));
            }
        }

        // Handlers should be in appropriate feature folder
        if (typeName.EndsWith("Handler", StringComparison.OrdinalIgnoreCase))
        {
            ValidateHandlerPlacement(type, violations);
        }
    }

    private void ValidateHandlerPlacement(Type handlerType, List<RuleViolation> violations)
    {
        var handlerName = handlerType.Name;
        var @namespace = handlerType.Namespace ?? string.Empty;

        // Command handlers should be with their commands
        if (handlerName.Contains("Command", StringComparison.OrdinalIgnoreCase))
        {
            if (!@namespace.Contains(".Commands.", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    "Command handlers should be placed near their commands",
                    $"Move '{handlerName}' to the same folder as its corresponding command"));
            }
        }

        // Query handlers should be with their queries
        if (handlerName.Contains("Query", StringComparison.OrdinalIgnoreCase))
        {
            if (!@namespace.Contains(".Queries.", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    "Query handlers should be placed near their queries",
                    $"Move '{handlerName}' to the same folder as its corresponding query"));
            }
        }
    }

    private static string GetFeatureName(string typeName)
    {
        // Extract feature name from type name
        // e.g., "CreateUserCommand" -> "CreateUser"
        if (typeName.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
            return typeName[..^7]; // Remove "Command"
        
        if (typeName.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
            return typeName[..^5]; // Remove "Query"
        
        if (typeName.EndsWith("Handler", StringComparison.OrdinalIgnoreCase))
            return typeName[..^7]; // Remove "Handler"
        
        return typeName;
    }
}