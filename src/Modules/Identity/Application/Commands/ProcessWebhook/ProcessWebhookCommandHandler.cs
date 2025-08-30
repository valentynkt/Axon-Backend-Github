using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.ProcessWebhook;

/// <summary>
/// Handler for processing Dynamic.xyz webhook events
/// </summary>
public class ProcessWebhookCommandHandler : IRequestHandler<ProcessWebhookCommand, Result<WebhookProcessResult, Error>>
{
    private readonly ILogger<ProcessWebhookCommandHandler> _logger;

    public ProcessWebhookCommandHandler(ILogger<ProcessWebhookCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task<Result<WebhookProcessResult, Error>> Handle(ProcessWebhookCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        // S1: Stub implementation - acknowledge receipt and schedule sync for user/wallet events
        _logger.LogInformation("Processing webhook event: {EventId} of type {EventName}", request.EventId, request.EventName);

        var syncScheduled = request.EventName.StartsWith("users.", StringComparison.OrdinalIgnoreCase) ||
                           request.EventName.StartsWith("wallets.", StringComparison.OrdinalIgnoreCase);

        var result = new WebhookProcessResult(
            Received: true,
            ProcessedAt: DateTime.UtcNow,
            SyncScheduled: syncScheduled,
            EventId: request.EventId,
            WebhookId: request.WebhookId,
            Status: "processed",
            RetryCount: 0,
            ErrorMessage: null
        );

        return Task.FromResult(Result.Success<WebhookProcessResult, Error>(result));
    }
}