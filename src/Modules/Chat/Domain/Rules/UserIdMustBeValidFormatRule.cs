using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Errors;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Business rule that ensures a AxonUserId string can be parsed to a valid Guid.
/// </summary>
internal sealed class UserIdMustBeValidFormatRule : BusinessRule
{
    private readonly string? _userIdString;

    public UserIdMustBeValidFormatRule(string? userIdString)
        : base(
            message: ChatDomainErrors.Authentication.InvalidUserIdMessage,
            code: ChatDomainErrors.Authentication.InvalidUserIdCode)
    {
        _userIdString = userIdString;
    }

    public override bool IsBroken() => 
        string.IsNullOrWhiteSpace(_userIdString) || 
        !Guid.TryParse(_userIdString, out _);

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}