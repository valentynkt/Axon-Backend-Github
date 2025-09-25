using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;

public class AxonPrincipalConfiguration : IEntityTypeConfiguration<AxonPrincipal>
{
    public void Configure(EntityTypeBuilder<AxonPrincipal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Principal", "identity");

        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new AxonUserId(value))
            .HasColumnType("uuid");

        builder.Property(p => p.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.RiskTier)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.CreatedAt)
            .HasColumnType("timestamptz");

        builder.Property(p => p.UpdatedAt)
            .HasColumnType("timestamptz");

        // Version for optimistic concurrency (from AggregateRoot)
        // Maps to PostgreSQL xmin system column for automatic concurrency control
        // Only IsRowVersion() is needed - Npgsql automatically handles xmin mapping
        builder.Property(p => p.Version)
            .IsRowVersion();

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

        // Configure PrincipalChainDefaults as owned entities to prevent concurrency conflicts
        // This ensures their version tracking is handled through the parent aggregate
        builder.OwnsMany(p => p.PrincipalChainDefaults, pcd =>
        {
            pcd.ToTable("PrincipalChainDefault", "identity");
            pcd.WithOwner().HasForeignKey("PrincipalId");
            pcd.Property<Guid>("Id").HasColumnName("id");

            // Configure all properties that were previously in PrincipalChainDefaultConfiguration
            pcd.Property(d => d.ChainId)
                .HasColumnName("chain_id")
                .HasMaxLength(50)
                .IsRequired();

            pcd.Property(d => d.WalletId)
                .HasConversion(id => id.Value, value => new WalletId(value))
                .HasColumnName("wallet_id")
                .HasColumnType("uuid")
                .IsRequired();

            pcd.Property(d => d.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz");

            pcd.Property(d => d.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz");

            pcd.Property(d => d.IsDeleted)
                .HasColumnName("is_deleted")
                .HasDefaultValue(false)
                .IsRequired();

            pcd.Property(d => d.DeletedAt)
                .HasColumnName("deleted_at")
                .HasColumnType("timestamptz");

            // Owned entities don't need their own version - they inherit from parent
            pcd.Ignore(d => d.Version);

            // Configure indexes for owned entity
            pcd.HasIndex(d => new { d.ChainId })
                .IsUnique()
                .HasDatabaseName("ux_chain_default")
                .HasFilter("is_deleted = false");

            pcd.HasIndex(d => d.WalletId).HasDatabaseName("idx_default_wallet_id");
        });

        builder.Navigation(p => p.PrincipalChainDefaults)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        builder.HasIndex(p => p.Type).HasDatabaseName("ix_principal_type");
        builder.HasIndex(p => p.RiskTier).HasDatabaseName("ix_principal_risk_tier");
        builder.HasIndex(p => p.CreatedAt).HasDatabaseName("ix_principal_created_at");
    }
}