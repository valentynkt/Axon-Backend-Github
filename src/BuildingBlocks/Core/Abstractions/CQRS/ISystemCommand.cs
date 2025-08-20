// /BuildingBlocks/Core/Abstractions/CQRS/ISystemCommand.cs
#nullable enable
namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Marker for trusted/system commands so pipelines can treat them specially (skip validation, etc.).
/// </summary>
public interface ISystemCommand : ICommand { }