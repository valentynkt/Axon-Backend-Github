using Axon.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;

public class CredentialConfiguration : IEntityTypeConfiguration<IdentityCredential>
{
    public void Configure(EntityTypeBuilder<IdentityCredential> builder)
    {
        builder.ToTable("credential", "identity");

        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new IdentityCredentialId(value))
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(c => c.PrincipalId)
            .HasConversion(id => id.Value, value => new AxonId(value))
            .HasColumnName("principal_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.Provider)
            .HasColumnName("provider")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Issuer)
            .HasColumnName("issuer")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.Subject)
            .HasColumnName("subject")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.LastSeenAt)
            .HasColumnName("last_seen_at")
            .HasColumnType("timestamptz");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsConcurrencyToken();

        // Unique constraint for (provider, issuer, subject)
        builder.HasIndex(c => new { c.Provider, c.Issuer, c.Subject })
            .IsUnique()
            .HasDatabaseName("ux_credential_provider_issuer_subject");

        // Performance indexes
        builder.HasIndex(c => c.PrincipalId).HasDatabaseName("ix_credential_principal_id");
        builder.HasIndex(c => c.Provider).HasDatabaseName("ix_credential_provider");
    }
}