namespace BuildingBlocks.Application.Validation;

/// <summary>
/// Marker interface for validators that can utilize metadata-aware validation context.
/// Validators implementing this interface will receive IValidationContext during validation
/// enabling access to tenant information, feature flags, and custom metadata for
/// context-aware validation rules in Epic 04.
/// </summary>
public interface IMetadataValidator
{
    /// <summary>
    /// Sets the validation context containing request metadata and trace information.
    /// Called by ValidationBehavior before validation begins.
    /// Implementations should store the context for use in validation rules.
    /// </summary>
    /// <param name="context">The validation context with metadata access</param>
    void SetContext(IValidationContext context);
}