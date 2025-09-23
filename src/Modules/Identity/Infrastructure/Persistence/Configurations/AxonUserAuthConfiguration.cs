namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Entity Framework Core configuration for AxonUserAuth entity.
/// Maps the Identity framework user to the database with proper relationships and indexes.
/// </summary>
public class AxonUserAuthConfiguration : IEntityTypeConfiguration<AxonUserAuth>
{
    public void Configure(EntityTypeBuilder<AxonUserAuth> builder)
    {
        // Table mapping
        builder.ToTable("AxonUserAuth", "identity");

        // Primary key
        builder.HasKey(x => x.Id);

        // Configure AxonUserId as value converter
        builder.Property(x => x.AxonPrincipalId)
            .HasConversion(
                v => v.Value,
                v => new AxonUserId(v))
            .IsRequired()
            .HasColumnName("AxonPrincipalId");

        // Provider information
        builder.Property(x => x.ProviderType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.OriginalIssuer)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.OriginalSubject)
            .HasMaxLength(500)
            .IsRequired();

        // Dynamic.xyz fields
        builder.Property(x => x.DynamicEnvironmentId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(x => x.DynamicUserId)
            .HasMaxLength(100)
            .IsRequired(false);

        // Timestamps
        builder.Property(x => x.FirstAuthenticatedAt)
            .IsRequired(false);

        builder.Property(x => x.LastAuthenticatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Chain and wallet information
        builder.Property(x => x.PrimaryChainId)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(x => x.PrimaryWalletAddress)
            .HasMaxLength(100)
            .IsRequired(false);

        // Identity framework standard fields
        builder.Property(x => x.UserName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.NormalizedUserName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(x => x.NormalizedEmail)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(x => x.SecurityStamp)
            .HasMaxLength(100)
            .IsConcurrencyToken();

        builder.Property(x => x.ConcurrencyStamp)
            .HasMaxLength(100)
            .IsConcurrencyToken();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(50)
            .IsRequired(false);

        // Indexes for performance
        builder.HasIndex(x => x.AxonPrincipalId)
            .IsUnique()
            .HasDatabaseName("IX_AxonUserAuth_AxonPrincipalId");

        builder.HasIndex(x => x.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("IX_AxonUserAuth_NormalizedUserName");

        builder.HasIndex(x => x.NormalizedEmail)
            .HasDatabaseName("IX_AxonUserAuth_NormalizedEmail");

        builder.HasIndex(x => new { x.ProviderType, x.OriginalSubject })
            .HasDatabaseName("IX_AxonUserAuth_ProviderType_OriginalSubject");

        builder.HasIndex(x => x.DynamicUserId)
            .HasDatabaseName("IX_AxonUserAuth_DynamicUserId")
            .HasFilter("\"DynamicUserId\" IS NOT NULL");

        builder.HasIndex(x => x.PrimaryWalletAddress)
            .HasDatabaseName("IX_AxonUserAuth_PrimaryWalletAddress")
            .HasFilter("\"PrimaryWalletAddress\" IS NOT NULL");

        // Relationship with AxonPrincipal
        // Note: We use shadow foreign key since AxonPrincipal is in a different bounded context
        builder.HasOne<AxonPrincipal>()
            .WithOne()
            .HasForeignKey<AxonUserAuth>(x => x.AxonPrincipalId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_AxonUserAuth_AxonPrincipal");
    }
}