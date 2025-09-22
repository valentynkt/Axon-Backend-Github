using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for authentication challenge generation and validation
/// </summary>
public interface IChallengeService
{
    /// <summary>
    /// Generates an authentication challenge for the given address
    /// </summary>
    Task<Result<AuthenticationChallenge, Error>> GenerateChallengeAsync(
        string address,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a token has been used for replay protection
    /// </summary>
    Task<Result<Unit, Error>> CheckAndMarkTokenUsedAsync(
        string jti,
        CancellationToken cancellationToken = default);
}