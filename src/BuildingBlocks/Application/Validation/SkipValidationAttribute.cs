using System.Diagnostics;

namespace BuildingBlocks.Application.Validation;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
[DebuggerDisplay("SkipValidation (Reason = {Reason})")]
public sealed class SkipValidationAttribute : Attribute
{
    public string? Reason { get; }
    public SkipValidationAttribute(string? reason = null) => Reason = reason;
}