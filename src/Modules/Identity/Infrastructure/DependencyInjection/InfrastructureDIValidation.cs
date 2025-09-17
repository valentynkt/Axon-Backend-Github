using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.Extensions.DependencyInjection;

namespace Axon.Modules.Identity.Infrastructure.DependencyInjection;

/// <summary>
/// Validates all critical Identity Infrastructure layer DI registrations
/// </summary>
public static class InfrastructureDIValidation
{
    /// <summary>
    /// Validates that all Identity Infrastructure services can be resolved from the DI container
    /// </summary>
    /// <param name="serviceProvider">The configured service provider</param>
    /// <returns>List of validation errors, if any</returns>
    public static List<string> ValidateInfrastructureServices(IServiceProvider serviceProvider)
    {
        var errors = new List<string>();

        try
        {
            // Database contexts validation
            TestServiceResolution<IdentityWriteDbContext>(serviceProvider, errors, "IdentityWriteDbContext - Write database context");
            TestServiceResolution<IdentityReadDbContext>(serviceProvider, errors, "IdentityReadDbContext - Read database context");
            TestServiceResolution<IIdentityWriteDbContext>(serviceProvider, errors, "IIdentityWriteDbContext interface");
            TestServiceResolution<IIdentityReadDbContext>(serviceProvider, errors, "IIdentityReadDbContext interface");
            
            // Write Repository validation
            TestServiceResolution<IAxonPrincipalWriteRepository>(serviceProvider, errors, "IAxonPrincipalWriteRepository");
            TestServiceResolution<IWalletWriteRepository>(serviceProvider, errors, "IWalletWriteRepository");
            
            // Read Repository validation
            TestServiceResolution<IAxonPrincipalReadRepository>(serviceProvider, errors, "IAxonPrincipalReadRepository");
            TestServiceResolution<IWalletReadRepository>(serviceProvider, errors, "IWalletReadRepository");
            
            // Unit of Work validation - module-specific
            TestServiceResolution<IWriteUnitOfWork<IdentityModule>>(serviceProvider, errors, "IWriteUnitOfWork<IdentityModule>");
            
            // Validate Unit of Work is properly wrapped
            var unitOfWork = serviceProvider.GetService<IWriteUnitOfWork<IdentityModule>>();
            if (unitOfWork != null && !unitOfWork.GetType().Name.Contains("EfUnitOfWork", StringComparison.Ordinal))
            {
                errors.Add($"CRITICAL: IWriteUnitOfWork<IdentityModule> resolves to {unitOfWork.GetType().Name} instead of EfUnitOfWork wrapper. Transaction management not properly configured!");
            }
            
        }
        catch (Exception ex)
        {
            errors.Add($"CRITICAL: Infrastructure DI validation failed with exception: {ex.Message}");
        }

        return errors;
    }

    private static void TestServiceResolution<T>(IServiceProvider serviceProvider, List<string> errors, string serviceName)
        where T : class
    {
        try
        {
            var service = serviceProvider.GetService<T>();
            if (service == null)
            {
                errors.Add($"INFRASTRUCTURE: {serviceName} ({typeof(T).Name}) could not be resolved from DI container");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"INFRASTRUCTURE: {serviceName} ({typeof(T).Name}) failed to resolve: {ex.Message}");
        }
    }
}