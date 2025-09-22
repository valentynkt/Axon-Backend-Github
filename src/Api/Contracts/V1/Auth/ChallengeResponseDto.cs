namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Canonical challenge payload the wallet should sign.
/// </summary>
public sealed record ChallengeResponseDto(
    string Message,         // full canonical message to sign
    string NetworkEnvironment,     // environment used to build/validate the challenge
    string ChainId,         // e.g. "solana"
    string Address,         // wallet address
    long IssuedAt,          // unix seconds
    long ExpiresAt,         // unix seconds
    string Nonce,           // random nonce for replay protection
    string? Audience        // optional audience echo
);