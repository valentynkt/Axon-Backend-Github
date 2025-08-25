using Axon.Modules.Chat.Application.Tests.Common;

namespace Axon.Modules.Chat.Application.Tests.Services.Idempotency;

[TestFixture]
public class ChatIdempotencyKeyProviderTests : ApplicationTestBase
{
    [Test]
    public void GetIdempotencyKey_ShouldReturnValidKey_WhenGivenValidCommand()
    {
        // Arrange
        // TODO: Setup test command
        
        // Act
        // TODO: Call GetIdempotencyKey method
        
        // Assert
        // TODO: Verify idempotency key is generated correctly
        Assert.Pass("Test placeholder - implement actual test logic");
    }
}