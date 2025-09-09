using Axon.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.EntityConfigurations;

public class PrincipalChainDefaultConfiguration : IEntityTypeConfiguration<PrincipalChainDefault>
{
    public void Configure(EntityTypeBuilder<PrincipalChainDefault> builder)
    {
        builder.ToTable("principal_chain_default", "identity");

        builder.HasKey(d => new { d.PrincipalId, d.ChainId });
        
        builder.Property(d => d.PrincipalId)
            .HasConversion(id => id.Value, value => AxonId.From(value))
            .HasColumnName("principal_id")
            .HasColumnType("char(26)");

        builder.Property(d => d.ChainId)
            .HasColumnName("chain_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.WalletId)
            .HasConversion(id => id.Value, value => WalletId.From(value))
            .HasColumnName("wallet_id")
            .HasColumnType("char(26)")
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsConcurrencyToken();

        // Performance indexes
        builder.HasIndex(d => d.PrincipalId).HasDatabaseName("ix_default_principal_id");
        builder.HasIndex(d => d.WalletId).HasDatabaseName("ix_default_wallet_id");

        // Foreign key to ensure wallet is verified & signing for this principal
        builder.HasCheckConstraint("ck_default_wallet_verified_signing", 
            "EXISTS (SELECT 1 FROM identity.wallet_ownership wo WHERE wo.principal_id = principal_id AND wo.wallet_id = wallet_id AND wo.status = 'Verified' AND wo.access_mode = 'Signing')");
    }
}