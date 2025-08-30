using System.ComponentModel.DataAnnotations;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;

public class HttpClientOptions
{
    [Range(5, 300, ErrorMessage = "TimeoutSeconds must be between 5 and 300 seconds")]
    public int TimeoutSeconds { get; set; } = 30;
    
    [Range(1, 10, ErrorMessage = "MaxRetryAttempts must be between 1 and 10")]
    public int MaxRetryAttempts { get; set; } = 3;
    
    [Range(1, 10000, ErrorMessage = "RateLimitPerMinute must be between 1 and 10000")]
    public int RateLimitPerMinute { get; set; } = 500;
}