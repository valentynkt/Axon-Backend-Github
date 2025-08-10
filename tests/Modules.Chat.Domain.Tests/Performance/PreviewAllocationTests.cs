using System;
using FluentAssertions;
using Xunit;
using Axon.Modules.Chat.Domain.Internal.Text;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Mothers;

namespace Axon.Modules.Chat.Domain.Tests.Performance;

/// <summary>
/// Tests to ensure preview generation follows allocation hygiene patterns:
/// - Content ≤100 chars: Preview reuses original string reference (0 allocations)
/// - Content >100 chars: Preview creates exactly one substring (1 allocation)
/// </summary>
public class PreviewAllocationTests
{
    [Fact]
    public void Preview_WhenContentLengthIs100OrLess_ShouldReuseOriginalReference()
    {
        // Arrange - Create a distinct string instance (not interned) to make ReferenceEquals meaningful
        var content = new string('a', 50); // 50 chars, definitely ≤100
        var conversation = ConversationBuilder.Create()
            .WithOwner(UserMother.DefaultUser())
            .WithTitle("Test Conversation")
            .Build();
        
        // Act
        var result = conversation.AppendUserMessage(
            MessageContent.Create(content).Value, 
            new FixedClock());
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Get the event to access the preview
        var events = conversation.DomainEvents;
        events.Should().HaveCount(1);
        var userMessageEvent = events[0].Should().BeOfType<Events.UserMessageAppendedEvent>().Subject;
        
        // The preview should be the exact same reference as the original content
        // This proves no extra string allocation occurred
        ReferenceEquals(userMessageEvent.ContentPreview, content).Should().BeTrue(
            "preview should reuse original string reference when content length ≤ 100");
    }
    
    [Fact]
    public void Preview_WhenContentLengthIs100_ShouldReuseOriginalReference()
    {
        // Arrange - Exactly 100 chars (boundary case)
        var content = new string('b', 100);
        var conversation = ConversationBuilder.Create()
            .WithOwner(UserMother.DefaultUser())
            .WithTitle("Test Conversation")
            .Build();
        
        // Act
        var result = conversation.AppendUserMessage(
            MessageContent.Create(content).Value, 
            new FixedClock());
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var events = conversation.DomainEvents;
        var userMessageEvent = events[0].Should().BeOfType<Events.UserMessageAppendedEvent>().Subject;
        
        // At exactly 100 chars, preview should still reuse original reference
        ReferenceEquals(userMessageEvent.ContentPreview, content).Should().BeTrue(
            "preview should reuse original string reference when content length = 100");
    }
    
    [Fact]
    public void Preview_WhenContentLengthIsOver100_ShouldCreateSubstring()
    {
        // Arrange - Content longer than 100 chars
        var content = new string('c', 150); // 150 chars, definitely >100
        var conversation = ConversationBuilder.Create()
            .WithOwner(UserMother.DefaultUser())
            .WithTitle("Test Conversation")
            .Build();
        
        // Act
        var result = conversation.AppendUserMessage(
            MessageContent.Create(content).Value, 
            new FixedClock());
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var events = conversation.DomainEvents;
        var userMessageEvent = events[0].Should().BeOfType<Events.UserMessageAppendedEvent>().Subject;
        
        // Preview should be a different reference (substring was created)
        ReferenceEquals(userMessageEvent.ContentPreview, content).Should().BeFalse(
            "preview should be a new substring when content length > 100");
        
        // Preview should be exactly 100 chars
        userMessageEvent.ContentPreview.Length.Should().Be(100,
            "preview should be truncated to exactly 100 characters");
        
        // Preview should be the first 100 chars of original content
        userMessageEvent.ContentPreview.Should().Be(content.Substring(0, 100),
            "preview should contain first 100 characters of original content");
    }
    
    [Fact]
    public void Preview_WithAssistantMessage_ShouldFollowSameAllocationPattern()
    {
        // Arrange - Test the pattern applies to assistant messages too
        var shortContent = new string('d', 75);
        var longContent = new string('e', 125);
        
        var conversation = ConversationBuilder.Create()
            .WithOwner(UserMother.DefaultUser())
            .WithTitle("Test Conversation")
            .WithUserMessage("Hello")
            .Build();
        
        // Act & Assert - Short content reuses reference
        var shortResult = conversation.AppendAssistantMessage(
            MessageContent.Create(shortContent).Value, 
            new FixedClock());
        
        shortResult.IsSuccess.Should().BeTrue();
        var shortEvent = conversation.DomainEvents[^1].Should().BeOfType<Events.AssistantMessageAppendedEvent>().Subject;
        ReferenceEquals(shortEvent.ContentPreview, shortContent).Should().BeTrue(
            "assistant message preview should reuse reference when ≤100 chars");
        
        // Act & Assert - Long content creates substring
        var longResult = conversation.AppendAssistantMessage(
            MessageContent.Create(longContent).Value, 
            new FixedClock());
        
        longResult.IsSuccess.Should().BeTrue();
        var longEvent = conversation.DomainEvents[^1].Should().BeOfType<Events.AssistantMessageAppendedEvent>().Subject;
        ReferenceEquals(longEvent.ContentPreview, longContent).Should().BeFalse(
            "assistant message preview should create substring when >100 chars");
        longEvent.ContentPreview.Length.Should().Be(100);
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void TextSlices_Preview_WhenLengthWithinLimit_ShouldReuseReference(int length)
    {
        // Arrange
        var content = new string('x', length);
        
        // Act
        var preview = TextSlices.Preview(content, 100);
        
        // Assert
        ReferenceEquals(preview, content).Should().BeTrue(
            $"TextSlices.Preview should reuse reference when length ({length}) ≤ 100");
    }
    
    [Theory]
    [InlineData(101)]
    [InlineData(150)]
    [InlineData(1000)]
    public void TextSlices_Preview_WhenLengthExceedsLimit_ShouldCreateSubstring(int length)
    {
        // Arrange
        var content = new string('y', length);
        
        // Act
        var preview = TextSlices.Preview(content, 100);
        
        // Assert
        ReferenceEquals(preview, content).Should().BeFalse(
            $"TextSlices.Preview should create substring when length ({length}) > 100");
        preview.Length.Should().Be(100);
        preview.Should().Be(content.Substring(0, 100));
    }
    
    [Fact]
    public void TextSlices_Preview_WhenSourceIsNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => TextSlices.Preview(null!, 100);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }
}