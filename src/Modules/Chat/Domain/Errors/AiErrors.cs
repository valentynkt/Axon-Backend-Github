using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Modules.Chat.Domain.Errors;

/// <summary>
/// Domain errors related to AI operations
/// </summary>
public static class AiErrors
{
    public static Error ServiceUnavailable => 
        Error.Unavailable("The AI service is currently unavailable", "AI.ServiceUnavailable");
    
    public static Error InvalidConfiguration => 
        Error.Configuration("The AI configuration is invalid", "AI.InvalidConfiguration");
    
    public static Error RequestFailed => 
        Error.External("The AI request failed", "AI.RequestFailed");
    
    public static Error ResponseInvalid => 
        Error.Serialization("The AI response is invalid", "AI.ResponseInvalid");
    
    public static Error Timeout => 
        Error.Timeout("The AI request timed out", "AI.Timeout");
    
    public static Error RateLimited => 
        Error.RateLimit("Too many requests to the AI service", "AI.RateLimited");
}