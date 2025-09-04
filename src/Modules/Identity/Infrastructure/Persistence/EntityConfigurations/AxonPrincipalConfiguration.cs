using System.Text.Json;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Identity.Infrastructure.Persistence.EntityConfigurations;

/// <summary>
/// EF Core configuration for AxonPrincipal aggregate root and its owned entities.
/// Includes critical unique constraints and performance indexes.
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

        // Owned entity: PrincipalProfile
        builder.OwnsOne(p => p.Profile, profile =>
        {
            // Owned entities don't need their own Id - ignore inherited properties
            profile.Ignore(pp => pp.Id);
            
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

            // Complex type: ChainDefaults stored as JSON
            profile.Property(pp => pp.DefaultPerChain)
                .HasConversion(
                    defaults => JsonSerializer.Serialize(defaults.Value, JsonSerializationOptions.DatabaseStorage),
                    json => ChainDefaults.Create(
                        JsonSerializer.Deserialize<Dictionary<ChainId, WalletId>>(json, JsonSerializationOptions.DatabaseStorage) 
                            ?? new Dictionary<ChainId, WalletId>())
                        .Value)
                .HasColumnName("default_per_chain")
                .HasColumnType("jsonb");
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
    }
}