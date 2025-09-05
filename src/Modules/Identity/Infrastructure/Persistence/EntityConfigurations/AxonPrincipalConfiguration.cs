using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.EntityConfigurations;

/// <summary>
/// EF Core configuration for AxonPrincipal aggregate root and its owned entities.
/// Now uses PrincipalChainDefault collection instead of JSON-based chain defaults.
/// </summary>
public sealed class AxonPrincipalConfiguration : IEntityTypeConfiguration<AxonPrincipal>
{
    public void Configure(EntityTypeBuilder<AxonPrincipal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        
        // Table configuration
        builder.ToTable("axon_principals", "identity");
        
        // Primary key
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new AxonId(value))
            .HasColumnName("id");

        // Value object conversions
        builder.Property(p => p.Type)
            .HasConversion(
                type => type.Value,
                value => PrincipalType.From(value))
            .HasColumnName("type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.PrimaryEmailHash)
            .HasConversion(
                hash => hash != null ? hash.Value.Value : null,
                value => value != null ? EmailHash.From(value) : null)
            .HasColumnName("primary_email_hash")
            .HasMaxLength(64); // SHA256 hex = 64 chars

        // Owned entity: PrincipalProfile (simplified - no more ChainDefaults)
        builder.OwnsOne(p => p.Profile, profile =>
        {
            // Ignore inherited Entity properties for owned entities
            profile.Ignore(pp => pp.Id);
            profile.Ignore(pp => pp.CreatedAt);
            profile.Ignore(pp => pp.UpdatedAt);
            
            profile.Property(pp => pp.PreferredLanguage)
                .HasConversion(
                    lang => lang.Value,
                    value => PreferredLanguage.From(value))
                .HasColumnName("preferred_language")
                .HasMaxLength(10)
                .IsRequired();

            profile.Property(pp => pp.RiskTier)
                .HasConversion(
                    tier => tier.Value,
                    value => RiskTier.From(value))
                .HasColumnName("risk_tier")
                .HasMaxLength(20)
                .IsRequired();
        });

        // Owned collection: PrincipalChainDefault (separate table)
        builder.OwnsMany(p => p.ChainDefaults, chainDefault =>
        {
            chainDefault.ToTable("principal_chain_defaults", "identity");

            // Primary key for the owned entity
            chainDefault.HasKey(cd => cd.Id);
            chainDefault.Property(cd => cd.Id)
                .HasConversion(
                    id => id,
                    value => value)
                .HasColumnName("id");

            // Foreign key back to AxonPrincipal
            chainDefault.Property(cd => cd.AxonId)
                .HasConversion(
                    id => id.Value,
                    value => new AxonId(value))
                .HasColumnName("axon_id")
                .IsRequired();

            // Chain and Wallet IDs
            chainDefault.Property(cd => cd.ChainId)
                .HasConversion(
                    id => id.Value,
                    value => ChainId.From(value))
                .HasColumnName("chain_id")
                .HasMaxLength(50)
                .IsRequired();

            chainDefault.Property(cd => cd.WalletId)
                .HasConversion(
                    id => id.Value,
                    value => new WalletId(value))
                .HasColumnName("wallet_id")
                .IsRequired();

            // Audit properties
            chainDefault.Property(cd => cd.CreatedAt).HasColumnName("created_at");
            chainDefault.Property(cd => cd.UpdatedAt).HasColumnName("updated_at");

            // 🚨 CRITICAL: Unique constraint on (AxonId, ChainId) - one default per chain
            chainDefault.HasIndex(cd => new { cd.AxonId, cd.ChainId })
                .IsUnique()
                .HasDatabaseName("ix_principal_chain_defaults_axon_chain_unique");

            // Foreign key to wallets table (referential integrity)
            chainDefault.HasIndex(cd => cd.WalletId)
                .HasDatabaseName("ix_principal_chain_defaults_wallet_id");

            // Performance index for chain-based queries
            chainDefault.HasIndex(cd => cd.ChainId)
                .HasDatabaseName("ix_principal_chain_defaults_chain_id");
        });

        // Configure navigation properties without creating additional foreign keys
        // The relationships are defined in the child entity configurations
        builder.HasMany(p => p.Credentials)
            .WithOne()
            .HasForeignKey(c => c.AxonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.WalletOwnerships)
            .WithOne()
            .HasForeignKey(o => o.AxonId)
            .OnDelete(DeleteBehavior.Cascade);

        // Audit properties
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        // Query filters
        builder.HasQueryFilter(p => !p.IsDeleted);

        // Performance indexes based on repository queries
        builder.HasIndex(p => p.Type)
            .HasDatabaseName("ix_axon_principals_type");
        
        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("ix_axon_principals_created_at");
            
        // Composite index for common filter operations (not deleted + type)
        builder.HasIndex(p => new { p.IsDeleted, p.Type })
            .HasDatabaseName("ix_axon_principals_is_deleted_type")
            .HasFilter("is_deleted = false");

        // Index on primary email hash for lookups
        builder.HasIndex(p => p.PrimaryEmailHash)
            .HasDatabaseName("ix_axon_principals_primary_email_hash")
            .HasFilter("primary_email_hash IS NOT NULL");

        // Check constraints for data integrity
        builder.HasCheckConstraint("ck_axon_principals_type", 
            "type IN ('human', 'service')");

        builder.HasCheckConstraint("ck_axon_principals_preferred_language",
            "preferred_language IN ('en', 'es', 'fr', 'de', 'ja', 'ko', 'zh')");

        builder.HasCheckConstraint("ck_axon_principals_risk_tier",
            "risk_tier IN ('low', 'medium', 'high', 'critical')");

        // Check constraint for email hash format (if provided)
        builder.HasCheckConstraint("ck_axon_principals_email_hash_format",
            "primary_email_hash IS NULL OR (LENGTH(primary_email_hash) = 64 AND primary_email_hash ~ '^[a-f0-9]+$')");
    }
}