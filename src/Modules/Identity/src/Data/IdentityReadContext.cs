using System.Reflection;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using BuildingBlocks.Infrastructure.Persistence.Read;
using Identity.Identity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Identity.Models;

namespace Identity.Data;

public sealed class IdentityReadContext : ReadDbContextBase<IdentityReadContext>, 
    IReadDbContext<IdentityReadContext>
{
    public IdentityReadContext(
        DbContextOptions<IdentityReadContext> options,
        ILogger<IdentityReadContext> logger) 
        : base(options, logger)
    {
    }

    public override string ModuleName => "Identity";
    
    // Read models (can be the same as write models or specialized DTOs)
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserClaim> UserClaims => Set<UserClaim>();
    public DbSet<RoleClaim> RoleClaims => Set<RoleClaim>();
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Apply same configurations as write context for consistency
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        
        // Apply Identity framework configurations
        builder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
        });
        
        builder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(r => r.Id);
        });
        
        builder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });
        });
        
        builder.Entity<UserClaim>(entity =>
        {
            entity.ToTable("user_claims");
            entity.HasKey(uc => uc.Id);
            entity.HasIndex(uc => uc.UserId);
        });
        
        builder.Entity<RoleClaim>(entity =>
        {
            entity.ToTable("role_claims");
            entity.HasKey(rc => rc.Id);
            entity.HasIndex(rc => rc.RoleId);
        });
        
        builder.Entity<UserLogin>(entity =>
        {
            entity.ToTable("user_logins");
            entity.HasKey(ul => new { ul.LoginProvider, ul.ProviderKey });
            entity.HasIndex(ul => ul.UserId);
        });
        
        builder.Entity<UserToken>(entity =>
        {
            entity.ToTable("user_tokens");
            entity.HasKey(ut => new { ut.UserId, ut.LoginProvider, ut.Name });
        });

        base.OnModelCreating(builder);
    }
}