namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Request to generate a wallet sign-in challenge (manual/SIWS flow).
/// </summary>
public sealed record ChallengeRequestDto(
    string ChainId,       // e.g. "solana" or "solana:mainnet"
    string WalletAddress,       // wallet address
    string? Audience      // optional: intended audience to bind the challenge
);