using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Service for resolving NetworkEnvironment from various sources
/// Maps Dynamic.xyz environment IDs to blockchain network environments
/// </summary>
/// TODO: This is probably wrong Implementation EnvirnomentId from dynamic is just relatde to dynamic not NetworkEnvironmen
public class NetworkEnvironmentResolver : INetworkEnvironmentResolver
{
    private readonly ILogger<NetworkEnvironmentResolver> _logger;

    // Mapping from Dynamic environment IDs to NetworkEnvironments
    // This would typically come from configuration or database
    private static readonly Dictionary<string, NetworkEnvironment> DynamicEnvironmentMap = new()
    {
        // Production Dynamic environments → mainnet
        { "production", NetworkEnvironment.Mainnet },
        { "prod", NetworkEnvironment.Mainnet },
        { "mainnet", NetworkEnvironment.Mainnet },

        // Development Dynamic environments → devnet
        { "development", NetworkEnvironment.Devnet },
        { "dev", NetworkEnvironment.Devnet },
        { "devnet", NetworkEnvironment.Devnet },
        { "sandbox", NetworkEnvironment.Devnet },

        // Test Dynamic environments → testnet
        { "test", NetworkEnvironment.Testnet },
        { "testing", NetworkEnvironment.Testnet },
        { "testnet", NetworkEnvironment.Testnet },
        { "staging", NetworkEnvironment.Testnet }
    };

    public NetworkEnvironmentResolver(ILogger<NetworkEnvironmentResolver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves NetworkEnvironment from Dynamic's EnvironmentId
    /// </summary>
    /// <param name="dynamicEnvironmentId">Dynamic.xyz's environment identifier</param>
    /// <returns>Resolved NetworkEnvironment or default (mainnet)</returns>
    public Result<NetworkEnvironment, Error> ResolveFromDynamicEnvironment(string? dynamicEnvironmentId)
    {
        if (string.IsNullOrWhiteSpace(dynamicEnvironmentId))
        {
            _logger.LogDebug("No Dynamic environment ID provided, defaulting to mainnet");
            return Result.Success<NetworkEnvironment, Error>(NetworkEnvironment.Mainnet);
        }

        var normalizedId = dynamicEnvironmentId.Trim().ToLowerInvariant();

        // Check for direct mapping
        if (DynamicEnvironmentMap.TryGetValue(normalizedId, out var mappedEnvironment))
        {
            _logger.LogDebug("Mapped Dynamic environment '{DynamicEnvironmentId}' to NetworkEnvironment '{NetworkEnvironment}'",
                dynamicEnvironmentId, mappedEnvironment.Value);
            return Result.Success<NetworkEnvironment, Error>(mappedEnvironment);
        }

        // Check if it's already a valid NetworkEnvironment value
        var networkEnvResult = NetworkEnvironment.Create(normalizedId);
        if (networkEnvResult.IsSuccess)
        {
            _logger.LogDebug("Dynamic environment '{DynamicEnvironmentId}' is valid NetworkEnvironment",
                dynamicEnvironmentId);
            return networkEnvResult;
        }

        // UUID or unknown environment ID → default to mainnet with warning
        _logger.LogWarning("Unknown Dynamic environment ID '{DynamicEnvironmentId}', defaulting to mainnet. " +
                          "Consider adding mapping if this is a known environment.",
            dynamicEnvironmentId);

        return Result.Success<NetworkEnvironment, Error>(NetworkEnvironment.Mainnet);
    }

    /// <summary>
    /// Resolves NetworkEnvironment from request context (fallback to mainnet)
    /// </summary>
    /// <returns>Default NetworkEnvironment (mainnet for production)</returns>
    public NetworkEnvironment ResolveDefault()
    {
        // In a production environment, this would typically be mainnet
        // Could be configurable based on deployment environment
        _logger.LogDebug("Using default NetworkEnvironment: mainnet");
        return NetworkEnvironment.Mainnet;
    }
}