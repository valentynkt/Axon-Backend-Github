namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Canonical challenge payload the wallet should sign.
/// </summary>
public sealed record ChallengeResponseDto(
    string Message,         // full canonical message to sign
    string ChainId,         // compound format e.g. "solana-mainnet"
    string Address,         // wallet address
    long IssuedAt,          // unix seconds
    long ExpiresAt,         // unix seconds
    string Nonce,           // random nonce for replay protection
    string? Audience,       // optional audience echo
    string Mac,             // HMAC-SHA256 in Base64Url
    string Mkv              // MAC key version like "v1"
);