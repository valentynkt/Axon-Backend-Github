// /BuildingBlocks/Application/Validation/ValidationErrorMapper.cs
#nullable enable
using BuildingBlocks.Core.Diagnostics.Errors;
using FluentValidation.Results;

namespace BuildingBlocks.Application.Validation;

/// <summary>
/// Maps FluentValidation failures to a rich Error/aggregate Error.
/// Kept small, shared by the validation behavior and (optionally) endpoints.
/// </summary>
public static class ValidationErrorMapper
{
    public static Error FromFailures<TRequest>(IReadOnlyCollection<ValidationFailure> failures, TRequest request)
    {
        var errors = failures
            .GroupBy(f => string.IsNullOrWhiteSpace(f.PropertyName) ? "_global" : f.PropertyName)
            .Select(g =>
            {
                var msg   = string.Join("; ", g.Select(x => x.ErrorMessage));
                var codes = g.Select(x => x.ErrorCode).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToArray();

                return Error.Validation(
                    message: msg,
                    code:    codes.FirstOrDefault() ?? "VALIDATION_ERROR",
                    metadata: new Dictionary<string, object>
                    {
                        ["field"]     = g.Key,
                        ["count"]     = g.Count(),
                        ["codes"]     = codes,
                        ["severity"]  = g.Max(x => x.Severity).ToString(),
                        ["samples"]   = g.Select(x => x.AttemptedValue ?? "<null>").Take(3).ToArray(),
                        ["request"]   = request?.GetType().FullName ?? request?.GetType().Name ?? "Unknown"
                    });
            })
            .ToArray();

        return errors.Length == 1 ? errors[0] : Error.Aggregate(errors);
    }
}