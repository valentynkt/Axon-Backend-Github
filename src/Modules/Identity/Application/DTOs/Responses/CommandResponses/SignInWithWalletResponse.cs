namespace Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;

/// <summary>
/// Status enumeration for SignInWithWallet operation.
/// </summary>
public enum SignInWithWalletStatus
{
    /// <summary>
    /// New principal was created and wallet linked.
    /// </summary>
    PrincipalCreatedAndWalletLinked,

    /// <summary>
    /// Existing principal found by wallet ownership.
    /// </summary>
    ExistingPrincipalFoundByWallet
}

/// <summary>
/// Response for SignInWithWallet command.
/// </summary>
public sealed record SignInWithWalletResponse(
    PrincipalDto Principal,
    WalletOwnershipDto WalletOwnership,
    bool? AppliedDefault,
    string? DefaultNotAppliedReason,
    SignInWithWalletStatus Status
);