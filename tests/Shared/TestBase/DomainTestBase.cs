using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Tests.Shared.TestBase;

/// <summary>
/// Base class for Domain layer tests providing common domain test utilities
/// </summary>
[TestFixture]
public abstract class DomainTestBase
{
    /// <summary>
    /// Creates a valid ConversationId for testing
    /// </summary>
    protected static ConversationId ValidConversationId() => ConversationId.New();

    /// <summary>
    /// Creates a valid MessageId for testing
    /// </summary>
    protected static MessageId ValidMessageId() => MessageId.New();

    /// <summary>
    /// Creates a valid GUID for testing
    /// </summary>
    protected static Guid ValidGuid() => Guid.NewGuid();

    /// <summary>
    /// Creates a collection of valid GUIDs for testing
    /// </summary>
    protected static IEnumerable<Guid> ValidGuids(int count) => 
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid());

    /// <summary>
    /// Asserts that a Result is successful
    /// </summary>
    protected static void AssertSuccess<T>(Result<T> result) where T : class
    {
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

    /// <summary>
    /// Asserts that a Result is a failure with specific error type
    /// </summary>
    protected static void AssertFailure<T>(Result<T> result, ErrorType expectedType)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(expectedType);
    }

    /// <summary>
    /// Asserts that a Result is a validation failure with specific message
    /// </summary>
    protected static void AssertValidationFailure<T>(Result<T> result, string expectedMessage)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldBe(expectedMessage);
    }
}