using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("wallet", "identity");

        builder.HasKey(w => w.Id);
        
        builder.Property(w => w.Id)
            .HasConversion(id => id.Value, value => new WalletId(value))
            .HasColumnName("id")
            .HasColumnType("char(26)");

        builder.Property(w => w.ChainId)
            .HasConversion(new ChainId.EfCoreValueConverter())
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

        // Unique constraint for (chain_id, address)
        builder.HasIndex(w => new { w.ChainId, w.Address })
            .IsUnique()
            .HasDatabaseName("ux_wallet_chain_address");

        // Performance indexes
        builder.HasIndex(w => w.ChainId).HasDatabaseName("ix_wallet_chain_id");
        builder.HasIndex(w => w.LastSeenAt).HasDatabaseName("ix_wallet_last_seen_at");
    }
}