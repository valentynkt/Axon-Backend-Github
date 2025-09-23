using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;

public class WalletOwnershipConfiguration : IEntityTypeConfiguration<WalletOwnership>
{
    public void Configure(EntityTypeBuilder<WalletOwnership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("wallet_ownership", "identity");

        builder.HasKey(o => o.Id);
        
        builder.Property(o => o.Id)
            .HasConversion(id => id.Value, value => new WalletOwnershipId(value))
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(o => o.PrincipalId)
            .HasConversion(id => id.Value, value => new AxonUserId(value))
            .HasColumnName("principal_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(o => o.WalletId)
            .HasConversion(id => id.Value, value => new WalletId(value))
            .HasColumnName("wallet_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(o => o.AccessMode)
            .HasConversion<string>()
            .HasColumnName("access_mode")
            .HasMaxLength(20);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(20);

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");

        builder.Property(o => o.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Soft delete support
        builder.Property(o => o.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(o => o.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz");

        // Partial unique index for ownership pair (principal_id, wallet_id)
        builder.HasIndex(o => new { o.PrincipalId, o.WalletId })
            .IsUnique()
            .HasDatabaseName("ux_ownership_pair")
            .HasFilter("is_deleted = false");

        // Partial unique index for exclusive signing constraint per wallet
        builder.HasIndex(o => o.WalletId)
            .IsUnique()
            .HasDatabaseName("ux_exclusive_signing")
            .HasFilter("status = 'Verified' AND access_mode = 'Signing' AND is_deleted = false");

        // Performance indexes with soft delete filters
        builder.HasIndex(o => o.WalletId)
            .HasDatabaseName("idx_ownership_wallet_active")
            .HasFilter("is_deleted = false");

        builder.HasIndex(o => o.PrincipalId)
            .HasDatabaseName("idx_ownership_principal_active")
            .HasFilter("is_deleted = false");

        builder.HasIndex(o => o.Status).HasDatabaseName("idx_ownership_status");
    }
}