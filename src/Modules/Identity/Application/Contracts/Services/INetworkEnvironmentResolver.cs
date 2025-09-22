using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for resolving NetworkEnvironment from various sources
/// </summary>
public interface INetworkEnvironmentResolver
{
    /// <summary>
    /// Resolves NetworkEnvironment from Dynamic's EnvironmentId
    /// </summary>
    /// <param name="dynamicEnvironmentId">Dynamic.xyz's environment identifier</param>
    /// <returns>Resolved NetworkEnvironment or default (mainnet)</returns>
    Result<NetworkEnvironment, Error> ResolveFromDynamicEnvironment(string? dynamicEnvironmentId);

    /// <summary>
    /// Resolves NetworkEnvironment from request context (fallback to mainnet)
    /// </summary>
    /// <returns>Default NetworkEnvironment (mainnet for production)</returns>
    NetworkEnvironment ResolveDefault();
}