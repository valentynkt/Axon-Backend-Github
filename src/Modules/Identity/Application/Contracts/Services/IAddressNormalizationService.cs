using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for normalizing blockchain addresses according to chain-specific rules.
/// Ensures consistent address formatting and validation across the application.
/// </summary>
public interface IAddressNormalizationService
{
    /// <summary>
    /// Normalizes an address according to the specified chain's rules.
    /// </summary>
    /// <param name="chainId">The blockchain chain identifier</param>
    /// <param name="address">The address to normalize</param>
    /// <returns>A Result containing the normalized address or an error if validation fails</returns>
    Result<Address, Error> NormalizeAddress(string chainId, string address);
}