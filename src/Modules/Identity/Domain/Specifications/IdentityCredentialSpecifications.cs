using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Specifications;

/// <summary>
/// Specification for active identity credentials (not deleted).
/// </summary>
public sealed class ActiveCredentialSpec : Specification<IdentityCredential>
{
    public ActiveCredentialSpec()
    {
        Query.Where(c => !c.IsDeleted);
    }
}

/// <summary>
/// Specification for credentials of a specific provider type.
/// </summary>
public sealed class ProviderCredentialSpec : Specification<IdentityCredential>
{
    public ProviderCredentialSpec(ProviderType providerType)
    {
        Query.Where(c => c.ProviderType == providerType);
    }
}

/// <summary>
/// Specification for credentials belonging to a specific principal.
/// </summary>
public sealed class PrincipalCredentialSpec : Specification<IdentityCredential>
{
    public PrincipalCredentialSpec(AxonId principalId)
    {
        Query.Where(c => c.AxonId == principalId);
    }
}

/// <summary>
/// Specification for a unique credential (provider, issuer, subject combination).
/// </summary>
public sealed class UniqueCredentialSpec : Specification<IdentityCredential>
{
    public UniqueCredentialSpec(ProviderType providerType, string issuer, string subject)
    {
        Query.Where(c => c.ProviderType == providerType && 
                        c.Issuer == issuer && 
                        c.Subject == subject);
    }
}

/// <summary>
/// Specification for credentials that have been seen recently.
/// </summary>
public sealed class RecentlySeenCredentialSpec : Specification<IdentityCredential>
{
    public RecentlySeenCredentialSpec(DateTimeOffset cutoffDate)
    {
        Query.Where(c => c.LastSeenAt >= cutoffDate);
    }
}

/// <summary>
/// Specification for credentials with a specific environment ID.
/// </summary>
public sealed class EnvironmentCredentialSpec : Specification<IdentityCredential>
{
    public EnvironmentCredentialSpec(string environmentId)
    {
        Query.Where(c => c.EnvironmentId == environmentId);
    }
}