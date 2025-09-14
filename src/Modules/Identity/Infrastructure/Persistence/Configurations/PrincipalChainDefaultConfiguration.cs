using Axon.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;

public class PrincipalChainDefaultConfiguration : IEntityTypeConfiguration<PrincipalChainDefault>
{
    public void Configure(EntityTypeBuilder<PrincipalChainDefault> builder)
    {
        builder.ToTable("principal_chain_default", "identity");

        builder.HasKey(d => d.Id);

        // Unique constraint for business logic - one default per principal per chain
        builder.HasIndex(d => new { d.PrincipalId, d.ChainId })
            .IsUnique()
            .HasDatabaseName("ix_principal_chain_default_unique");
        
        builder.Property(d => d.PrincipalId)
            .HasConversion(id => id.Value, value => new AxonId(value))
            .HasColumnName("principal_id")
            .HasColumnType("uuid");

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

        // Performance indexes
        builder.HasIndex(d => d.PrincipalId).HasDatabaseName("ix_default_principal_id");
        builder.HasIndex(d => d.WalletId).HasDatabaseName("ix_default_wallet_id");

        // Note: Business logic constraint (verified signing wallet) enforced in domain layer
        // PostgreSQL doesn't support subqueries in CHECK constraints
    }
}