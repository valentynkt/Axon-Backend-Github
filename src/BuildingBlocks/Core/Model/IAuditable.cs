namespace BuildingBlocks.Core.Model;

/// <summary>
/// Represents an entity that supports audit tracking.
/// Follows SRP by focusing solely on audit trail concerns.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// Gets or sets when this entity was created.
    /// </summary>
    DateTime? CreatedAt { get; set; }
    
    /// <summary>
    /// Gets or sets who created this entity.
    /// </summary>
    long? CreatedBy { get; set; }
    
    /// <summary>
    /// Gets or sets when this entity was last modified.
    /// </summary>
    DateTime? LastModified { get; set; }
    
    /// <summary>
    /// Gets or sets who last modified this entity.
    /// </summary>
    long? LastModifiedBy { get; set; }
}