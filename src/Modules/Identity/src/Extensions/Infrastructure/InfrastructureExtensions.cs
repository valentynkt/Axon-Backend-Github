using BuildingBlocks.Persistence;
using BuildingBlocks.Mapster;
using BuildingBlocks.Web;
using BuildingBlocks.Persistence.Common;
using BuildingBlocks.Persistence.Write;
using FluentValidation;
using Identity.Data;
using Identity.Data.Seed;
using Identity.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Extensions.Infrastructure;


public static class InfrastructureExtensions
{
    public static WebApplicationBuilder AddIdentityModules(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IdentityEventMapper>();
        builder.AddMinimalEndpoints(assemblies: typeof(IdentityRoot).Assembly);
        builder.Services.AddValidatorsFromAssembly(typeof(IdentityRoot).Assembly);
        builder.Services.AddCustomMapster(typeof(IdentityRoot).Assembly);
        
        // Add CQRS contexts
        builder.AddCustomDbContext<IdentityWriteContext>(nameof(Identity) + "Write");
        builder.AddCustomDbContext<IdentityReadContext>(nameof(Identity) + "Read");
        
        // Register repositories
        builder.Services.AddScoped<IUserWriteRepository, UserWriteRepository>();
        builder.Services.AddScoped<IUserReadRepository, UserReadRepository>();
        
        // Register Unit of Work for write operations
        builder.Services.AddScoped<IWriteUnitOfWork<IdentityWriteContext>, PostgresWriteUnitOfWork<IdentityWriteContext>>();
        
        builder.Services.AddScoped<IDataSeeder, IdentityDataSeeder>();
        builder.AddCustomIdentityServer();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        });

        builder.Services.AddCustomMediatR();

        return builder;
    }

    public static WebApplication UseIdentityModules(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseIdentityServer();
        
        // Use write context for migrations
        app.UseMigration<IdentityWriteContext>();

        return app;
    }
}