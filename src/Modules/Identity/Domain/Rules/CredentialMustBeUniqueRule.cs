using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that identity credentials are unique within a principal by (ProviderType, Issuer, Subject).
/// </summary>
internal sealed class CredentialMustBeUniqueRule : BusinessRule
{
    private readonly ProviderType _providerType;
    private readonly string _issuer;
    private readonly string _subject;
    private readonly IEnumerable<IdentityCredential> _existingCredentials;

    public CredentialMustBeUniqueRule(
        ProviderType providerType,
        string issuer,
        string subject,
        IEnumerable<IdentityCredential> existingCredentials)
        : base(
            message: $"Credential for provider '{providerType.Value}', issuer '{issuer}', subject '{subject}' already exists.",
            code: "IDENTITY.CREDENTIAL.DUPLICATE")
    {
        _providerType = providerType;
        _issuer = issuer;
        _subject = subject;
        _existingCredentials = existingCredentials;
    }

    public override bool IsBroken()
    {
        return _existingCredentials.Any(c => 
            !c.IsDeleted && c.Matches(_providerType, _issuer, _subject));
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}