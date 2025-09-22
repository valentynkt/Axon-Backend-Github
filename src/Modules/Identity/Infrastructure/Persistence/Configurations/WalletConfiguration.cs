using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("wallet", "identity", t =>
        {
            // Check constraint for Solana network validation using modern EF Core 9 approach
            t.HasCheckConstraint(
                "check_wallet_solana_network_environment",
                "chain_id != 'solana' OR network_environment IN ('mainnet', 'devnet', 'testnet')"
            );
        });

        builder.HasKey(w => w.Id);
        
        builder.Property(w => w.Id)
            .HasConversion(id => id.Value, value => new WalletId(value))
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(w => w.NetworkEnvironment)
            .HasConversion(new NetworkEnvironment.EfCoreValueConverter())
            .HasColumnName("network_environment")
            .HasMaxLength(50)
            .IsRequired()
            .HasComment("Network for on-chain artifacts (mainnet/devnet/testnet)");

        builder.Property(w => w.ChainId)
            .HasColumnName("chain_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(w => w.Address)
            .HasConversion(new Address.EfCoreValueConverter())
            .HasColumnName("address")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(w => w.FirstSeenAt)
            .HasColumnName("first_seen_at")
            .HasColumnType("timestamptz");

        builder.Property(w => w.LastSeenAt)
            .HasColumnName("last_seen_at")
            .HasColumnType("timestamptz");

        builder.Property(w => w.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");

        builder.Property(w => w.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsConcurrencyToken();

        // Soft delete support
        builder.Property(w => w.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(w => w.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz");

        // Partial unique index for network environment isolation - triple key constraint
        builder.HasIndex(w => new { w.NetworkEnvironment, w.ChainId, w.Address })
            .IsUnique()
            .HasDatabaseName("ux_wallet_netenv_chain_addr")
            .HasFilter("is_deleted = false");

        // Performance index for cross-environment lookups
        builder.HasIndex(w => new { w.ChainId, w.Address })
            .HasDatabaseName("idx_wallet_chain_addr_active")
            .HasFilter("is_deleted = false");

        // Performance indexes
        builder.HasIndex(w => w.ChainId).HasDatabaseName("idx_wallet_chain_id");
        builder.HasIndex(w => w.LastSeenAt).HasDatabaseName("idx_wallet_last_seen_at");

        // Foreign key relationship - WalletOwnerships reference this Wallet
        builder.HasMany<WalletOwnership>()
            .WithOne()
            .HasForeignKey(wo => wo.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}