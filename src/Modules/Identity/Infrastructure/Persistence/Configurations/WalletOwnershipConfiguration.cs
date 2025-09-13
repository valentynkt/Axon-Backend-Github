using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;

public class WalletOwnershipConfiguration : IEntityTypeConfiguration<WalletOwnership>
{
    public void Configure(EntityTypeBuilder<WalletOwnership> builder)
    {
        builder.ToTable("wallet_ownership", "identity");

        builder.HasKey(o => o.Id);
        
        builder.Property(o => o.Id)
            .HasConversion(id => id.Value, value => new WalletOwnershipId(value))
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(o => o.PrincipalId)
            .HasConversion(id => id.Value, value => new AxonId(value))
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
            .HasColumnType("timestamptz")
            .IsConcurrencyToken();

        // Unique constraint for (principal_id, wallet_id)
        builder.HasIndex(o => new { o.PrincipalId, o.WalletId })
            .IsUnique()
            .HasDatabaseName("ux_ownership_principal_wallet");

        // Partial unique index for verified & signing owners per wallet
        builder.HasIndex(o => o.WalletId)
            .IsUnique()
            .HasDatabaseName("ux_wallet_verified_signing_owner")
            .HasFilter("status = 'Verified' AND access_mode = 'Signing'");

        // Performance indexes
        builder.HasIndex(o => o.PrincipalId).HasDatabaseName("ix_ownership_principal_id");
        builder.HasIndex(o => o.WalletId).HasDatabaseName("ix_ownership_wallet_id");
        builder.HasIndex(o => o.Status).HasDatabaseName("ix_ownership_status");
    }
}