using Axon.Api.Contracts.V1.Identity.Webhooks;
using BuildingBlocks.Application.Validation.Base;
using FluentValidation;

namespace Axon.Api.Endpoints.V1.Webhooks.Dynamic;

/// <summary>
/// Validator for incoming Dynamic.xyz webhook events
/// </summary>
public class ProcessDynamicWebhookRequestValidator : BaseValidator<DynamicWebhookEventDto>
{
    private static readonly HashSet<string> ValidEventTypes = new()
    {
        "users.created",
        "users.updated",
        "users.deleted",
        "wallets.linked",
        "wallets.unlinked",
        "wallets.updated",
        "sessions.created",
        "sessions.deleted",
        "environments.updated"
    };

    public ProcessDynamicWebhookRequestValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("EventId is required")
            .MaximumLength(100).WithMessage("EventId must not exceed 100 characters");

        RuleFor(x => x.EventName)
            .NotEmpty().WithMessage("EventName is required")
            .Must(BeValidEventType).WithMessage(x => $"Invalid event type: {x.EventName}. Must be one of the allowed event types.");

        RuleFor(x => x.CreatedAt)
            .Must(BeValidTimestamp).WithMessage("CreatedAt timestamp cannot be more than 5 minutes in the future");

        RuleFor(x => x.Data)
            .NotNull().WithMessage("Data payload is required");
    }

    private static bool BeValidEventType(string eventName)
    {
        return !string.IsNullOrWhiteSpace(eventName) && ValidEventTypes.Contains(eventName);
    }

    private static bool BeValidTimestamp(DateTime createdAt)
    {
        var now = DateTime.UtcNow;
        var maxFutureTime = now.AddMinutes(5);
        return createdAt <= maxFutureTime;
    }
}