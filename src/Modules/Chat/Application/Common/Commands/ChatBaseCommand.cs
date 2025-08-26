using BuildingBlocks.Core.Abstractions.CQRS;

namespace Axon.Modules.Chat.Application.Common.Commands;

/// <summary>
/// Base class for all Chat module commands.
/// Provides common functionality for authenticated command execution.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command</typeparam>
public abstract record ChatBaseCommand<TResponse> : RequestBase, ICommand<TResponse>, IAuthenticatedRequest
    where TResponse : notnull;