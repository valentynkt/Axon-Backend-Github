using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
public sealed class ConversationIdTests : DomainTestBase
{
    [Test]
    public void New_GivenNoParameters_ShouldCreateUniqueConversationIds()

    {
        // Arrange & Act
        var conversationId1 = ConversationId.New();
        var conversationId2 = ConversationId.New();

        // Assert
        conversationId1.Value.ShouldNotBe(Guid.Empty);
        conversationId2.Value.ShouldNotBe(Guid.Empty);
        conversationId1.Value.ShouldNotBe(conversationId2.Value);
    }

    [Test]
    public void Create_GivenValidGuid_ShouldReturnSuccessWithCorrectValue()
    {
        // Arrange
        var validGuid = ValidGuid();


        // Act
        var result = ConversationId.Create(validGuid);

        // Assert
        result.ShouldBeSuccessAnd(conversationId => 
            conversationId.Value.ShouldBe(validGuid));
    }

    [Test]
    public void Create_GivenEmptyGuid_ShouldReturnValidationError()

    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act
        var result = ConversationId.Create(emptyGuid);

        // Assert
        result.ShouldBeValidationFailure("ConversationId cannot be empty");
    }

    [Test]
    public void Create_GivenValidGuidString_ShouldReturnSuccessWithCorrectValue()
    {
        // Arrange
        var validGuid = ValidGuid();

        var validGuidString = validGuid.ToString();

        // Act
        var result = ConversationId.Create(validGuidString);

        // Assert
        result.ShouldBeSuccessAnd(conversationId => 
            conversationId.Value.ShouldBe(validGuid));
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Create_GivenNullOrWhitespaceString_ShouldReturnValidationError(string? input)

    {
        // Act
        var result = ConversationId.Create(input);

        // Assert
        result.ShouldBeValidationFailure("ConversationId string cannot be null or empty");
    }

    [Test]
    [TestCase("not-a-guid")]
    [TestCase("12345")]
    [TestCase("invalid-guid-format")]
    [TestCase("123e4567-e89b-12d3-a456-42661417400")]  // Missing last character
    public void Create_GivenInvalidGuidString_ShouldReturnValidationError(string invalidGuidString)

    {
        // Act
        var result = ConversationId.Create(invalidGuidString);

        // Assert
        result.ShouldBeValidationFailure("ConversationId must be a valid GUID format");
    }

    [Test]
    public void ToString_GivenValidConversationId_ShouldReturnGuidString()
    {
        // Arrange
        var guid = ValidGuid();
        var conversationId = ConversationId.Create(guid).ShouldBeSuccessWithValue();


        // Act
        var result = conversationId.ToString();

        // Assert
        result.ShouldBe(guid.ToString());
    }

    [Test]
    public void ImplicitOperator_GivenValidConversationId_ShouldConvertToGuid()
    {
        // Arrange
        var originalGuid = ValidGuid();
        var conversationId = ConversationId.Create(originalGuid).ShouldBeSuccessWithValue();


        // Act
        Guid convertedGuid = conversationId;

        // Assert
        convertedGuid.ShouldBe(originalGuid);
    }

    [Test]
    public void Equality_GivenSameGuidValues_ShouldReturnTrue()
    {
        // Arrange
        var guid = ValidGuid();
        var conversationId1 = ConversationId.Create(guid).ShouldBeSuccessWithValue();
        var conversationId2 = ConversationId.Create(guid).ShouldBeSuccessWithValue();

        // Act & Assert
        conversationId1.ShouldBe(conversationId2);
        conversationId1.Equals(conversationId2).ShouldBeTrue();
        (conversationId1 == conversationId2).ShouldBeTrue();
        (conversationId1 != conversationId2).ShouldBeFalse();
    }

    [Test]
    public void Equality_GivenDifferentGuidValues_ShouldReturnFalse()
    {
        // Arrange
        var conversationId1 = ValidConversationId();
        var conversationId2 = ValidConversationId();

        // Act & Assert
        conversationId1.ShouldNotBe(conversationId2);
        conversationId1.Equals(conversationId2).ShouldBeFalse();
        (conversationId1 == conversationId2).ShouldBeFalse();
        (conversationId1 != conversationId2).ShouldBeTrue();
    }

    [Test]
    public void GetHashCode_GivenSameGuidValues_ShouldReturnSameValue()
    {
        // Arrange
        var guid = ValidGuid();
        var conversationId1 = ConversationId.Create(guid).ShouldBeSuccessWithValue();
        var conversationId2 = ConversationId.Create(guid).ShouldBeSuccessWithValue();


        // Act
        var hashCode1 = conversationId1.GetHashCode();
        var hashCode2 = conversationId2.GetHashCode();

        // Assert
        hashCode1.ShouldBe(hashCode2);
    }

    [Test]
    public void Factory_ShouldCreateEqualConversationIdsFromSameGuid()
    {
        // Arrange & Act
        var (conversationId1, conversationId2) = ChatDomainFactory.EqualConversationIds();

        // Assert
        conversationId1.ShouldBe(conversationId2);
        conversationId1.GetHashCode().ShouldBe(conversationId2.GetHashCode());
    }

    [Test]
    public void Factory_ShouldCreateUniqueConversationIds()
    {
        // Arrange & Act
        var conversationIds = ChatDomainFactory.ValidConversationIds(5).ToList();

        // Assert
        conversationIds.Count.ShouldBe(5);
        conversationIds.ShouldBeUnique();
        conversationIds.ShouldAllBe(id => id.Value != Guid.Empty);

    }
}