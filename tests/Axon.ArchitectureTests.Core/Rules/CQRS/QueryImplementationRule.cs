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
                ValidateQueryReadOnlyBehavior(queryType, violations);
                ValidateQueryPaging(queryType, violations);
                ValidateQueryFiltering(queryType, violations);
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

    /// <summary>
    /// Validates that queries don't contain state-changing operations or mutable collections.
    /// </summary>
    private void ValidateQueryReadOnlyBehavior(Type queryType, List<RuleViolation> violations)
    {
        var properties = GetPublicProperties(queryType);
        
        foreach (var property in properties)
        {
            // Check for mutable collections that might be modified
            if (IsMutableCollectionType(property.PropertyType))
            {
                violations.Add(CreateViolation(
                    queryType,
                    $"Query property '{property.Name}' uses mutable collection type '{property.PropertyType.Name}'",
                    $"Use IReadOnlyCollection<T>, IReadOnlyList<T>, or immutable collection for '{property.Name}'"));
            }

            // Check for properties with public setters
            if (property.CanWrite && property.SetMethod?.IsPublic == true)
            {
                violations.Add(CreateViolation(
                    queryType,
                    $"Query property '{property.Name}' has public setter, violating read-only principle",
                    $"Make property '{property.Name}' init-only or readonly"));
            }
        }
    }

    /// <summary>
    /// Validates that queries follow proper paging patterns when dealing with collections.
    /// </summary>
    private void ValidateQueryPaging(Type queryType, List<RuleViolation> violations)
    {
        var properties = GetPublicProperties(queryType);
        var hasPotentialCollectionResult = HasCollectionReturnType(queryType);
        
        if (hasPotentialCollectionResult)
        {
            var hasPagingProperties = properties.Any(p => 
                p.Name.Contains("Page", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Skip", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Take", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase));

            if (!hasPagingProperties && queryType.Name.Contains("List", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(CreateViolation(
                    queryType,
                    "Queries returning collections should include paging parameters",
                    $"Add paging properties (PageSize, PageNumber) to '{queryType.Name}'"));
            }
        }
    }

    /// <summary>
    /// Validates that queries have appropriate filtering capabilities.
    /// </summary>
    private void ValidateQueryFiltering(Type queryType, List<RuleViolation> violations)
    {
        // Queries with "GetAll" or "List" patterns should have filtering
        if (queryType.Name.Contains("GetAll", StringComparison.OrdinalIgnoreCase) ||
            queryType.Name.Contains("ListAll", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                queryType,
                "Avoid 'GetAll' or 'ListAll' patterns without filtering capabilities",
                $"Add filtering parameters or rename '{queryType.Name}' to be more specific"));
        }
    }

    /// <summary>
    /// Checks if a type is a mutable collection type.
    /// </summary>
    private static bool IsMutableCollectionType(Type type)
    {
        return (type.IsGenericType && 
                (type.GetGenericTypeDefinition() == typeof(List<>) ||
                 type.GetGenericTypeDefinition() == typeof(IList<>) ||
                 type.GetGenericTypeDefinition() == typeof(ICollection<>))) ||
               type.IsArray;
    }

    /// <summary>
    /// Checks if the query returns a collection type.
    /// </summary>
    private static bool HasCollectionReturnType(Type queryType)
    {
        var requestInterface = queryType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

        if (requestInterface != null)
        {
            var returnType = requestInterface.GetGenericArguments()[0];
            return IsCollectionType(returnType);
        }

        return false;
    }

    /// <summary>
    /// Checks if a type represents a collection.
    /// </summary>
    private static bool IsCollectionType(Type type)
    {
        return type.IsArray ||
               (type.IsGenericType && 
                (type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
                 type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                 type.GetGenericTypeDefinition() == typeof(IList<>) ||
                 type.GetGenericTypeDefinition() == typeof(List<>)));
    }

    private static bool IsRecord(Type type) =>
        type.GetMethod("Equals", new[] { type }) != null &&
        type.GetMethod("GetHashCode", Type.EmptyTypes) != null &&
        type.BaseType == typeof(object);
}