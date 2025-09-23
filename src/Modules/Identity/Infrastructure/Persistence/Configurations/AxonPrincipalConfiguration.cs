using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;

public class AxonPrincipalConfiguration : IEntityTypeConfiguration<AxonPrincipal>
{
    public void Configure(EntityTypeBuilder<AxonPrincipal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("principal", "identity");

        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new AxonUserId(value))
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(p => p.Type)
            .HasConversion<string>()
            .HasColumnName("type")
            .HasMaxLength(20);

        builder.Property(p => p.RiskTier)
            .HasConversion<string>()
            .HasColumnName("risk_tier")
            .HasMaxLength(20);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.Property(p => p.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();

        // Navigation properties with backing field access
        builder.HasMany(p => p.Credentials)
            .WithOne()
            .HasForeignKey("PrincipalId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Credentials)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.WalletOwnerships)
            .WithOne()
            .HasForeignKey("PrincipalId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.WalletOwnerships)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.PrincipalChainDefaults)
            .WithOne()
            .HasForeignKey("PrincipalId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.PrincipalChainDefaults)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        builder.HasIndex(p => p.Type).HasDatabaseName("ix_principal_type");
        builder.HasIndex(p => p.RiskTier).HasDatabaseName("ix_principal_risk_tier");
        builder.HasIndex(p => p.CreatedAt).HasDatabaseName("ix_principal_created_at");
    }
}