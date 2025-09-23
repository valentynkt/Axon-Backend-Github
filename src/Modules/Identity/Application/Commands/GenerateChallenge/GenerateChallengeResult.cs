namespace Axon.Modules.Identity.Application.Commands.GenerateChallenge;

public sealed record GenerateChallengeResult(
    string Message,
    string NetworkEnvironment,
    string ChainId,
    string Address,
    long IssuedAt,
    long ExpiresAt,
    string Nonce,
    string? Audience
);