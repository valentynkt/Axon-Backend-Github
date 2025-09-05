using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.EntityConfigurations;

/// <summary>
/// EF Core configuration for Wallet aggregate root.
/// Now uses strongly-typed WalletProfile and WalletTag join table instead of JSON.
/// </summary>
public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        
        // Table configuration
        builder.ToTable("wallets", "identity");
        
        // Primary key
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .HasConversion(
                id => id.Value,
                value => new WalletId(value))
            .HasColumnName("id");

        // Value object conversions
        builder.Property(w => w.Chain)
            .HasConversion(
                chain => chain.Value,
                value => ChainId.From(value))
            .HasColumnName("chain")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(w => w.Address)
            .HasConversion(
                address => address.Value,
                value => Address.From(value))
            .HasColumnName("address")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(w => w.FirstSeenAt)
            .HasColumnName("first_seen_at")
            .IsRequired();

        builder.Property(w => w.LastSeenAt)
            .HasColumnName("last_seen_at")
            .IsRequired();

        // Owned entity: WalletProfile (inline columns)
        builder.OwnsOne(w => w.Profile, profile =>
        {
            // Ignore inherited Entity properties for owned entities
            profile.Ignore(p => p.Id);
            profile.Ignore(p => p.CreatedAt);
            profile.Ignore(p => p.UpdatedAt);

            profile.Property(p => p.Provider)
                .HasConversion(
                    provider => provider.Value,
                    value => ProviderType.From(value))
                .HasColumnName("provider")
                .HasMaxLength(100)
                .IsRequired();

            profile.Property(p => p.DisplayName)
                .HasColumnName("display_name")
                .HasMaxLength(255);
        });

        // Configure relationship to WalletTag join table
        builder.HasMany(w => w.WalletTags)
            .WithOne()
            .HasForeignKey(wt => wt.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        // Audit properties
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.IsDeleted).HasColumnName("is_deleted");
        builder.Property(w => w.DeletedAt).HasColumnName("deleted_at");

        // Query filters
        builder.HasQueryFilter(w => !w.IsDeleted);

        // 🚨 CRITICAL: Unique constraint on (Chain, Address)
        builder.HasIndex(w => new { w.Chain, w.Address })
            .IsUnique()
            .HasDatabaseName("ix_wallets_chain_address_unique");

        // Performance indexes based on repository queries
        builder.HasIndex(w => w.Chain)
            .HasDatabaseName("ix_wallets_chain");
        
        builder.HasIndex(w => w.Address)
            .HasDatabaseName("ix_wallets_address");
        
        builder.HasIndex(w => w.LastSeenAt)
            .HasDatabaseName("ix_wallets_last_seen_at");
        
        builder.HasIndex(w => w.FirstSeenAt)
            .HasDatabaseName("ix_wallets_first_seen_at");

        // Index on provider for filtering by wallet source
        // Use proper expression for owned entity property
        builder.OwnsOne(w => w.Profile)
            .HasIndex(p => p.Provider)
            .HasDatabaseName("ix_wallets_provider");
    }
}

/// <summary>
/// EF Core configuration for WalletTag join entity.
/// </summary>
public sealed class WalletTagConfiguration : IEntityTypeConfiguration<WalletTag>
{
    public void Configure(EntityTypeBuilder<WalletTag> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Table configuration
        builder.ToTable("wallet_tags", "identity");

        // Primary key
        builder.HasKey(wt => wt.Id);
        builder.Property(wt => wt.Id)
            .HasConversion(
                id => id,
                value => value)
            .HasColumnName("id");

        // Foreign key to Wallet
        builder.Property(wt => wt.WalletId)
            .HasConversion(
                id => id.Value,
                value => new WalletId(value))
            .HasColumnName("wallet_id")
            .IsRequired();

        // Tag value object
        builder.Property(wt => wt.Tag)
            .HasConversion(
                tag => tag.Value,
                value => Tag.From(value))
            .HasColumnName("tag")
            .HasMaxLength(100)
            .IsRequired();

        // Audit properties
        builder.Property(wt => wt.CreatedAt).HasColumnName("created_at");
        builder.Property(wt => wt.UpdatedAt).HasColumnName("updated_at");

        // 🚨 CRITICAL: Unique constraint on (WalletId, Tag)
        builder.HasIndex(wt => new { wt.WalletId, wt.Tag })
            .IsUnique()
            .HasDatabaseName("ix_wallet_tags_wallet_tag_unique");

        // Performance index on tag for searching across wallets
        builder.HasIndex(wt => wt.Tag)
            .HasDatabaseName("ix_wallet_tags_tag");

        // Check constraint to ensure only allowed tags
        // Note: This would be better generated from the Tag VO allowed values
        builder.HasCheckConstraint("ck_wallet_tags_allowed_values", 
            "tag IN ('personal', 'business', 'trading', 'defi', 'gaming', 'nft', 'dao', 'test', 'main', 'hot', 'cold')");
    }
}