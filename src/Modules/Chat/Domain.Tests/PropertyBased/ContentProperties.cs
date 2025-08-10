using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Axon.Modules.Chat.Domain.Tests.PropertyBased;

/// <summary>
/// Property-based tests for message content validation and behavior.
/// Tests content length limits and failure handling.
/// </summary>
public class ContentProperties
{
    [Property(Arbitrary = new[] { typeof(ContentGenerators) })]
    public Property ValidContentLengths_AppendUser_ShouldSucceed(ValidContentLength validContent)
    {
        return Prop.ForAll(Gen.Constant(validContent), content =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var originalCount = builder.MessageCount;
            
            // Act
            var result = builder.TryAppendUser(content.Value);
            
            // Assert
            return result.IsSuccess.Label($"Content length {content.Value.Length} should succeed")
                .And(builder.MessageCount == originalCount + 1).Label("Message count should increment");
        });
    }

    [Property(Arbitrary = new[] { typeof(ContentGenerators) })]
    public Property ValidContentLengths_AppendAssistant_ShouldSucceed(ValidContentLength validContent)
    {
        return Prop.ForAll(Gen.Constant(validContent), content =>
        {
            // Arrange  
            var builder = ConversationBuilder.Started().AppendUser("Setup message");
            var originalCount = builder.MessageCount;
            
            // Act
            var result = builder.TryAppendAssistant(content.Value);
            
            // Assert
            return result.IsSuccess.Label($"Content length {content.Value.Length} should succeed")
                .And(builder.MessageCount == originalCount + 1).Label("Message count should increment");
        });
    }

    [Property(Arbitrary = new[] { typeof(ContentGenerators) })]
    public Property InvalidContentLengths_ShouldFailWithCatalogCode(InvalidContentLength invalidContent)
    {
        return Prop.ForAll(Gen.Constant(invalidContent), content =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var originalCount = builder.MessageCount;
            
            // Act
            var result = builder.TryAppendUser(content.Value);
            
            // Assert
            var expectedCode = string.IsNullOrWhiteSpace(content.Value?.Trim())
                ? "CHAT_MESSAGE_CONTENT_EMPTY"
                : "CHAT_MESSAGE_CONTENT_TOO_LONG";
                
            return result.IsFailure.Label("Should fail")
                .And(result.Error.Code == expectedCode).Label($"Should have error code {expectedCode}")
                .And(builder.MessageCount == originalCount).Label("Message count should not change on failure");
        });
    }

    [Property]
    public Property WhitespaceAndCasePreservation_ShouldMaintainOriginal()
    {
        return Prop.ForAll(ContentGenerators.ValidContentWithWhitespace(), content =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            
            // Act
            builder.AppendUser(content);
            var retrievedContent = builder.LastMessage?.Content?.Value;
            
            // Assert
            return (retrievedContent == content).Label($"Content '{content}' should be preserved exactly");
        });
    }
}

/// <summary>
/// Custom generators for content property-based testing.
/// </summary>
public static class ContentGenerators
{
    public static Arbitrary<ValidContentLength> ValidContentLengths() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new ValidContentLength(StringFactory.Len1())),
            Gen.Constant(new ValidContentLength(StringFactory.Len10())),
            Gen.Constant(new ValidContentLength(StringFactory.Len100())),
            Gen.Constant(new ValidContentLength(StringFactory.Len1k())),
            Gen.Constant(new ValidContentLength(StringFactory.Len10k())),
            Gen.Constant(new ValidContentLength(StringFactory.Len50k())),
            Gen.Constant(new ValidContentLength(StringFactory.Len100k()))
        ));

    public static Arbitrary<InvalidContentLength> InvalidContentLengths() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new InvalidContentLength("")),
            Gen.Constant(new InvalidContentLength("   ")),
            Gen.Constant(new InvalidContentLength("\t\n  ")),
            Gen.Constant(new InvalidContentLength(null)),
            Gen.Constant(new InvalidContentLength(StringFactory.Len100kPlus1()))
        ));

    public static Gen<string> ValidContentWithWhitespace() =>
        Gen.OneOf(
            Gen.Constant("  leading spaces"),
            Gen.Constant("trailing spaces  "),
            Gen.Constant("  both sides  "),
            Gen.Constant("Mixed\tTabs\nAndSpaces"),
            Gen.Constant("CamelCasePreserved"),
            Gen.Constant("UPPERCASE_PRESERVED"),
            Gen.Constant("lowercase_preserved")
        );
}

/// <summary>
/// Wrapper for valid content lengths in property tests.
/// </summary>
public record ValidContentLength(string Value);

/// <summary>
/// Wrapper for invalid content lengths in property tests.
/// </summary>
public record InvalidContentLength(string? Value);