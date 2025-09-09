using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.DTOs.Requests;

/// <summary>
/// Request object for attaching a wallet to a principal during credential operations.
/// </summary>
public sealed record AttachWalletRequest(
    string ChainId,
    string RawAddress,
    string ProofType,
    string? AccessMode = null,
    string? Label = null,
    bool Verify = true,
    bool SetAsDefault = false
);