using FluentValidation;

namespace BuildingBlocks.Application.Validation;

/// <summary>
/// Enhanced base validator with metadata-aware validation capabilities for Epic 04.
/// Provides context-aware helper methods for tenant-specific, feature-flag conditional,
/// and metadata-driven validation rules while maintaining FluentValidation compatibility.
/// </summary>
/// <typeparam name="T">The type being validated</typeparam>
public abstract class ValidatorBase<T> : AbstractValidator<T>, IMetadataValidator
    where T : class
{
    /// <summary>
    /// Current validation context providing access to metadata, trace context, and feature flags.
    /// Available after SetContext is called by ValidationBehavior.
    /// </summary>
    protected IValidationContext? Context { get; private set; }
    
    /// <summary>
    /// Sets the validation context for metadata-aware validation rules.
    /// Called automatically by ValidationBehavior before validation begins.
    /// </summary>
    /// <param name="context">The validation context with metadata access</param>
    public virtual void SetContext(IValidationContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }
    
    /// <summary>
    /// Apply validation rule only when specified feature flag is enabled.
    /// Provides feature-flag conditional validation for gradual feature rollouts.
    /// </summary>
    /// <typeparam name="TProperty">Type of the property being validated</typeparam>
    /// <param name="ruleBuilder">The rule builder to conditionally apply</param>
    /// <param name="featureName">Name of the feature flag to check</param>
    /// <returns>Rule builder options for further configuration</returns>
    /// <example>
    /// RuleFor(x => x.PhoneNumber)
    ///     .NotEmpty()
    ///     .WhenFeatureEnabled("RequirePhoneNumber");
    /// </example>
    protected IRuleBuilderOptions<T, TProperty> WhenFeatureEnabled<TProperty>(
        IRuleBuilder<T, TProperty> ruleBuilder, 
        string featureName)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);
        
        if (string.IsNullOrEmpty(featureName))
        {
            throw new ArgumentException("Feature name cannot be null or empty", nameof(featureName));
        }
        
        return ruleBuilder.When(_ => Context?.IsFeatureEnabled(featureName) == true);
    }
    
    /// <summary>
    /// Apply validation rule only for specific tenant.
    /// Enables tenant-specific validation rules in multi-tenant scenarios.
    /// </summary>
    /// <typeparam name="TProperty">Type of the property being validated</typeparam>
    /// <param name="ruleBuilder">The rule builder to conditionally apply</param>
    /// <param name="tenantId">Target tenant identifier</param>
    /// <returns>Rule builder options for further configuration</returns>
    /// <example>
    /// RuleFor(x => x.Department)
    ///     .NotEmpty()
    ///     .WhenTenant("enterprise-tenant");
    /// </example>
    protected IRuleBuilderOptions<T, TProperty> WhenTenant<TProperty>(
        IRuleBuilder<T, TProperty> ruleBuilder, 
        string tenantId)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);
        
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new ArgumentException("Tenant ID cannot be null or empty", nameof(tenantId));
        }
        
        return ruleBuilder.When(_ => string.Equals(Context?.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Apply validation rule only when metadata contains specific key-value pair.
    /// Enables flexible metadata-driven validation scenarios.
    /// </summary>
    /// <typeparam name="TProperty">Type of the property being validated</typeparam>
    /// <param name="ruleBuilder">The rule builder to conditionally apply</param>
    /// <param name="key">Metadata key to check</param>
    /// <param name="expectedValue">Expected metadata value</param>
    /// <returns>Rule builder options for further configuration</returns>
    /// <example>
    /// RuleFor(x => x.CompanyName)
    ///     .NotEmpty()
    ///     .WhenMetadata("UserType", "Business");
    /// </example>
    protected IRuleBuilderOptions<T, TProperty> WhenMetadata<TProperty>(
        IRuleBuilder<T, TProperty> ruleBuilder, 
        string key, 
        object expectedValue)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);
        ArgumentNullException.ThrowIfNull(expectedValue);
        
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Metadata key cannot be null or empty", nameof(key));
        }
        
        return ruleBuilder.When(_ => IsMetadataValueMatch(key, expectedValue));
    }
    
    /// <summary>
    /// Apply validation rule only for specific user.
    /// Enables user-specific validation rules and personalized validation logic.
    /// </summary>
    /// <typeparam name="TProperty">Type of the property being validated</typeparam>
    /// <param name="ruleBuilder">The rule builder to conditionally apply</param>
    /// <param name="userId">Target user identifier</param>
    /// <returns>Rule builder options for further configuration</returns>
    /// <example>
    /// RuleFor(x => x.AdminSettings)
    ///     .NotNull()
    ///     .WhenUser("admin-user-id");
    /// </example>
    protected IRuleBuilderOptions<T, TProperty> WhenUser<TProperty>(
        IRuleBuilder<T, TProperty> ruleBuilder, 
        string userId)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);
        
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        }
        
        return ruleBuilder.When(_ => string.Equals(Context?.UserId, userId, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Apply validation rule based on custom context predicate.
    /// Provides maximum flexibility for complex context-aware validation scenarios.
    /// </summary>
    /// <typeparam name="TProperty">Type of the property being validated</typeparam>
    /// <param name="ruleBuilder">The rule builder to conditionally apply</param>
    /// <param name="contextPredicate">Predicate function using validation context</param>
    /// <returns>Rule builder options for further configuration</returns>
    /// <example>
    /// RuleFor(x => x.AdvancedSettings)
    ///     .NotNull()
    ///     .WhenContext(ctx => ctx.IsFeatureEnabled("AdvancedMode") && ctx.TenantId == "premium");
    /// </example>
    protected IRuleBuilderOptions<T, TProperty> WhenContext<TProperty>(
        IRuleBuilder<T, TProperty> ruleBuilder, 
        Func<IValidationContext?, bool> contextPredicate)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);
        ArgumentNullException.ThrowIfNull(contextPredicate);
        
        return ruleBuilder.When(_ => contextPredicate(Context));
    }
    
    /// <summary>
    /// Get strongly-typed metadata value with fallback.
    /// Provides safe access to context metadata with default values.
    /// </summary>
    /// <typeparam name="TValue">Type of the metadata value</typeparam>
    /// <param name="key">Metadata key to retrieve</param>
    /// <param name="defaultValue">Default value if key not found or conversion fails</param>
    /// <returns>Metadata value or default</returns>
    protected TValue GetMetadataOrDefault<TValue>(string key, TValue defaultValue = default!)
    {
        return Context?.GetMetadata<TValue>(key) ?? defaultValue;
    }
    
    /// <summary>
    /// Check if current request has specific feature enabled.
    /// Convenience method for feature flag checks in validation logic.
    /// </summary>
    /// <param name="featureName">Feature flag name to check</param>
    /// <returns>True if feature is enabled</returns>
    protected bool HasFeatureEnabled(string featureName)
    {
        return Context?.IsFeatureEnabled(featureName) == true;
    }
    
    /// <summary>
    /// Check if current request is from specific tenant.
    /// Convenience method for tenant-specific validation logic.
    /// </summary>
    /// <param name="tenantId">Tenant identifier to check</param>
    /// <returns>True if request is from specified tenant</returns>
    protected bool IsFromTenant(string tenantId)
    {
        return string.Equals(Context?.TenantId, tenantId, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Check if metadata value matches expected value with type-safe comparison.
    /// </summary>
    /// <param name="key">Metadata key to check</param>
    /// <param name="expectedValue">Expected value to compare against</param>
    /// <returns>True if metadata value matches expected value</returns>
    private bool IsMetadataValueMatch(string key, object expectedValue)
    {
        if (Context == null)
        {
            return false;
        }
        
        var actualValue = Context.GetMetadata<object>(key);
        
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
}