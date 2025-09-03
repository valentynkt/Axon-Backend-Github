using System.Text.Json;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.EntityConfigurations;

/// <summary>
/// EF Core configuration for Wallet aggregate root.
/// Includes critical unique constraints on chain+address and performance indexes.
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

        // WalletMeta as JSON
        builder.Property(w => w.Meta)
            .HasConversion(
                meta => JsonSerializer.Serialize(meta.Data, JsonSerializationOptions.DatabaseStorage),
                json => WalletMeta.Create(
                    JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonSerializationOptions.DatabaseStorage))
                    .Value)
            .HasColumnName("meta")
            .HasColumnType("jsonb");

        // Tags collection stored as JSON array
        builder.Property<HashSet<Tag>>("_tags")
            .HasConversion(
                tags => JsonSerializer.Serialize(tags.Select(t => t.Value).ToArray(), JsonSerializationOptions.SimpleCollections),
                json => JsonSerializer.Deserialize<string[]>(json, JsonSerializationOptions.SimpleCollections) != null
                    ? JsonSerializer.Deserialize<string[]>(json, JsonSerializationOptions.SimpleCollections)!
                        .Select(Tag.From)
                        .ToHashSet()
                    : new HashSet<Tag>())
            .HasColumnName("tags")
            .HasColumnType("jsonb");

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
    }
}