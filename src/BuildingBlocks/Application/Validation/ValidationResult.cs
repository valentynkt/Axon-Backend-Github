namespace BuildingBlocks.Application.Validation;

public sealed class ValidationResult
{
    public static readonly ValidationResult Success = new([]);

    public IReadOnlyList<ValidationError> Errors { get; }
    public bool IsValid => Errors.Count == 0;

    public ValidationResult(IReadOnlyList<ValidationError> errors) => Errors = errors;

    public static ValidationResult Combine(params ValidationResult[] results) =>
        new(results.SelectMany(r => r.Errors).ToArray());

    public ValidationResult With(ValidationError error) =>
        new(Errors.Concat(new[] { error }).ToArray());

    public ValidationResult GroupByField() =>
        new(Errors
            .GroupBy(e => (e.Field ?? "_global", e.Code))
            .Select(g => g.Count() == 1 ? g.First()
                : g.First() with { Message = string.Join("; ", g.Select(x => x.Message)) })
            .ToArray());
}