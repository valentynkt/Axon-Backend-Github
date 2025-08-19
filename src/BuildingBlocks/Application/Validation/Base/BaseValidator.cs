using FluentValidation;
using FluentValidation.Results;

namespace BuildingBlocks.Application.Validation.Base;

/// <summary>
/// Base validator with optimized performance settings
/// </summary>
public abstract class BaseValidator<T> : AbstractValidator<T>
{
    protected BaseValidator()
    {
        // Performance optimization: Stop on first failure per property  
        ClassLevelCascadeMode = CascadeMode.Stop;
        
        // Enable async validation support
        SetupAsyncValidation();
    }

    /// <summary>
    /// Override this method to configure async validation patterns
    /// </summary>
    protected virtual void SetupAsyncValidation()
    {
        // Default implementation - override in derived classes if needed
    }

    /// <summary>
    /// Pre-validate method for fast-fail scenarios
    /// Call this before expensive validation operations
    /// </summary>
    protected virtual bool PreValidate(T instance)
    {
        // Basic null check
        if (instance == null) return false;
        
        // Override in derived classes for more specific checks
        return true;
    }


}