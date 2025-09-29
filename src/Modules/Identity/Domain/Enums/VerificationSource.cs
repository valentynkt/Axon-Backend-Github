namespace Axon.Modules.Identity.Domain.Enums;

/// <summary>
/// Source of wallet ownership verification, used for tie-breaking in principal resolution.
/// </summary>
public enum VerificationSource
{
    /// <summary>
    /// Verified through Dynamic provider attestation
    /// </summary>
    DynamicAttested = 1,

    /// <summary>
    /// Verified through direct message signature
    /// </summary>
    DirectSignatureMsg = 2,

    /// <summary>
    /// Verified through transaction signature
    /// </summary>
    DirectSignatureTx = 3,

    /// <summary>
    /// Watch-only wallet without verification
    /// </summary>
    WatchOnly = 4
}