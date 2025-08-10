using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Message entity.
/// Configures unique constraint on (ConversationId, Sequence) to prevent duplicates.
/// </summary>
public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages", "chat");

        // Primary key
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(
                id => id.Value,
                value => MessageId.From(value))
            .IsRequired();

        // Conversation relationship
        builder.Property(m => m.ConversationId)
            .HasConversion(
                id => id.Value,
                value => ConversationId.From(value))
            .IsRequired();

        // Role
        builder.Property(m => m.Role)
            .HasConversion(
                role => role.Value,
                value => MessageRole.FromString(value).Value!)
            .HasMaxLength(20)
            .IsRequired();

        // Content value object
        builder.OwnsOne(m => m.Content, content =>
        {
            content.Property(c => c.Value)
                .HasColumnName("Content")
                .HasMaxLength(100_000)
                .IsRequired();
        });

        // Sequence
        builder.Property(m => m.Sequence)
            .IsRequired();

        // Timestamp
        builder.Property(m => m.Timestamp)
            .IsRequired();

        // CRITICAL: Unique constraint on (ConversationId, Sequence)
        // This prevents duplicate sequences and ensures ordering integrity
        builder.HasIndex(m => new { m.ConversationId, m.Sequence })
            .IsUnique()
            .HasDatabaseName("IX_Messages_ConversationId_Sequence");

        // Performance indexes
        builder.HasIndex(m => m.ConversationId);
        builder.HasIndex(m => m.Timestamp);
        builder.HasIndex(m => new { m.ConversationId, m.Timestamp });
    }
}