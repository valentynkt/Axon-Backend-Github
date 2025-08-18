// File: /Api/Endpoints/Chat/ProcessMessageValidator.cs

using Axon.Api.Contracts.Chat;
using FastEndpoints;
using FluentValidation;

namespace Axon.Api.Endpoints.Chat;

public sealed class ProcessMessageValidator : Validator<ProcessMessageRequest>
{
    public ProcessMessageValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(8000);

        RuleFor(x => x.McpServerUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) || Uri.IsWellFormedUriString(url, UriKind.Absolute))
            .WithMessage("McpServerUrl must be a valid absolute URL.");
    }
}