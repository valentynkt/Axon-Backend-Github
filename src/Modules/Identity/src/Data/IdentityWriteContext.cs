using System.Reflection;
using BuildingBlocks.Core;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Identity.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Identity.Models;

namespace Identity.Data;

public sealed class IdentityWriteContext : WriteDbContextBase<IdentityWriteContext>, 
    IWriteDbContext<IdentityWriteContext>
{
    public IdentityWriteContext(
        DbContextOptions<IdentityWriteContext> options,
        ILogger<IdentityWriteContext> logger) 
        : base(options, logger)
    {
    }

    public override string ModuleName => "Identity";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Apply Identity configurations
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