using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Errors;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Business rule that ensures a user is authenticated.
/// </summary>
internal sealed class UserMustBeAuthenticatedRule : BusinessRule
{
    private readonly bool _isAuthenticated;
    private readonly string? _userId;

    public UserMustBeAuthenticatedRule(bool isAuthenticated, string? userId)
        : base(
            message: ChatDomainErrors.Authentication.UnauthenticatedMessage,
            code: ChatDomainErrors.Authentication.UnauthenticatedCode)
    {
        _isAuthenticated = isAuthenticated;
        _userId = userId;
    }

    public override bool IsBroken() => !_isAuthenticated || string.IsNullOrWhiteSpace(_userId);

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}