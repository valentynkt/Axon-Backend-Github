using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that a credential has a valid provider type for the current operation context.
/// While ProviderType has built-in validation, this rule adds context-specific validation
/// (e.g., certain operations may only be allowed for specific provider types).
/// </summary>
internal sealed class CredentialMustHaveValidProviderRule : BusinessRule
{
    private readonly ProviderType _providerType;
    private readonly string _operationContext;

    public CredentialMustHaveValidProviderRule(ProviderType providerType, string operationContext = "general")
        : base(
            message: $"Provider type '{providerType.Value}' is not valid for operation '{operationContext}'.",
            code: "IDENTITY.CREDENTIAL.PROVIDER.INVALID_FOR_CONTEXT")
    {
        _providerType = providerType;
        _operationContext = operationContext;
    }

    public override bool IsBroken()
    {
        return _operationContext switch
        {
            "wallet_linking" => !(_providerType.IsDynamic || _providerType.IsSiws),
            "api_access" => !_providerType.IsServiceApi,
            "general" => false, // All provider types are valid for general operations
            _ => true // Unknown operation context is invalid
        };
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}