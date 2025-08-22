using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Axon.BuildingBlocks.Web.Configuration;

/// <summary>
/// Interface for API modules that need to register services
/// </summary>
public interface IApiModule
{
    /// <summary>
    /// Configure services for this module
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="environment">Host environment</param>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment);
    
    /// <summary>
    /// Module name for identification
    /// </summary>
    string ModuleName { get; }
    
    /// <summary>
    /// Module version for API versioning
    /// </summary>
    string Version { get; }
}