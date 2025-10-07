using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Commands.GenerateChallenge;

public sealed record GenerateChallengeCommand(
    string ChainId,      // e.g. "solana" or "solana:mainnet"
    string WalletAddress,
    string? Audience = null
) : IRequest<Result<GenerateChallengeResult, Error>>;