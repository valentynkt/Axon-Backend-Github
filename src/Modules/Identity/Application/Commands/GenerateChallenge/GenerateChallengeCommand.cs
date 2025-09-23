using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Commands.GenerateChallenge;

public sealed record GenerateChallengeCommand(
    string ChainId,      // Compound format e.g. "solana-mainnet"
    string WalletAddress,
    string? Audience = null
) : IRequest<Result<GenerateChallengeResult, Error>>;