using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Int-based StrongId base class with validation helpers for positive identifiers.
/// </summary>
public abstract record IntStrongId : StrongId<int>
{
    protected IntStrongId(int value) : base(value)
    {
        if (value <= 0)
            throw new ArgumentException("Integer StrongId must be positive.", nameof(value));
    }

    /// <summary>Create from string with positivity validation.</summary>
    protected static Result<TDerived> FromString<TDerived>(string value, Func<int, TDerived> factory)
        where TDerived : IntStrongId
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<TDerived>.Failure(Error.Validation($"{typeof(TDerived).Name} cannot be empty.", $"{typeof(TDerived).Name}_EMPTY"));

        if (!int.TryParse(value, out var parsed))
            return Result<TDerived>.Failure(Error.Validation($"Invalid {typeof(TDerived).Name} format.", $"{typeof(TDerived).Name}_INVALID"));

        if (parsed <= 0)
            return Result<TDerived>.Failure(Error.Validation($"{typeof(TDerived).Name} must be positive.", $"{typeof(TDerived).Name}_NOT_POSITIVE"));

        return Result<TDerived>.Success(factory(parsed));
    }

    /// <summary>Try-create without throwing.</summary>
    protected static bool TryFromString<TDerived>(string value, Func<int, TDerived> factory, out TDerived? id)
        where TDerived : IntStrongId
    {
        id = null;
        if (!int.TryParse(value, out var parsed) || parsed <= 0) return false;
        id = factory(parsed);
        return true;
    }
}