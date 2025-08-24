using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Errors;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Business rule that ensures an AI Response ID is valid (not null or whitespace).
/// </summary>
internal sealed class AiResponseMustBeValidRule : BusinessRule
{
    private readonly string? _aiResponseId;

    public AiResponseMustBeValidRule(string? aiResponseId)
        : base(
            message: ChatDomainErrors.AiProcessing.InvalidResponseIdMessage,
            code: ChatDomainErrors.AiProcessing.InvalidResponseIdCode)
    {
        _aiResponseId = aiResponseId;
    }

    public override bool IsBroken() => string.IsNullOrWhiteSpace(_aiResponseId);

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}