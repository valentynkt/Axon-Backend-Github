using Microsoft.Extensions.Options;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;

public class DynamicXyzOptionsValidator : IValidateOptions<DynamicXyzOptions>
{
    public ValidateOptionsResult Validate(string? name, DynamicXyzOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            failures.Add("BaseUrl is required");
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
            failures.Add("BaseUrl must be a valid absolute URI");

        if (string.IsNullOrWhiteSpace(options.ApiToken))
            failures.Add("ApiToken is required");

        if (string.IsNullOrWhiteSpace(options.EnvironmentId))
            failures.Add("EnvironmentId is required");

        if (options.Jwt.JwksCacheMinutes < 1 || options.Jwt.JwksCacheMinutes > 1440)
            failures.Add("JwksCacheMinutes must be between 1 and 1440 minutes");

        if (options.Jwt.ClockSkewMinutes < 0 || options.Jwt.ClockSkewMinutes > 30)
            failures.Add("ClockSkewMinutes must be between 0 and 30 minutes");

        if (options.HttpClient.TimeoutSeconds < 5 || options.HttpClient.TimeoutSeconds > 300)
            failures.Add("TimeoutSeconds must be between 5 and 300 seconds");

        if (options.HttpClient.MaxRetryAttempts < 1 || options.HttpClient.MaxRetryAttempts > 10)
            failures.Add("MaxRetryAttempts must be between 1 and 10");

        if (options.HttpClient.RateLimitPerMinute < 1 || options.HttpClient.RateLimitPerMinute > 10000)
            failures.Add("RateLimitPerMinute must be between 1 and 10000");

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }
}