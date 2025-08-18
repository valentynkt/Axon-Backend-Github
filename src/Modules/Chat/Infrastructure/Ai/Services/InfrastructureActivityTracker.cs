using System.Diagnostics;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Ai.Services;

/// <summary>
/// Infrastructure activity tracker implementation
/// </summary>
public sealed class InfrastructureActivityTracker : IActivityTracker
{
    private static readonly ActivitySource ActivitySource = new("Axon.Chat.Infrastructure");
    private readonly ILogger<InfrastructureActivityTracker> _logger;
    
    public InfrastructureActivityTracker(ILogger<InfrastructureActivityTracker> logger)
    {
        _logger = logger;
    }
    
    public Activity? StartActivity(AiRequest request)
    {
        var activity = ActivitySource.StartActivity("ProcessAiMessage");
        activity?.SetTag("message.length", request.Message.Length);
        activity?.SetTag("mcp.server_count", request.McpConfigs?.Count ?? 0);
        return activity;
    }
    
    public void TagResponse(Activity? activity, AiResponse response, TimeSpan duration)
    {
        if (activity == null) return;
        
        activity.SetTag("response.length", response.Content.Length);
        activity.SetTag("response.id", response.ResponseId);
        activity.SetTag("duration.ms", duration.TotalMilliseconds);
    }
    
    public void TagError(Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Error);
    }
    
    public void LogProcessingStart(string model, int mcpServerCount)
    {
        _logger.LogInformation("Processing message with {Model} and {McpServerCount} MCP servers", 
            model, mcpServerCount);
    }
    
    public void LogProcessingSuccess(TimeSpan duration, int toolCount)
    {
        _logger.LogInformation("Successfully processed message in {Duration}ms with {ToolCount} tools",
            duration.TotalMilliseconds, toolCount);
    }
    
    public void LogProcessingFailure(Exception ex, TimeSpan duration)
    {
        _logger.LogError(ex, "Failed to process message after {Duration}ms", 
            duration.TotalMilliseconds);
    }
}