using Axon.Api.Contracts.V1.Identity.Webhooks;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Commands.ProcessWebhook;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;

namespace Axon.Api.Endpoints.V1.Webhooks.Dynamic;

/// <summary>
/// Endpoint for processing Dynamic.xyz webhook events
/// </summary>
public class ProcessDynamicWebhookEndpoint : BaseIdentityCommandEndpoint<DynamicWebhookEventDto, DynamicWebhookResponseDto, ProcessWebhookCommand, WebhookProcessResult>
{
    public ProcessDynamicWebhookEndpoint(IMediator mediator, ILogger<ProcessDynamicWebhookEndpoint> logger) 
        : base(mediator, logger)
    {
    }

    public override void Configure()
    {
        Post(GetRoute());
        AllowAnonymous(); // Webhooks use signature validation, not Bearer tokens
        Validator<ProcessDynamicWebhookRequestValidator>();

        // Require the signature header (actual validation will be in S2)
        PreProcessor<SignatureHeaderRequirement>();

        Summary(s =>
        {
            s.Summary = GetSummary();
            s.Description = GetDescription();
            s.Responses[202] = "Webhook accepted for processing";
            s.Responses[400] = "Invalid webhook payload";
            s.Responses[401] = "Missing or invalid signature header";
            s.Responses[500] = "Internal server error";

            s.ExampleRequest = new DynamicWebhookEventDto
            {
                EventId = "evt_01HQXYZ789ABCDEF01234567",
                EventName = "users.created",
                WebhookId = "whk_config_123456",
                CreatedAt = DateTime.UtcNow,
                Data = new Dictionary<string, object>
                {
                    ["userId"] = "usr_01HQABC123DEF456789",
                    ["email"] = "user@example.com",
                    ["createdAt"] = DateTime.UtcNow.ToString("O")
                }
            };
        });

        Tags("Webhooks"); // Different tag from Authentication endpoints
    }

    protected override string GetRoute() => "/api/v1/webhooks/dynamic";

    protected override string GetSummary() => "Process Dynamic.xyz webhook event";

    protected override string GetDescription() => 
        "Receives and processes webhook events from Dynamic.xyz for user and wallet lifecycle changes. " +
        "Returns 202 Accepted to indicate the webhook has been received and queued for async processing.";

    protected override string GetSuccessResponse() => "Webhook accepted and queued for processing";

    protected override async Task<Result<ProcessWebhookCommand, Error>> ExecuteCommand(
        DynamicWebhookEventDto request, 
        CancellationToken ct)
    {
        var command = new ProcessWebhookCommand(
            request.EventId,
            request.EventName,
            request.WebhookId,
            request.CreatedAt,
            request.Data
        );
        
        return await Task.FromResult(Result.Success<ProcessWebhookCommand, Error>(command));
    }

    protected override Result<DynamicWebhookResponseDto, Error> MapDomainToResponse(WebhookProcessResult domainResult)
    {
        var response = new DynamicWebhookResponseDto
        {
            Received = domainResult.Received,
            ProcessedAt = domainResult.ProcessedAt,
            SyncScheduled = domainResult.SyncScheduled,
            Acknowledgment = new WebhookAcknowledgmentDto
            {
                EventId = domainResult.EventId,
                WebhookId = domainResult.WebhookId,
                Status = domainResult.Status,
                RetryCount = domainResult.RetryCount,
                ErrorMessage = domainResult.ErrorMessage
            }
        };

        return Result.Success<DynamicWebhookResponseDto, Error>(response);
    }

    public override async Task HandleAsync(DynamicWebhookEventDto req, CancellationToken ct)
    {
        await base.HandleAsync(req, ct);
        
        // Override to return 202 Accepted for webhook processing
        if (Response != null && HttpContext.Response.StatusCode == 200)
        {
            HttpContext.Response.StatusCode = 202;
        }
    }
}

/// <summary>
/// Pre-processor to check for required X-Dynamic-Signature header
/// </summary>
public class SignatureHeaderRequirement : IPreProcessor<DynamicWebhookEventDto>
{
    public Task PreProcessAsync(IPreProcessorContext<DynamicWebhookEventDto> context, CancellationToken ct)
    {
        if (!context.HttpContext.Request.Headers.ContainsKey("X-Dynamic-Signature"))
        {
            context.HttpContext.Response.StatusCode = 401;
            return context.HttpContext.Response.WriteAsync("Missing X-Dynamic-Signature header", ct);
        }

        // S1: Just check header presence - S2 will validate the actual signature
        return Task.CompletedTask;
    }
}