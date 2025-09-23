using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;

public class PrincipalChainDefaultConfiguration : IEntityTypeConfiguration<PrincipalChainDefault>
{
    public void Configure(EntityTypeBuilder<PrincipalChainDefault> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("principal_chain_default", "identity");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(d => d.PrincipalId)
            .HasConversion(id => id.Value, value => new AxonUserId(value))
            .HasColumnName("principal_id")
            .HasColumnType("uuid")
            .IsRequired();

        // NetworkEnvironment property - conversion configured globally, column name specified here
        builder.Property(d => d.NetworkEnvironment)
            .HasColumnName("network_environment")
            .IsRequired()
            .HasComment("Network environment for chain default isolation");

        builder.Property(d => d.ChainId)
            .HasColumnName("chain_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.WalletId)
            .HasConversion(id => id.Value, value => new WalletId(value))
            .HasColumnName("wallet_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Soft delete support
        builder.Property(d => d.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(d => d.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz");

        // Partial unique index for chain default constraint with network environment
        builder.HasIndex(d => new { d.PrincipalId, d.NetworkEnvironment, d.ChainId })
            .IsUnique()
            .HasDatabaseName("ux_chain_default")
            .HasFilter("is_deleted = false");

        // Performance indexes
        builder.HasIndex(d => d.PrincipalId).HasDatabaseName("idx_default_principal_id");
        builder.HasIndex(d => d.WalletId).HasDatabaseName("idx_default_wallet_id");

        // Note: Business logic constraint (verified signing wallet) enforced in domain layer
        // PostgreSQL doesn't support subqueries in CHECK constraints
    }
}