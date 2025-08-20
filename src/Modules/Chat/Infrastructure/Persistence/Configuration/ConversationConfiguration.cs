using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Configuration;

using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Primitives.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Id)
            .HasConversion(new ConversationId.EfCoreValueConverter())
            .IsRequired();

        builder.Property(c => c.OwnerId)
            .HasConversion(new UserId.EfCoreValueConverter())
            .IsRequired();

        builder.Property(c => c.Title)
            .IsRequired(false)
            .HasMaxLength(200);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(c => c.LastAiResponseId)
            .HasConversion(new AiResponseId.EfCoreValueConverter())
            .HasMaxLength(100);

        // Audit fields from AuditableDeletableEntity
        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt);

        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Version for optimistic concurrency (from AggregateRoot)
        builder.Property(c => c.Version)
            .IsConcurrencyToken();

        // Configure Messages as a private field backing
        builder.HasMany<Message>("_messages")
            .WithOne()
            .HasForeignKey("ConversationId")
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore domain events in persistence
        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.MessagesOrdered);
        builder.Ignore(c => c.MessageCount);
        builder.Ignore(c => c.IsActive);
    }
}