using System.Reflection;
using Mapster;
using MapsterMapper;

namespace Axon.Api.Configuration.Mapping;

/// <summary>
/// Centralized Mapster configuration and validation
/// </summary>
public static class MapsterConfiguration
{
    /// <summary>
    /// Configure all Mapster mappings with validation
    /// </summary>
    public static IServiceCollection AddMapsterWithProfiles(
        this IServiceCollection services, 
        params Assembly[] assemblies)
    {
        // Configure global settings
        var config = ConfigureGlobalSettings();
        
        // Auto-discover and register mapping profiles
        RegisterMappingProfiles(config, assemblies);
        
        // Validate all mappings are properly configured
        ValidateMappings(config);
        
        // Register Mapster services
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();
        
        return services;
    }

    private static TypeAdapterConfig ConfigureGlobalSettings()
    {
        var config = new TypeAdapterConfig();
        
        config.Default
            .NameMatchingStrategy(NameMatchingStrategy.Flexible)
            .PreserveReference(true)
            .ShallowCopyForSameType(true)
            .IgnoreNullValues(false);
            
        return config;
    }

    private static void RegisterMappingProfiles(TypeAdapterConfig config, Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = new[] { Assembly.GetCallingAssembly() };
        }

        // Scan for IRegister implementations
        config.Scan(assemblies);
        
        // Log registered profiles for diagnostics
        var profileTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsAssignableTo(typeof(IRegister)) && 
                       t.IsClass && 
                       !t.IsAbstract)
            .ToList();

        foreach (var profileType in profileTypes)
        {
            Console.WriteLine($"[Mapster] Registered mapping profile: {profileType.Name}");
        }
    }

    private static void ValidateMappings(TypeAdapterConfig config)
    {
        try
        {
            // Compile configuration to catch mapping errors early
            config.Compile();
            Console.WriteLine("[Mapster] All mappings validated successfully");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Mapster mapping validation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validate that a specific mapping is configured
    /// </summary>
    public static void EnsureMappingExists<TSource, TDestination>(this TypeAdapterConfig config)
    {
        try
        {
            var compiledConfig = config.Fork(c => c.NewConfig<TSource, TDestination>());
            compiledConfig.Compile();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Required mapping from {typeof(TSource).Name} to {typeof(TDestination).Name} is not configured: {ex.Message}");
        }
    }
}