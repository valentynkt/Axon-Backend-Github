using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;
using Shouldly;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Axon.Modules.Chat.Domain.Tests.PropertyBased;

/// <summary>
/// Property-based tests for message content preview generation.
/// Tests the 100-character preview rule with no ellipsis.
/// </summary>
public class PreviewProperties
{
    [Property(Arbitrary = new[] { typeof(PreviewGenerators) })]
    public Property ShortContent_PreviewShouldMatchOriginal(ShortContent shortContent)
    {
        return Prop.ForAll(Gen.Constant(shortContent), content =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            
            // Act
            builder.AppendUser(content.Value);
            var preview = builder.LastMessage?.Preview?.Value;
            
            // Assert
            return (preview == content.Value).Label($"Preview should match original: '{content.Value}'")
                .And((preview?.Length <= 100).Label("Preview should not exceed 100 characters"))
                .And(!preview?.Contains("...")).Label("Preview should not contain ellipsis");
        });
    }

    [Property(Arbitrary = new[] { typeof(PreviewGenerators) })]
    public Property LongContent_PreviewShouldBeTruncatedTo100(LongContent longContent)
    {
        return Prop.ForAll(Gen.Constant(longContent), content =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var expectedPreview = MessageExpectations.PreviewOf(content.Value);
            
            // Act
            builder.AppendUser(content.Value);
            var actualPreview = builder.LastMessage?.Preview?.Value;
            
            // Assert
            return (actualPreview == expectedPreview).Label($"Preview should match expected: '{expectedPreview}'")
                .And((actualPreview?.Length == 100).Label("Preview should be exactly 100 characters"))
                .And(!actualPreview?.Contains("...")).Label("Preview should not contain ellipsis")
                .And(content.Value.StartsWith(actualPreview ?? "")).Label("Preview should be substring from start");
        });
    }

    [Property]
    public Property BoundaryContent_99Chars_ShouldMatchOriginal()
    {
        return Prop.ForAll(Gen.Constant(StringFactory.Len99()), content =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            
            // Act
            builder.AppendUser(content);
            var preview = builder.LastMessage?.Preview?.Value;
            
            // Assert
            return (preview == content).Label("99-char content should have preview equal to original")
                .And((preview?.Length == 99).Label("Preview should be exactly 99 characters"));
        });
    }

    [Property]
    public Property BoundaryContent_100Chars_ShouldMatchOriginal()
    {
        return Prop.ForAll(Gen.Constant(StringFactory.Len100()), content =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            
            // Act
            builder.AppendUser(content);
            var preview = builder.LastMessage?.Preview?.Value;
            
            // Assert
            return (preview == content).Label("100-char content should have preview equal to original")
                .And((preview?.Length == 100).Label("Preview should be exactly 100 characters"));
        });
    }

    [Property]
    public Property BoundaryContent_101Chars_ShouldBeTruncated()
    {
        return Prop.ForAll(Gen.Constant(StringFactory.Len101()), content =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var expectedPreview = content.Substring(0, 100);
            
            // Act
            builder.AppendUser(content);
            var preview = builder.LastMessage?.Preview?.Value;
            
            // Assert
            return (preview == expectedPreview).Label("101-char content should be truncated to first 100 chars")
                .And((preview?.Length == 100).Label("Preview should be exactly 100 characters"))
                .And(!preview?.Contains("...")).Label("Truncated preview should not contain ellipsis");
        });
    }

    [Property]
    public Property WhitespaceHandling_ShouldPreserveSpacesInPreview()
    {
        return Prop.ForAll(PreviewGenerators.ContentWithWhitespace(), content =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var expectedPreview = MessageExpectations.PreviewOf(content);
            
            // Act
            builder.AppendUser(content);
            var preview = builder.LastMessage?.Preview?.Value;
            
            // Assert
            return (preview == expectedPreview).Label($"Whitespace should be preserved in preview for: '{content}'");
        });
    }
}

/// <summary>
/// Custom generators for preview property-based testing.
/// </summary>
public static class PreviewGenerators
{
    public static Arbitrary<ShortContent> ShortContents() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new ShortContent(StringFactory.Len1())),
            Gen.Constant(new ShortContent(StringFactory.Len10())),
            Gen.Constant(new ShortContent(StringFactory.Len50())),
            Gen.Constant(new ShortContent(StringFactory.Len99()))
        ));

    public static Arbitrary<LongContent> LongContents() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new LongContent(StringFactory.Len101())),
            Gen.Constant(new LongContent(StringFactory.Len200())),
            Gen.Constant(new LongContent(StringFactory.Len1k())),
            Gen.Constant(new LongContent(StringFactory.Len10k()))
        ));

    public static Gen<string> ContentWithWhitespace() =>
        Gen.OneOf(
            Gen.Constant("  leading spaces for preview"),
            Gen.Constant("trailing spaces for preview  "),
            Gen.Constant("Mixed\tTabs\nForPreview"),
            Gen.Constant(StringFactory.Len50() + "   trailing"),
            Gen.Constant("   " + StringFactory.Len50()),
            Gen.Constant(StringFactory.Len150()) // Will be truncated, preserving initial whitespace
        );
}

/// <summary>
/// Wrapper for short content (≤100 chars) in property tests.
/// </summary>
public record ShortContent(string Value);

/// <summary>
/// Wrapper for long content (>100 chars) in property tests.
/// </summary>
public record LongContent(string Value);