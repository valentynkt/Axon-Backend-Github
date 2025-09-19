using BuildingBlocks.Application.Events.Enveloping;
using BuildingBlocks.Application.Events.Publishing;
using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Infrastructure.Messaging;
using BuildingBlocks.Infrastructure.Messaging.MassTransit;
using CustomHeaders = BuildingBlocks.Infrastructure.Messaging.Headers.MessageHeaders;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Events;

public sealed class MassTransitIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly IPublishEndpoint _publish;
    private readonly IEnvelopeContextAccessor _ctx;
    private readonly IOptions<MassTransitOptions> _options;
    private readonly IHostEnvironment? _environment;
    private readonly ILogger<MassTransitIntegrationEventPublisher> _logger;

    public MassTransitIntegrationEventPublisher(
        IPublishEndpoint publish, 
        IEnvelopeContextAccessor ctx,
        IOptions<MassTransitOptions> options,
        IHostEnvironment? environment,
        ILogger<MassTransitIntegrationEventPublisher> logger)
    {
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _environment = environment;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(IEnumerable<IIntegrationEvent> events, CancellationToken ct = default)
    {
        if (events is null) return;

        var meta = _ctx.Current;
        var options = _options.Value;
        var eventList = events.ToList();
        
        if (eventList.Count == 0) return;

        // Apply publish timeout if configured
        using var timeoutCts = options.PublishTimeout > TimeSpan.Zero
            ? new CancellationTokenSource(options.PublishTimeout)
            : null;
        
        using var linkedCts = timeoutCts != null
            ? CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token)
            : null;
        
        var cancellationToken = linkedCts?.Token ?? ct;

        _logger.LogDebug("Publishing {EventCount} integration events with trace: {TraceId}", 
            eventList.Count, meta?.TraceId);

        foreach (var e in eventList)
        {
            await _publish.Publish(e, send =>
            {
                // Set MassTransit CorrelationId and RequestId properties
                // Use event ID as correlation ID for tracking
                send.CorrelationId = e.EventId;
                
                // Set RequestId if available from envelope
                if (meta?.RequestId.HasValue == true)
                    send.RequestId = meta.RequestId.Value;

                // Continue with custom headers for visibility
                if (meta is not null)
                {
                    // Standard correlation headers
                    if (!string.IsNullOrWhiteSpace(meta.TraceId))
                        send.Headers.Set(CustomHeaders.TraceId, meta.TraceId);

                    if (meta.RequestId.HasValue)
                        send.Headers.Set(CustomHeaders.RequestId, meta.RequestId.Value);

                    if (!string.IsNullOrWhiteSpace(meta.TenantId))
                        send.Headers.Set(CustomHeaders.TenantId, meta.TenantId);

                    // Check for user-id in metadata (since IntegrationEnvelopeContext doesn't have AxonUserId property yet)
                    // TODO: Once INF-04 adds AxonUserId to envelope, prefer that over metadata
                    if (meta.Metadata?.TryGetValue("user-id", out var userId) == true && userId is not null)
                        send.Headers.Set(CustomHeaders.AxonUserId, userId);

                    // Add any additional metadata
                    if (meta.Metadata is not null)
                    {
                        foreach (var (k, v) in meta.Metadata)
                        {
                            // Skip user-id as we already handled it above
                            if (k != "user-id" && v is not null)
                                send.Headers.Set(k, v);
                        }
                    }
                }

                // Add timestamp
                send.Headers.Set(CustomHeaders.PublishedAt, DateTimeOffset.UtcNow.ToString("O"));

                // Add source/environment/version headers if enabled (INF-09)
                if (options.IncludeEnvironmentHeaders && _environment != null)
                {
                    send.Headers.Set(CustomHeaders.SourceService, _environment.ApplicationName);
                    send.Headers.Set(CustomHeaders.Environment, _environment.EnvironmentName);
                }

                // Add message version if available
                var messageType = e.GetType();
                var versionAttr = messageType.GetCustomAttributes(typeof(MessageVersionAttribute), false).FirstOrDefault();
                if (versionAttr is MessageVersionAttribute version)
                {
                    send.Headers.Set(CustomHeaders.MessageVersion, version.Version);
                }
            }, cancellationToken);
        }

        _logger.LogInformation("Published {EventCount} integration events successfully", eventList.Count);
    }
}