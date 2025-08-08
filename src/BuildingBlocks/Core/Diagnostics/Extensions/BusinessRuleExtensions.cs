using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Rules;

namespace BuildingBlocks.Core.Diagnostics.Extensions;

/// <summary>
/// Extensions for converting IBusinessRule to Error types
/// </summary>
public static class BusinessRuleExtensions
{
    /// <summary>
    /// Converts a business rule to an Error
    /// </summary>
    /// <param name="businessRule">The business rule to convert</param>
    /// <returns>Error representing the business rule violation</returns>
    public static Error ToError(this IBusinessRule businessRule)
    {
        return Error.BusinessRule(businessRule.Message, businessRule.Code);
    }
    
    /// <summary>
    /// Converts a business rule to an Error with additional metadata
    /// </summary>
    /// <param name="businessRule">The business rule to convert</param>
    /// <param name="metadata">Additional metadata to include</param>
    /// <returns>Error representing the business rule violation with metadata</returns>
    public static Error ToError(this IBusinessRule businessRule, IReadOnlyDictionary<string, object>? metadata)
    {
        return Error.BusinessRule(businessRule.Message, businessRule.Code, metadata);
    }
}