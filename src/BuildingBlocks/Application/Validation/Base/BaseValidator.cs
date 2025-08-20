// /BuildingBlocks/Application/Validation/Base/BaseValidator.cs
#nullable enable
using FluentValidation;

namespace BuildingBlocks.Application.Validation.Base;

/// <summary>
/// Base validator with conservative, fast-fail defaults.
/// </summary>
public abstract class BaseValidator<T> : AbstractValidator<T>
{
    protected BaseValidator()
    {
        // Stop per rule and per class (keeps messages concise and fast)
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode  = CascadeMode.Stop;
    }
}