using System.Text.Json;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.EntityConfigurations;

/// <summary>
/// EF Core configuration for IdentityCredential entity.
/// Includes critical unique constraints and performance indexes.
/// </summary>
public sealed class IdentityCredentialConfiguration : IEntityTypeConfiguration<IdentityCredential>
{
    public void Configure(EntityTypeBuilder<IdentityCredential> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        
        // Table configuration
        builder.ToTable("identity_credentials", "identity");
        
        // Primary key
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(
                id => id.Value,
                value => new IdentityCredentialId(value))
            .HasColumnName("id");

        // Foreign key to AxonPrincipal
        builder.Property(c => c.AxonId)
            .HasConversion(
                id => id.Value,
                value => new AxonId(value))
            .HasColumnName("axon_id");

        // Value object conversions
        builder.Property(c => c.ProviderType)
            .HasConversion(
                type => type.Value,
                value => ProviderType.From(value))
            .HasColumnName("provider_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Issuer)
            .HasColumnName("issuer")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.Subject)
            .HasColumnName("subject")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.EnvironmentId)
            .HasColumnName("environment_id")
            .HasMaxLength(100);

        builder.Property(c => c.VerifiedAt)
            .HasColumnName("verified_at");

        builder.Property(c => c.LastSeenAt)
            .HasColumnName("last_seen_at");

        builder.Property(c => c.Metadata)
            .HasConversion(
                meta => JsonSerializer.Serialize(meta.Value, JsonSerializationOptions.DatabaseStorage),
                json => CredentialMetadata.Create(
                    JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonSerializationOptions.DatabaseStorage))
                    .Value)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        // Audit columns
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted");
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");

        // Query filters for soft deletion
        builder.HasQueryFilter(c => !c.IsDeleted);

        // 🚨 CRITICAL: Unique constraint on (ProviderType, Issuer, Subject)
        builder.HasIndex(c => new { c.ProviderType, c.Issuer, c.Subject })
            .IsUnique()
            .HasDatabaseName("ix_credentials_provider_issuer_subject_unique");

        // Performance indexes
        builder.HasIndex(c => c.AxonId)
            .HasDatabaseName("ix_credentials_axon_id");
        
        builder.HasIndex(c => c.LastSeenAt)
            .HasDatabaseName("ix_credentials_last_seen_at");
        
        builder.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("ix_credentials_created_at");
    }
}