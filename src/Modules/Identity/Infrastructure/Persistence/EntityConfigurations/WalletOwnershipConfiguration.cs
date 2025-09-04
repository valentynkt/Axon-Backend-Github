using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.EntityConfigurations;

/// <summary>
/// EF Core configuration for WalletOwnership entity.
/// Includes performance indexes and relationship configuration.
/// </summary>
public sealed class WalletOwnershipConfiguration : IEntityTypeConfiguration<WalletOwnership>
{
    public void Configure(EntityTypeBuilder<WalletOwnership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        
        // Table configuration
        builder.ToTable("wallet_ownerships", "identity");
        
        // Primary key
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasConversion(
                id => id.Value,
                value => new WalletOwnershipId(value))
            .HasColumnName("id");

        // Foreign key to AxonPrincipal
        builder.Property(o => o.AxonId)
            .HasConversion(
                id => id.Value,
                value => new AxonId(value))
            .HasColumnName("axon_id");

        // Foreign key to Wallet
        builder.Property(o => o.WalletId)
            .HasConversion(
                id => id.Value,
                value => new WalletId(value))
            .HasColumnName("wallet_id");

        // Value object conversions
        builder.Property(o => o.ChainId)
            .HasConversion(
                id => id.Value,
                value => ChainId.From(value))
            .HasColumnName("chain_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.ProofType)
            .HasConversion(
                type => type.Value,
                value => ProofType.From(value))
            .HasColumnName("proof_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.AccessMode)
            .HasConversion(
                mode => mode.Value,
                value => AccessMode.From(value))
            .HasColumnName("access_mode")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.State)
            .HasConversion(
                state => state.Value,
                value => OwnershipState.From(value))
            .HasColumnName("state")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.FirstLinkedAt)
            .HasColumnName("first_linked_at");

        builder.Property(o => o.LastVerifiedAt)
            .HasColumnName("last_verified_at");

        builder.Property(o => o.Label)
            .HasColumnName("label")
            .HasMaxLength(100);

        // Audit columns
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.IsDeleted).HasColumnName("is_deleted");
        builder.Property(o => o.DeletedAt).HasColumnName("deleted_at");

        // Query filters for soft deletion
        builder.HasQueryFilter(o => !o.IsDeleted);

        // Performance indexes
        builder.HasIndex(o => o.WalletId)
            .HasDatabaseName("ix_wallet_ownerships_wallet_id");
        
        builder.HasIndex(o => o.ChainId)
            .HasDatabaseName("ix_wallet_ownerships_chain_id");
        
        builder.HasIndex(o => new { o.State, o.AccessMode })
            .HasDatabaseName("ix_wallet_ownerships_state_access_mode")
            .HasFilter("is_deleted = false");
        
        builder.HasIndex(o => o.CreatedAt)
            .HasDatabaseName("ix_wallet_ownerships_created_at");
    }
}