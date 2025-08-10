using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Axon.Modules.Chat.Domain.Aggregates;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Conversation aggregate.
/// Configures unique constraints, indexes, and optimistic concurrency.
/// </summary>
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations", "chat");

        // Primary key
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(
                id => id.Value,
                value => ConversationId.From(value))
            .IsRequired();

        // Owner relationship
        builder.Property(c => c.OwnerId)
            .HasConversion(
                id => id.Value,
                value => UserId.From(value))
            .IsRequired();
        builder.HasIndex(c => c.OwnerId);

        // Status
        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Title value object
        builder.OwnsOne(c => c.Title, title =>
        {
            title.Property(t => t.Value)
                .HasColumnName("Title")
                .HasMaxLength(200)
                .IsRequired();
        });

        // Title metadata
        builder.Property(c => c.IsDefaultTitle)
            .IsRequired();

        // Tracking fields
        builder.Property(c => c.MessagesCount)
            .IsRequired();

        builder.Property(c => c.LastAuthor)
            .HasConversion(
                role => role != null ? role.Value : null,
                value => value != null ? MessageRole.FromString(value).Value : null)
            .HasMaxLength(20);

        builder.Property(c => c.NextSequence)
            .IsRequired();

        // Temporal fields
        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        // Optimistic concurrency control using Version from AggregateRoot
        builder.Property(c => c.Version)
            .IsConcurrencyToken()
            .IsRequired();

        // Messages relationship
        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => new { c.OwnerId, c.Status });
        builder.HasIndex(c => c.CreatedAt);

        // Ignore domain events (handled separately)
        builder.Ignore(c => c.DomainEvents);
    }
}