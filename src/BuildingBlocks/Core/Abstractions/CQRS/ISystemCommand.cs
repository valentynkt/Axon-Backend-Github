namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Marker for internal/system-level commands (maintenance, migrations, etc.).
/// Useful for policies, pipelines, or security.
/// </summary>
public interface ISystemCommand
{
}