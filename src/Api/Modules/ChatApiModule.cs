using Axon.BuildingBlocks.Web.Configuration;
using Axon.Modules.Chat.Application.DependencyInjection;
using Axon.Modules.Chat.Infrastructure.DependencyInjection;

namespace Axon.Api.Modules;

/// <summary>
/// Chat module API registration
/// </summary>
public sealed class ChatApiModule : IApiModule
{
    public string ModuleName => "Chat";
    public string Version => "v1";

    public void ConfigureServices(
        IServiceCollection services, 
        IConfiguration configuration,
        IHostEnvironment _)
    {
        // Register Chat Application layer services (handlers, validators, orchestration)
        services.AddChatApplication();
        
        // Register Chat Infrastructure services (repositories, external services)
        services.AddChatInfrastructure(configuration);
        
        // Register validators
        RegisterValidators();
    }

    private static void RegisterValidators()
    {
        // Validators are auto-registered by FastEndpoints
        // But we can explicitly register if needed
    }
}