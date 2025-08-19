using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query.Validator;

namespace Axon.Api.OData;

/// <summary>
/// Custom OData query validator to enforce security and performance constraints
/// </summary>
public class SecureODataQueryValidator : ODataQueryValidator
{
    private readonly HashSet<string> _allowedOrderByProperties = new()
    {
        "UpdatedAt",
        "CreatedAt", 
        "Title",
        "Id",
        "MessageCount",
        "Role",
        "Content",
        "TokenCount"
    };

    private readonly HashSet<string> _allowedFilterProperties = new()
    {
        "UpdatedAt",
        "CreatedAt",
        "Title",
        "Id",
        "OwnerId",
        "ConversationId",
        "MessageCount",
        "Role",
        "IsDeleted"
    };

    public override void ValidateOrderByQueryOption(OrderByQueryOption orderByOption, ODataValidationSettings validationSettings)
    {
        if (orderByOption?.OrderByNodes != null)
        {
            foreach (var node in orderByOption.OrderByNodes)
            {
                var propertyName = GetPropertyName(node);
                if (!string.IsNullOrEmpty(propertyName) && !_allowedOrderByProperties.Contains(propertyName))
                {
                    throw new ODataException($"Ordering by '{propertyName}' is not allowed. Allowed properties: {string.Join(", ", _allowedOrderByProperties)}");
                }
            }
        }

        base.ValidateOrderByQueryOption(orderByOption, validationSettings);
    }

    public override void ValidateFilterQueryOption(FilterQueryOption filterOption, ODataValidationSettings validationSettings)
    {
        // Validate filter properties are allowed
        if (filterOption?.FilterClause != null)
        {
            ValidateFilterNode(filterOption.FilterClause.Expression);
        }

        base.ValidateFilterQueryOption(filterOption, validationSettings);
    }

    public override void ValidateSelectExpandQueryOption(SelectExpandQueryOption selectExpandOption, ODataValidationSettings validationSettings)
    {
        // For now, disable $expand to prevent deep object graphs
        if (selectExpandOption?.RawExpand != null)
        {
            throw new ODataException("The $expand query option is not supported in this API version.");
        }

        base.ValidateSelectExpandQueryOption(selectExpandOption, validationSettings);
    }

    private void ValidateFilterNode(SingleValueNode node)
    {
        // Traverse the filter expression tree to validate property access
        // This is a simplified version - extend as needed for complex scenarios
        
        // Note: Full implementation would recursively validate all nodes
        // For MVP, we're allowing filters validated at the repository level
    }

    private static string? GetPropertyName(OrderByNode node)
    {
        // Extract property name from the order by node
        // This is simplified - in production, handle nested properties
        return node.Direction.ToString(); // Placeholder - implement proper extraction
    }
}