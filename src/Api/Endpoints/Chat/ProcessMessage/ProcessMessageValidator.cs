using Axon.Api.Contracts.Chat;
using FastEndpoints;
using FluentValidation;

namespace Axon.Api.Endpoints.Chat.ProcessMessage;

/// <summary>
/// Validator for ProcessMessageRequest using FastEndpoints validation
/// </summary>
public sealed class ProcessMessageRequestValidator : Validator<ProcessMessageRequest>
{
    public ProcessMessageRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message cannot be empty")
            .MaximumLength(10000)
            .WithMessage("Message cannot exceed 10,000 characters");
    }
}