using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Guid-based StrongId base class with helpful factory helpers for derived IDs.
/// </summary>
public abstract record GuidStrongId : StrongId<Guid>
{
    protected GuidStrongId(Guid value) : base(value) { }

    /// <summary>Create a new Guid-based ID (for concrete derived types).</summary>
    protected static TDerived New<TDerived>(Func<Guid, TDerived> factory)
        where TDerived : GuidStrongId
        => factory(Guid.NewGuid());

    /// <summary>Create from "D" or any Guid.Parse format with validation.</summary>
    protected static Result<TDerived> FromString<TDerived>(string value, Func<Guid, TDerived> factory)
        where TDerived : GuidStrongId
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<TDerived>.Failure(Error.Validation($"{typeof(TDerived).Name} cannot be empty.", $"{typeof(TDerived).Name}_EMPTY"));

        if (!Guid.TryParse(value, out var parsed))
            return Result<TDerived>.Failure(Error.Validation($"Invalid {typeof(TDerived).Name} format.", $"{typeof(TDerived).Name}_INVALID"));

        if (parsed == Guid.Empty)
            return Result<TDerived>.Failure(Error.Validation($"{typeof(TDerived).Name} cannot be empty GUID.", $"{typeof(TDerived).Name}_EMPTY_GUID"));

        return Result<TDerived>.Success(factory(parsed));
    }

    /// <summary>Try-create without throwing.</summary>
    protected static bool TryFromString<TDerived>(string value, Func<Guid, TDerived> factory, out TDerived? id)
        where TDerived : GuidStrongId
    {
        id = null;
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty) return false;
        id = factory(parsed);
        return true;
    }
}