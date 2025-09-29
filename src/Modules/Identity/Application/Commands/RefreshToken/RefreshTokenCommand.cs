using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Commands.RefreshToken;

/// <summary>
/// Command to refresh an access token using a valid refresh token
/// </summary>
/// <param name="RefreshToken">The refresh token to validate and use for generating new tokens</param>
public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<RefreshTokenResult, Error>>;

/// <summary>
/// Result of refresh token operation
/// </summary>
/// <param name="AccessToken">New access token</param>
/// <param name="RefreshToken">New refresh token</param>
/// <param name="ExpiresAt">Access token expiration</param>
/// <param name="RefreshExpiresAt">Refresh token expiration</param>
public record RefreshTokenResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    DateTime RefreshExpiresAt);