using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Web.Configuration;

public sealed record CorsOptions
{
    public const string SectionName = "Cors";
    
    public string PolicyName { get; init; } = "DefaultPolicy";
    
    [Required]
    public string[] AllowedOrigins { get; init; } = Array.Empty<string>();
    
    public string[] AllowedMethods { get; init; } = ["GET", "POST", "PUT", "DELETE", "OPTIONS"];
    
    public string[] AllowedHeaders { get; init; } = ["*"];
    
    public bool AllowCredentials { get; init; } = true;
    
    public int PreflightMaxAge { get; init; } = 86400;
    
    public bool IsValid => AllowedOrigins.Length > 0;
}