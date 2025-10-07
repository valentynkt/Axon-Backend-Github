namespace Axon.Modules.Identity.Application.Commands.GenerateChallenge;

public sealed record GenerateChallengeResult(
    string Message,
    string ChainId,      // Compound format e.g. "solana:mainnet"
    string Address,
    long IssuedAt,
    long ExpiresAt,
    string Nonce,
    string? Audience
);