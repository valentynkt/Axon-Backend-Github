using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;

public class CredentialConfiguration : IEntityTypeConfiguration<IdentityCredential>
{
    public void Configure(EntityTypeBuilder<IdentityCredential> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Credential", "identity");

        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new IdentityCredentialId(value))
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(c => c.PrincipalId)
            .HasConversion(id => id.Value, value => new AxonUserId(value))
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
            .HasColumnType("timestamptz");

        // Soft delete support
        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz");

        // Partial unique index for credential provider uniqueness (network-agnostic)
        builder.HasIndex(c => new { c.Provider, c.Issuer, c.Subject })
            .IsUnique()
            .HasDatabaseName("ux_credential_provider")
            .HasFilter("is_deleted = false");

        // Performance indexes
        builder.HasIndex(c => c.PrincipalId).HasDatabaseName("idx_credential_principal_id");
        builder.HasIndex(c => c.Provider).HasDatabaseName("idx_credential_provider");

        // Override the global soft delete query filter
        // This entity is a navigation property of AxonPrincipal aggregate
        // EF Core doesn't allow query filters on owned/navigation entities
        builder.HasQueryFilter(e => true);
    }
}