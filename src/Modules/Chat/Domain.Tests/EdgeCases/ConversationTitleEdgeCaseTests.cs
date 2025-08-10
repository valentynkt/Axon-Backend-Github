using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Tests.EdgeCases.TestData;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.EdgeCases;

/// <summary>
/// Tests title edge cases and boundary conditions per STORY CH-DOM-010.
/// Focuses on title limits, empty handling, and error catalog conformance.
/// </summary>
public class ConversationTitleEdgeCaseTests
{
    private readonly UserId _ownerId = UserId.New();
    private readonly FixedClock _clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void TIT_START_EMPTY_OK_StartWithEmptyTitle_ShouldAllowAndSetDefaultFlag()
    {
        // Act
        var result = Conversation.Start(_ownerId, string.Empty, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Title.ShouldBeEmpty();
        result.Value.IsDefaultTitle.ShouldBeTrue();
    }

    [Fact]
    public void TIT_START_NULL_OK_StartWithNullTitle_ShouldAllowAndSetDefaultFlag()
    {
        // Act
        var result = Conversation.Start(_ownerId, null, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Title.ShouldBeEmpty();
        result.Value.IsDefaultTitle.ShouldBeTrue();
    }

    [Fact]
    public void TIT_START_WHITESPACE_OK_StartWithWhitespaceOnlyTitle_ShouldTreatAsEmpty()
    {
        // Arrange
        var whitespaceTitle = "   \t\n   ";

        // Act
        var result = Conversation.Start(_ownerId, whitespaceTitle, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Title.ShouldBeEmpty();
        result.Value.IsDefaultTitle.ShouldBeTrue();
    }    [Fact]
    public void TIT_UPD_1_OK_UpdateTitleToLength1_ShouldSucceed()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .SetOwner(_ownerId)
            .SetClock(_clock)
            .Start()
            .Conversation;

        // Act
        var result = conversation.UpdateTitle(StringGenerators.Title.Length1, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.Title.ShouldBe(StringGenerators.Title.Length1);
        conversation.IsDefaultTitle.ShouldBeFalse();
    }

    [Fact]
    public void TIT_UPD_200_OK_UpdateTitleToLength200_ShouldSucceed()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .SetOwner(_ownerId)
            .SetClock(_clock)
            .Start()
            .Conversation;

        // Act
        var result = conversation.UpdateTitle(StringGenerators.Title.Length200, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.Title.ShouldBe(StringGenerators.Title.Length200);
        conversation.IsDefaultTitle.ShouldBeFalse();
    }

    [Fact]
    public void TIT_UPD_0_FAIL_UpdateTitleToLength0_ShouldFailWithCatalogCode()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .SetOwner(_ownerId)
            .SetClock(_clock)
            .Start()
            .Conversation;

        // Act - Empty title should fail for updates (per business rules)
        var result = conversation.UpdateTitle(string.Empty, _clock);

        // Assert - Should fail with catalog code
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT_CONVERSATION_TITLE_EMPTY");
    }    [Fact]
    public void TIT_UPD_201_FAIL_UpdateTitleToLength201_ShouldFailWithCatalogCode()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .SetOwner(_ownerId)
            .SetClock(_clock)
            .Start()
            .Conversation;

        // Act - Title with 201 chars should fail
        var result = conversation.UpdateTitle(StringGenerators.Title.Length201, _clock);

        // Assert - Should fail with catalog code
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT_CONVERSATION_TITLE_TOO_LONG");
    }

    [Fact]
    public void TIT_PRESERVE_INTERNAL_WHITESPACE_TitleWithInternalWhitespace_ShouldPreserveExactly()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .SetOwner(_ownerId)
            .SetClock(_clock)
            .Start()
            .Conversation;

        var titleWithSpaces = "  Hello   World  ";
        var expectedTitle = "Hello   World"; // Trimmed but internal preserved

        // Act
        var result = conversation.UpdateTitle(titleWithSpaces, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.Title.ShouldBe(expectedTitle);
    }

    [Fact]
    public void TIT_PRESERVE_CASE_TitleWithMixedCase_ShouldPreserveCaseExactly()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .SetOwner(_ownerId)
            .SetClock(_clock)
            .Start()
            .Conversation;

        var titleWithCase = "Hello WoRLD";

        // Act
        var result = conversation.UpdateTitle(titleWithCase, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.Title.ShouldBe(titleWithCase);
    }
}