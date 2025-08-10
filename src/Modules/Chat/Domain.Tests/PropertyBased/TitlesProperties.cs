using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Axon.Modules.Chat.Domain.Tests.PropertyBased;

/// <summary>
/// Property-based tests for conversation title validation and behavior.
/// Tests title creation, updates, and preservation of whitespace/case.
/// </summary>
public class TitlesProperties
{
    [Property(Arbitrary = new[] { typeof(TitleGenerators) })]
    public Property ValidTitleLengths_UpdateTitle_ShouldSucceed(ValidTitleLength validTitle)
    {
        return Prop.ForAll(Gen.Constant(validTitle), title =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            
            // Act
            var result = builder.TryUpdateTitle(title.Value);
            
            // Assert
            return result.IsSuccess.Label($"Title length {title.Value.Length} should succeed");
        });
    }

    [Property(Arbitrary = new[] { typeof(TitleGenerators) })]
    public Property InvalidTitleLengths_UpdateTitle_ShouldFailWithCatalogCode(InvalidTitleLength invalidTitle)
    {
        return Prop.ForAll(Gen.Constant(invalidTitle), title =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            
            // Act
            var result = builder.TryUpdateTitle(title.Value);
            
            // Assert
            var expectedCode = title.Value.Trim().Length == 0 
                ? "CHAT_CONVERSATION_TITLE_EMPTY" 
                : "CHAT_CONVERSATION_TITLE_TOO_LONG";
                
            return result.IsFailure.Label("Should fail")
                .And(result.Error.Code == expectedCode).Label($"Should have error code {expectedCode}");
        });
    }

    [Property]
    public Property TitleInternalWhitespace_ShouldBePreserved()
    {
        // Generate titles with internal whitespace
        var titleGen = from len in Gen.Choose(10, 50)
                      from chars in Gen.ArrayOf(len, Gen.OneOf(Gen.Elements('a', 'b', 'c'), Gen.Constant(' ')))
                      let title = new string(chars).Trim() // Ensure not empty after trim
                      where title.Length > 0 && title.Length <= 200
                      select title;

        return Prop.ForAll(titleGen, title =>
        {
            // Arrange
            var originalTitle = "  " + title + "  "; // Add padding
            var builder = ConversationBuilder.Started();
            
            // Act
            var result = builder.TryUpdateTitle(originalTitle);
            
            // Assert
            return result.IsSuccess.Label("Should succeed")
                .And(builder.Conversation.Title == title).Label("Should preserve internal whitespace and trim outer");
        });
    }

    [Property]
    public Property TitleCase_ShouldBePreserved()
    {
        var titleGen = from len in Gen.Choose(5, 50)
                      from title in Gen.ArrayOf(len, Gen.OneOf(
                          Gen.Elements('A', 'B', 'C'),
                          Gen.Elements('a', 'b', 'c'),
                          Gen.Elements('X', 'y', 'Z')))
                      let titleStr = new string(title)
                      where titleStr.Trim().Length > 0 && titleStr.Trim().Length <= 200
                      select titleStr.Trim();

        return Prop.ForAll(titleGen, title =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            
            // Act
            var result = builder.TryUpdateTitle(title);
            
            // Assert
            return result.IsSuccess.Label("Should succeed")
                .And(builder.Conversation.Title == title).Label("Should preserve exact case");
        });
    }}

/// <summary>
/// Custom generators for title property testing.
/// </summary>
public static class TitleGenerators
{
    public static Arbitrary<ValidTitleLength> ValidTitleLength()
    {
        var gen = from len in Gen.Choose(1, 200)
                  select new ValidTitleLength(StringFactory.AlphaNum(len));
        return gen.ToArbitrary();
    }

    public static Arbitrary<InvalidTitleLength> InvalidTitleLength()
    {
        var emptyGen = Gen.OneOf(
            Gen.Constant(""),
            Gen.Constant("   "),
            Gen.Constant("\t\n  "));
        
        var tooLongGen = from len in Gen.Choose(201, 250)
                        select StringFactory.AlphaNum(len);
        
        var gen = Gen.OneOf(
            emptyGen.Select(s => new InvalidTitleLength(s)),
            tooLongGen.Select(s => new InvalidTitleLength(s)));
            
        return gen.ToArbitrary();
    }
}

/// <summary>
/// Wrapper for valid title lengths (1-200 chars).
/// </summary>
public readonly record struct ValidTitleLength(string Value);

/// <summary>
/// Wrapper for invalid title lengths (0 or >200 chars).
/// </summary>
public readonly record struct InvalidTitleLength(string Value);