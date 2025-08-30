using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Commands.ProcessWebhook;

/// <summary>
/// Command to process incoming Dynamic.xyz webhook events
/// </summary>
public record ProcessWebhookCommand(
    string EventId,
    string EventName,
    string? WebhookId,
    DateTime CreatedAt,
    IReadOnlyDictionary<string, object> Data
) : IRequest<Result<WebhookProcessResult, Error>>;