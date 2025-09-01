using System.ComponentModel.DataAnnotations;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;

public class DynamicXyzOptions
{
    public const string SectionName = "DynamicXyz";
    
    [Required]
    public string BaseUrl { get; set; } = string.Empty;
    
    [Required]
    public string ApiToken { get; set; } = string.Empty;
    
    [Required]
    public string EnvironmentId { get; set; } = string.Empty;
    
    public string WebhookSecret { get; set; } = string.Empty;
    
    public JwtValidationOptions Jwt { get; set; } = new();
    
    public HttpClientOptions HttpClient { get; set; } = new();
    
    public string JwksUri => $"{BaseUrl.TrimEnd('/')}/sdk/{EnvironmentId}/.well-known/jwks.json";
}