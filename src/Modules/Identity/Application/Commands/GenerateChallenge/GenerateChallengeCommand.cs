using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Commands.GenerateChallenge;

public sealed record GenerateChallengeCommand(
    NetworkEnvironment NetworkEnvironment,
    string ChainId,
    string WalletAddress,
    string? Audience = null
) : IRequest<Result<GenerateChallengeResult, Error>>;