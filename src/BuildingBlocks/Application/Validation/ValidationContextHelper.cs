namespace BuildingBlocks.Application.Validation;

/// <summary>
/// Helper class for common validation context operations.
/// Reduces code duplication in ValidatorBase implementations.
/// </summary>
public static class ValidationContextHelper
{
    /// <summary>
    /// Validates that a string parameter is not null or empty.
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="parameterName">The parameter name for exception messages</param>
    /// <exception cref="ArgumentException">Thrown when value is null or empty</exception>
    public static void ValidateStringParameter(string? value, string parameterName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"{parameterName} cannot be null or empty", parameterName);
        }
    }

    /// <summary>
    /// Checks if metadata value matches expected value with type-safe comparison.
    /// </summary>
    /// <param name="context">The validation context</param>
    /// <param name="key">Metadata key to check</param>
    /// <param name="expectedValue">Expected value to compare against</param>
    /// <returns>True if metadata value matches expected value</returns>
    public static bool IsMetadataValueMatch(IValidationContext? context, string key, object expectedValue)
    {
        if (context == null)
        {
            return false;
        }
        
        var actualValue = context.GetMetadata<object>(key);
        
        if (actualValue == null && expectedValue == null)
        {
            return true;
        }
        
        if (actualValue == null || expectedValue == null)
        {
            return false;
        }
        
        // Handle string comparisons case-insensitively
        if (actualValue is string actualString && expectedValue is string expectedString)
        {
            return string.Equals(actualString, expectedString, StringComparison.OrdinalIgnoreCase);
        }
        
        // Handle direct equality
        return actualValue.Equals(expectedValue);
    }

    /// <summary>
    /// Checks if current request is from specific tenant.
    /// </summary>
    /// <param name="context">The validation context</param>
    /// <param name="tenantId">Target tenant identifier</param>
    /// <returns>True if request is from specified tenant</returns>
    public static bool IsFromTenant(IValidationContext? context, string tenantId)
    {
        return string.Equals(context?.TenantId, tenantId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if current request is from specific user.
    /// </summary>
    /// <param name="context">The validation context</param>
    /// <param name="userId">Target user identifier</param>
    /// <returns>True if request is from specified user</returns>
    public static bool IsFromUser(IValidationContext? context, string userId)
    {
        return string.Equals(context?.UserId, userId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if specified feature is enabled for current request.
    /// </summary>
    /// <param name="context">The validation context</param>
    /// <param name="featureName">Feature flag name to check</param>
    /// <returns>True if feature is enabled</returns>
    public static bool HasFeatureEnabled(IValidationContext? context, string featureName)
    {
        return context?.IsFeatureEnabled(featureName) == true;
    }
}