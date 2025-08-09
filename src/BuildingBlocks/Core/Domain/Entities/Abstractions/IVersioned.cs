namespace BuildingBlocks.Core.Domain.Entities.Abstractions;

/// <summary>
/// Opt-in trait for optimistic concurrency using a version token.
/// Generic to stay provider-agnostic (e.g., uint/xmin, ulong/rowversion, byte[]).
/// </summary>
public interface IVersioned<TVersion>
{
    TVersion Version { get; }
}

/// <summary>
/// Default alias for current stack (Postgres xmin/uint).
/// If you migrate providers, you can switch specific entities to another TVersion.
/// </summary>
public interface IVersioned : IVersioned<uint> { }