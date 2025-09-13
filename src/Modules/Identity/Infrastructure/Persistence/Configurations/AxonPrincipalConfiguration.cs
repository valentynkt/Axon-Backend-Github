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
        builder.ToTable("principal", "identity");

        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new AxonId(value))
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
            .HasColumnType("timestamptz")
            .IsConcurrencyToken();

        // Navigation properties
        builder.HasMany(p => p.Credentials)
            .WithOne()
            .HasForeignKey("PrincipalId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.WalletOwnerships)
            .WithOne()
            .HasForeignKey("PrincipalId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.PrincipalChainDefaults)
            .WithOne()
            .HasForeignKey("PrincipalId")
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(p => p.Type).HasDatabaseName("ix_principal_type");
        builder.HasIndex(p => p.RiskTier).HasDatabaseName("ix_principal_risk_tier");
        builder.HasIndex(p => p.CreatedAt).HasDatabaseName("ix_principal_created_at");
    }
}