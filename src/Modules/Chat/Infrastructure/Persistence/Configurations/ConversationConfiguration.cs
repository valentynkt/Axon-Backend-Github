using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Errors;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;


namespace Axon.Modules.Chat.Infrastructure.Persistence.Configurations;
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(new ConversationId.EfCoreValueConverter())
            .IsRequired();

        builder.Property(c => c.OwnerId)
            .HasConversion(new AxonUserId.EfCoreValueConverter())
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
        // Maps to PostgreSQL xmin system column for automatic concurrency control
        builder.Property(c => c.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        // Configure Messages as owned collection - they are part of the aggregate
        // This ensures they don't have independent concurrency tracking
        builder.OwnsMany<Message>("_messages", messages =>
        {
            messages.ToTable("Messages");
            messages.WithOwner().HasForeignKey(m => m.ConversationId);

            // Composite key for owned entity
            messages.HasKey(m => new { m.ConversationId, m.Id });

            messages.Property(m => m.Id)
                .HasConversion(new MessageId.EfCoreValueConverter())
                .IsRequired();

            messages.Property(m => m.ConversationId)
                .HasConversion(new ConversationId.EfCoreValueConverter())
                .IsRequired();

            messages.Property(m => m.Role)
                .HasConversion(
                    role => role.Value,
                    value => MessageRole.FromString(value).Value!)
                .IsRequired()
                .HasMaxLength(50);

            messages.Property(m => m.Content)
                .HasConversion(new MessageContent.EfCoreValueConverter())
                .IsRequired();

            messages.Property(m => m.Sequence)
                .IsRequired();

            messages.Property(m => m.AiResponseId)
                .HasConversion(new AiResponseId.EfCoreValueConverter())
                .HasMaxLength(100);

            // Audit fields from AuditableDeletableEntity
            messages.Property(m => m.CreatedAt)
                .IsRequired();

            messages.Property(m => m.UpdatedAt);

            messages.Property(m => m.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // Index for query performance
            messages.HasIndex(m => m.ConversationId);
            messages.HasIndex(m => new { m.ConversationId, m.Sequence });

            messages.UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // Ignore domain events and calculated properties in persistence
        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.IsActive);
        builder.Ignore(c => c.HasDefaultTitle);
    }
}