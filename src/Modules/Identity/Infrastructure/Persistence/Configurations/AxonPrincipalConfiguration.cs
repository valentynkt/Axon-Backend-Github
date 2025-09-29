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
        builder.Property(p => p.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        // Configure child entities as owned types - they are part of the aggregate
        // This ensures they don't have independent concurrency tracking

        // Configure Credentials as owned collection
        builder.OwnsMany(p => p.Credentials, credentials =>
        {
            credentials.ToTable("Credential", "identity");
            credentials.WithOwner().HasForeignKey(c => c.PrincipalId);

            // Composite key for owned entity
            credentials.HasKey(c => new { c.PrincipalId, c.Id });

            credentials.Property(c => c.Id)
                .HasConversion(id => id.Value, value => new IdentityCredentialId(value))
                .HasColumnName("id")
                .HasColumnType("uuid");

            credentials.Property(c => c.PrincipalId)
                .HasConversion(id => id.Value, value => new AxonUserId(value))
                .HasColumnName("principal_id")
                .HasColumnType("uuid")
                .IsRequired();

            credentials.Property(c => c.Provider)
                .HasColumnName("provider")
                .HasMaxLength(100)
                .IsRequired();

            credentials.Property(c => c.Issuer)
                .HasColumnName("issuer")
                .HasMaxLength(500)
                .IsRequired();

            credentials.Property(c => c.Subject)
                .HasColumnName("subject")
                .HasMaxLength(500)
                .IsRequired();

            credentials.Property(c => c.LastSeenAt)
                .HasColumnName("last_seen_at")
                .HasColumnType("timestamptz");

            credentials.Property(c => c.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz");

            credentials.Property(c => c.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz");

            credentials.Property(c => c.IsDeleted)
                .HasColumnName("is_deleted")
                .HasDefaultValue(false)
                .IsRequired();

            credentials.Property(c => c.DeletedAt)
                .HasColumnName("deleted_at")
                .HasColumnType("timestamptz");

            // Unique constraint for Provider-Issuer-Subject combination
            credentials.HasIndex(c => new { c.Provider, c.Issuer, c.Subject })
                .IsUnique()
                .HasDatabaseName("ux_credential_provider")
                .HasFilter("is_deleted = false");

            credentials.UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // Configure WalletOwnerships as owned collection
        builder.OwnsMany(p => p.WalletOwnerships, ownerships =>
        {
            ownerships.ToTable("WalletOwnership", "identity");
            ownerships.WithOwner().HasForeignKey(o => o.PrincipalId);

            // Composite key for owned entity
            ownerships.HasKey(o => new { o.PrincipalId, o.Id });

            ownerships.Property(o => o.Id)
                .HasConversion(id => id.Value, value => new WalletOwnershipId(value))
                .HasColumnName("id")
                .HasColumnType("uuid");

            ownerships.Property(o => o.PrincipalId)
                .HasConversion(id => id.Value, value => new AxonUserId(value))
                .HasColumnName("principal_id")
                .HasColumnType("uuid")
                .IsRequired();

            ownerships.Property(o => o.WalletId)
                .HasConversion(id => id.Value, value => new WalletId(value))
                .HasColumnName("wallet_id")
                .HasColumnType("uuid")
                .IsRequired();

            ownerships.Property(o => o.AccessMode)
                .HasConversion<string>()
                .HasColumnName("access_mode")
                .HasMaxLength(20);

            ownerships.Property(o => o.Status)
                .HasConversion<string>()
                .HasColumnName("status")
                .HasMaxLength(20);

            ownerships.Property(o => o.VerificationSource)
                .HasConversion<string>()
                .HasColumnName("verification_source")
                .HasMaxLength(50);

            ownerships.Property(o => o.VerifiedAt)
                .HasColumnName("verified_at")
                .HasColumnType("timestamptz");

            ownerships.Property(o => o.RevokedAt)
                .HasColumnName("revoked_at")
                .HasColumnType("timestamptz");

            ownerships.Property(o => o.RevokeReason)
                .HasColumnName("revoke_reason")
                .HasMaxLength(500);

            ownerships.Property(o => o.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz");

            ownerships.Property(o => o.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz");

            ownerships.Property(o => o.IsDeleted)
                .HasColumnName("is_deleted")
                .HasDefaultValue(false)
                .IsRequired();

            ownerships.Property(o => o.DeletedAt)
                .HasColumnName("deleted_at")
                .HasColumnType("timestamptz");

            // Indexes
            ownerships.HasIndex(o => o.WalletId).HasDatabaseName("idx_ownership_wallet_id");

            // Unique constraint for (PrincipalId, WalletId) pair
            ownerships.HasIndex(o => new { o.PrincipalId, o.WalletId })
                .HasDatabaseName("ux_ownership_pair")
                .IsUnique()
                .HasFilter("is_deleted = false");

            // Partial unique index for exclusive signing ownership
            ownerships.HasIndex(o => new { o.WalletId, o.AccessMode, o.Status })
                .HasDatabaseName("ux_exclusive_signing")
                .IsUnique()
                .HasFilter("access_mode = 'Signing' AND status = 'Verified' AND is_deleted = false");

            ownerships.UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // Configure PrincipalChainDefaults as owned collection
        builder.OwnsMany(p => p.PrincipalChainDefaults, chainDefaults =>
        {
            chainDefaults.ToTable("PrincipalChainDefault", "identity");
            chainDefaults.WithOwner().HasForeignKey(d => d.PrincipalId);

            // Composite key for owned entity
            chainDefaults.HasKey(d => new { d.PrincipalId, d.Id });

            chainDefaults.Property(d => d.Id)
                .HasColumnName("id")
                .HasColumnType("uuid");

            chainDefaults.Property(d => d.PrincipalId)
                .HasConversion(id => id.Value, value => new AxonUserId(value))
                .HasColumnName("principal_id")
                .HasColumnType("uuid")
                .IsRequired();

            chainDefaults.Property(d => d.ChainId)
                .HasColumnName("chain_id")
                .HasMaxLength(50)
                .IsRequired();

            chainDefaults.Property(d => d.WalletId)
                .HasConversion(id => id.Value, value => new WalletId(value))
                .HasColumnName("wallet_id")
                .HasColumnType("uuid")
                .IsRequired();

            chainDefaults.Property(d => d.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz");

            chainDefaults.Property(d => d.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz");

            chainDefaults.Property(d => d.IsDeleted)
                .HasColumnName("is_deleted")
                .HasDefaultValue(false)
                .IsRequired();

            chainDefaults.Property(d => d.DeletedAt)
                .HasColumnName("deleted_at")
                .HasColumnType("timestamptz");

            // Unique index for chain default constraint
            chainDefaults.HasIndex(d => new { d.PrincipalId, d.ChainId })
                .IsUnique()
                .HasDatabaseName("ux_chain_default")
                .HasFilter("is_deleted = false");

            // Performance indexes
            chainDefaults.HasIndex(d => d.PrincipalId).HasDatabaseName("idx_default_principal_id");
            chainDefaults.HasIndex(d => d.WalletId).HasDatabaseName("idx_default_wallet_id");

            chainDefaults.UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // Indexes
        builder.HasIndex(p => p.Type).HasDatabaseName("ix_principal_type");
        builder.HasIndex(p => p.RiskTier).HasDatabaseName("ix_principal_risk_tier");
        builder.HasIndex(p => p.CreatedAt).HasDatabaseName("ix_principal_created_at");
    }
}