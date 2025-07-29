using FluentValidation;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Validator for ProcessMessageCommand
/// </summary>
public sealed class ProcessMessageValidator : AbstractValidator<ProcessMessageCommand>
{
    private const int MaxMessageLength = 4000;

    public ProcessMessageValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message cannot be empty")
            .MaximumLength(MaxMessageLength)
            .WithMessage($"Message cannot exceed {MaxMessageLength} characters");

        When(x => !string.IsNullOrWhiteSpace(x.McpServerUrl), () =>
        {
            RuleFor(x => x.McpServerUrl)
                .Must(BeValidHttpsUrl!)
                .WithMessage("MCP server URL must be a valid HTTPS URL");
        });

        When(x => x.AllowedTools != null, () =>
        {
            RuleFor(x => x.AllowedTools)
                .Must(tools => tools!.All(tool => !string.IsNullOrWhiteSpace(tool)))
                .WithMessage("All allowed tools must have non-empty names");
        });
    }

    private static bool BeValidHttpsUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
            
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
               uri.Scheme == "https";
    }
}