using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Modules.Chat.Domain.Errors;

/// <summary>
/// Domain errors related to AI operations
/// </summary>
public static class AiErrors
{
    // Service availability errors
    public static Error ServiceUnavailable => 
        Error.Unavailable("The AI service is currently unavailable", "AI.SERVICE_UNAVAILABLE");
    
    public static Error ServiceUnavailable(string message) => 
        Error.Unavailable(message, "AI.SERVICE_UNAVAILABLE");
    
    // Configuration errors
    public static Error InvalidConfiguration => 
        Error.Configuration("The AI configuration is invalid", "AI.INVALID_CONFIGURATION");
    
    public static Error InvalidConfiguration(string configKey) => 
        Error.Configuration($"Invalid AI configuration: {configKey}", "AI.INVALID_CONFIGURATION", configKey);
    
    // Request errors
    public static Error RequestFailed => 
        Error.External("The AI request failed", "AI.REQUEST_FAILED");
    
    public static Error RequestFailed(string message, string code = "AI.REQUEST_FAILED") => 
        Error.External(message, code);
    
    public static Error EmptyMessage => 
        Error.Validation("AI request message cannot be empty", "AI.REQUEST.EMPTY_MESSAGE");
    
    public static Error InvalidRequest(string message) => 
        Error.Validation(message, "AI.REQUEST.INVALID");
    
    // Response errors
    public static Error ResponseInvalid => 
        Error.Serialization("The AI response is invalid", "AI.RESPONSE_INVALID");
    
    public static Error ResponseInvalid(string message) => 
        Error.Serialization(message, "AI.RESPONSE_INVALID");
    
    // Timeout errors
    public static Error Timeout => 
        Error.Timeout("The AI request timed out", "AI.TIMEOUT");
    
    public static Error Timeout(TimeSpan duration) => 
        Error.Timeout($"The AI request timed out after {duration.TotalSeconds:F1} seconds", "AI.TIMEOUT", duration);
    
    // Rate limiting errors
    public static Error RateLimited => 
        Error.RateLimit("Too many requests to the AI service", "AI.RATE_LIMITED");
    
    public static Error RateLimited(TimeSpan? retryAfter) => 
        Error.RateLimit("Too many requests to the AI service", "AI.RATE_LIMITED", retryAfter);
    
    // Network errors
    public static Error NetworkError(string message, Exception? exception = null) => 
        Error.Network(message, "AI.NETWORK_ERROR", exception);
    
    // Authentication/Authorization errors
    public static Error Unauthorized(string message = "Unauthorized access to AI service") => 
        Error.Unauthorized(message, "AI.UNAUTHORIZED");
    
    public static Error Forbidden(string message = "Access forbidden to AI service") => 
        Error.Forbidden(message, "AI.FORBIDDEN");
    
    // OpenAI specific errors
    public static Error OpenAiError(string message, string? openAiErrorCode = null) => 
        Error.External(message, openAiErrorCode ?? "AI.OPENAI_ERROR");
}