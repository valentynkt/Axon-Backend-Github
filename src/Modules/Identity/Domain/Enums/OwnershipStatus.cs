namespace Axon.Modules.Identity.Domain.Enums;

/// <summary>
/// Ownership status for wallet-principal relationships - pending/verified/revoked.
/// </summary>
public enum OwnershipStatus
{
    Pending = 0,
    Verified = 1,
    Revoked = 2
}