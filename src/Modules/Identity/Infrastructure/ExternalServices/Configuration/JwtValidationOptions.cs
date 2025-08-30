using System.ComponentModel.DataAnnotations;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;

public class JwtValidationOptions
{
    [Range(1, 1440, ErrorMessage = "JwksCacheMinutes must be between 1 and 1440 minutes")]
    public int JwksCacheMinutes { get; set; } = 10;
    
    [Range(0, 30, ErrorMessage = "ClockSkewMinutes must be between 0 and 30 minutes")]
    public int ClockSkewMinutes { get; set; } = 5;
}