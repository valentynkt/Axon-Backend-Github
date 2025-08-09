using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using FluentValidation;
using FluentValidation.Results;

namespace BuildingBlocks.Application.Exceptions;

/// <summary>
/// Exception for validation errors with FluentValidation support.
/// Moved to Application layer to avoid FluentValidation dependency in Core layer.
/// </summary>
public sealed class ValidationException : DomainException
{
    public IReadOnlyList<ValidationFailure> ValidationFailures { get; }
    
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base(ConvertFailuresToErrors(failures))
    {
        ValidationFailures = failures.ToList();
    }
    
    public ValidationException(string message, IEnumerable<ValidationFailure> failures)
        : base(message, ConvertFailuresToErrors(failures))
    {
        ValidationFailures = failures.ToList();
    }
    
    private static IEnumerable<Error> ConvertFailuresToErrors(IEnumerable<ValidationFailure> failures)
    {
        return failures.Select(f => 
        {
            var metadata = new Dictionary<string, object>
            {
                ["PropertyName"] = f.PropertyName,
                ["AttemptedValue"] = f.AttemptedValue ?? "null"
            };
            
            var error = Error.Validation(
                f.ErrorMessage,
                f.ErrorCode ?? "VALIDATION_ERROR", 
                metadata);
                
            // Map FluentValidation severity
            if (f.Severity == Severity.Warning)
                error = error.WithSeverity(ErrorSeverity.Warning);
            else if (f.Severity == Severity.Info)
                error = error.WithSeverity(ErrorSeverity.Info);
                
            return error;
        });
    }
    
    public bool HasErrorsForProperty(string propertyName)
    {
        return ValidationFailures.Any(f => 
            f.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    }
    
    public IEnumerable<ValidationFailure> GetErrorsForProperty(string propertyName)
    {
        return ValidationFailures.Where(f => 
            f.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    }
    
    public IEnumerable<string> GetErrorMessagesForProperty(string propertyName)
    {
        return GetErrorsForProperty(propertyName).Select(f => f.ErrorMessage);
    }

}