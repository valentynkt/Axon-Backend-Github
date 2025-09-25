using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Errors;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Axon.Modules.Chat.Domain.Entities;


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
        // Only IsRowVersion() is needed - Npgsql automatically handles xmin mapping
        builder.Property(c => c.Version)
            .IsRowVersion();

        // Configure Messages navigation property using the public property that exposes the private field
        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure the navigation to use field access for the private backing field
        builder.Navigation(c => c.Messages)
            .EnableLazyLoading(false)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Ignore domain events and calculated properties in persistence
        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.MessagesOrdered);
        builder.Ignore(c => c.MessageCount);
        builder.Ignore(c => c.IsActive);
    }
}