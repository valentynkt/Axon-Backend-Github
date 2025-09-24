namespace Axon.Modules.Identity.Infrastructure.Persistence.Context;

using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Identity-specific DbContext that extends IdentityDbContext for Microsoft Identity Framework integration.
/// This context manages AxonUserAuth entities and related Identity tables.
/// </summary>
public class IdentityContext : IdentityDbContext<AxonUserAuth, IdentityRole<Guid>, Guid>
{
    public IdentityContext(DbContextOptions<IdentityContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure schema for all Identity tables
        builder.HasDefaultSchema("identity");

        // Apply AxonUserAuth configuration
        builder.ApplyConfiguration(new AxonUserAuthConfiguration());

        // Configure Identity tables to use the identity schema
        builder.Entity<AxonUserAuth>().ToTable("AspNetUsers", "identity");
        builder.Entity<IdentityRole<Guid>>().ToTable("AspNetRoles", "identity");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("AspNetUserClaims", "identity");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("AspNetUserRoles", "identity");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("AspNetUserLogins", "identity");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("AspNetRoleClaims", "identity");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("AspNetUserTokens", "identity");

        // Configure primary keys for Identity entities
        builder.Entity<IdentityRole<Guid>>().HasKey(r => r.Id);
        builder.Entity<IdentityUserClaim<Guid>>().HasKey(uc => uc.Id);
        builder.Entity<IdentityUserRole<Guid>>().HasKey(ur => new { ur.UserId, ur.RoleId });
        builder.Entity<IdentityUserLogin<Guid>>().HasKey(ul => new { ul.LoginProvider, ul.ProviderKey });
        builder.Entity<IdentityRoleClaim<Guid>>().HasKey(rc => rc.Id);
        builder.Entity<IdentityUserToken<Guid>>().HasKey(ut => new { ut.UserId, ut.LoginProvider, ut.Name });

        // Configure indexes for performance
        builder.Entity<IdentityRole<Guid>>()
            .HasIndex(r => r.NormalizedName)
            .IsUnique()
            .HasDatabaseName("RoleNameIndex");

        builder.Entity<IdentityUserLogin<Guid>>()
            .HasIndex(ul => ul.UserId)
            .HasDatabaseName("IX_AspNetUserLogins_UserId");

        builder.Entity<IdentityUserRole<Guid>>()
            .HasIndex(ur => ur.RoleId)
            .HasDatabaseName("IX_AspNetUserRoles_RoleId");

        builder.Entity<IdentityUserClaim<Guid>>()
            .HasIndex(uc => uc.UserId)
            .HasDatabaseName("IX_AspNetUserClaims_UserId");

        builder.Entity<IdentityRoleClaim<Guid>>()
            .HasIndex(rc => rc.RoleId)
            .HasDatabaseName("IX_AspNetRoleClaims_RoleId");
    }
}

/// <summary>
/// Design-time factory for IdentityContext to support EF Core tools (migrations, etc.)
/// </summary>
public sealed class IdentityContextFactory : IDesignTimeDbContextFactory<IdentityContext>
{
    public IdentityContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityContext>();

        // Get connection string from environment or use default
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=axon_chat;Username=postgres;Password=postgres;Include Error Detail=true";

        optionsBuilder.UseNpgsql(connectionString, opt =>
        {
            opt.MigrationsAssembly(typeof(IdentityContext).Assembly.FullName);
            opt.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
        })
        .UseSnakeCaseNamingConvention();

        return new IdentityContext(optionsBuilder.Options);
    }
}