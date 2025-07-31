using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using MediatR;

namespace Axon.ArchitectureTests.Core.Rules.CQRS;

/// <summary>
/// Validates that Queries are properly implemented following CQRS patterns.
/// </summary>
public sealed class QueryImplementationRule : PatternComplianceRule
{
    public override string RuleId => "CQRS002";
    public override string Name => "Query Implementation Rule";
    public override string Description => "Queries must implement IRequest<T>, be records, follow naming conventions, and be read-only";
    public override string Category => "CQRS";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var queryTypes = context.Types
                .Where(t => IsQueryType(t))
                .ToList();

            foreach (var queryType in queryTypes)
            {
                ValidateQueryNaming(queryType, violations);
                ValidateQueryStructure(queryType, violations);
                ValidateQueryLocation(queryType, violations);
                ValidateQueryMediatRInterface(queryType, violations);
                ValidateQueryReturnType(queryType, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsQueryType(Type type) =>
        type.Name.EndsWith("Query", StringComparison.OrdinalIgnoreCase) ||
        IsInQueriesNamespace(type);

    private static bool IsInQueriesNamespace(Type type) =>
        type.Namespace?.Contains(".Queries.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Queries", StringComparison.OrdinalIgnoreCase) == true;

    private void ValidateQueryNaming(Type queryType, List<RuleViolation> violations)
    {
        if (!queryType.Name.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                queryType,
                "Query types must end with 'Query' suffix",
                $"Rename '{queryType.Name}' to '{queryType.Name}Query'"));
        }
    }

    private void ValidateQueryStructure(Type queryType, List<RuleViolation> violations)
    {
        if (!queryType.IsValueType && !IsRecord(queryType))
        {
            violations.Add(CreateViolation(
                queryType,
                "Queries should be implemented as records for immutability",
                $"Convert '{queryType.Name}' to a record type"));
        }
    }

    private void ValidateQueryLocation(Type queryType, List<RuleViolation> violations)
    {
        if (!IsInQueriesNamespace(queryType))
        {
            violations.Add(CreateViolation(
                queryType,
                "Queries must be placed in a Queries namespace",
                $"Move '{queryType.Name}' to a .Queries namespace"));
        }
    }

    private void ValidateQueryMediatRInterface(Type queryType, List<RuleViolation> violations)
    {
        var implementsIRequest = ImplementsGenericInterface(queryType, typeof(IRequest<>));

        if (!implementsIRequest)
        {
            violations.Add(CreateViolation(
                queryType,
                "Queries must implement IRequest<T> from MediatR (not plain IRequest)",
                $"Make '{queryType.Name}' implement IRequest<T> with appropriate return type"));
        }
    }

    private void ValidateQueryReturnType(Type queryType, List<RuleViolation> violations)
    {
        var requestInterface = queryType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

        if (requestInterface != null)
        {
            var returnType = requestInterface.GetGenericArguments()[0];
            
            // Queries should return data, not void/Unit
            if (returnType.Name == "Unit")
            {
                violations.Add(CreateViolation(
                    queryType,
                    "Queries should return data, not Unit. Use Commands for operations that don't return data",
                    $"Change '{queryType.Name}' to return appropriate data type or convert to Command"));
            }
        }
    }

    private static bool IsRecord(Type type) =>
        type.GetMethod("Equals", new[] { type }) != null &&
        type.GetMethod("GetHashCode", Type.EmptyTypes) != null &&
        type.BaseType == typeof(object);
}