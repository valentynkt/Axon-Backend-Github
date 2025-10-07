using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Commands.VerifyWalletSignature;

public sealed record VerifyWalletSignatureCommand(
    string ChainId,      // Compound format e.g. "solana-mainnet"
    string Address,
    string SignedMessage,
    string Signature
) : IRequest<Result<VerifyWalletSignatureResult, Error>>;