namespace BuildingBlocks.Application.Validation;

public enum ValidationSeverity 
{ 
    Info, 
    Warning, 
    Error 
}

public sealed record ValidationError(
    string Code,
    string Message,
    string? Field = null,
    ValidationSeverity Severity = ValidationSeverity.Error,
    object? AttemptedValue = null,
    IReadOnlyDictionary<string, object>? Metadata = null);