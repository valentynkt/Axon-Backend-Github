using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Axon.Modules.Chat.Infrastructure.Configuration;

/// <summary>
/// Service registration for Chat Infrastructure layer
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Register Chat Infrastructure services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access", Justification = "Configuration binding is acceptable for this use case")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050:Members annotated with 'RequiresDynamicCodeAttribute' require dynamic access", Justification = "Configuration binding is acceptable for this use case")]
    public static IServiceCollection AddChatInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        
        // Configure OpenAI options
        services.Configure<OpenAiOptions>(
            configuration.GetSection(OpenAiOptions.SectionName));

        // Register AI client
        services.AddScoped<IAiClient, OpenAiClient>();

        return services;
    }
}