using BuildingBlocks.Core.Abstractions.CQRS;

namespace Axon.Modules.Identity.Application.Common.Commands;

/// <summary>
/// Base class for all Identity module commands.
/// Provides common functionality for authenticated command execution.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command</typeparam>
public abstract record IdentityBaseCommand<TResponse> : RequestBase, ICommand<TResponse>, IAuthenticatedRequest
    where TResponse : notnull;