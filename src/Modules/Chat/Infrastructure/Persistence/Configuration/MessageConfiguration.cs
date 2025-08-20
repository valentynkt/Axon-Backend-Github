using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Configuration;

using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Primitives.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);
        
        builder.Property(m => m.Id)
            .HasConversion(new MessageId.EfCoreValueConverter())
            .IsRequired();

        builder.Property(m => m.ConversationId)
            .HasConversion(new ConversationId.EfCoreValueConverter())
            .IsRequired();

        builder.Property(m => m.Role)
            .HasConversion(
                role => role.Value,
                value => MessageRole.FromString(value).Value!)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.Content)
            .HasConversion(new MessageContent.EfCoreValueConverter())
            .IsRequired();

        builder.Property(m => m.Sequence)
            .IsRequired();

        builder.Property(m => m.AiResponseId)
            .HasConversion(new AiResponseId.EfCoreValueConverter())
            .HasMaxLength(100);

        // Audit fields from AuditableDeletableEntity
        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.UpdatedAt);

        builder.Property(m => m.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Index for query performance
        builder.HasIndex(m => m.ConversationId);
        builder.HasIndex(m => new { m.ConversationId, m.Sequence });
    }
}