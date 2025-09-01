using Axon.Modules.Identity.Domain.Abstractions;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that a credential cannot belong to another principal.
/// This rule requires repository access for cross-aggregate validation.
/// </summary>
internal sealed class CredentialCannotBelongToOtherPrincipalRule : BusinessRule
{
    private readonly ProviderType _providerType;
    private readonly string _issuer;
    private readonly string _subject;
    private readonly AxonId _currentPrincipalId;
    private readonly IAxonPrincipalRepository _repository;

    public CredentialCannotBelongToOtherPrincipalRule(
        ProviderType providerType,
        string issuer,
        string subject,
        AxonId currentPrincipalId,
        IAxonPrincipalRepository repository)
        : base(
            message: $"Credential for provider '{providerType.Value}', issuer '{issuer}', subject '{subject}' already belongs to another principal.",
            code: "IDENTITY.CREDENTIAL.BELONGS_TO_OTHER")
    {
        _providerType = providerType;
        _issuer = issuer;
        _subject = subject;
        _currentPrincipalId = currentPrincipalId;
        _repository = repository;
    }

    public override bool IsBroken()
    {
        // Synchronous version throws - should use async version
        throw new InvalidOperationException("Use IsBrokenAsync for cross-aggregate validation.");
    }

    public override async ValueTask<bool> IsBrokenAsync(CancellationToken ct = default)
    {
        var existingPrincipal = await _repository.FindByCredentialAsync(
            _providerType, _issuer, _subject, ct);

        if (existingPrincipal is null)
            return false;

        // It's broken if the credential belongs to a different principal
        return existingPrincipal.Id != _currentPrincipalId;
    }
}