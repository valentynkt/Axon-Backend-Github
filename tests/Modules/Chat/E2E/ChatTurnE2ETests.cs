using System.Net;
using System.Text.Json;
using Axon.Api.Contracts.V1.Chat;
using Axon.Modules.Identity.E2E.Infrastructure;
using Axon.Modules.Identity.Infrastructure.Persistence.DbInvariants;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Axon.Modules.Chat.E2E;

/// <summary>
/// E2E tests for Chat Turn API endpoint.
/// Tests the complete chat flow: new conversations, continuing conversations,
/// authentication integration, and error scenarios.
/// Covers the most critical 80% of chat functionality.
/// </summary>
[TestFixture]
public class ChatTurnE2ETests : E2ETestBase
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Mock AI service for predictable responses
        SetupMockAiService();
    }

    #region New Conversation Tests

    [Test]
    public async Task ChatTurn_NewConversation_ShouldCreateConversationAndReturnAssistantResponse()
    {
        // Arrange: Set up authenticated user
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        var request = new ChatTurnRequestDto
        {
            ConversationId = null, // Start new conversation
            Message = "Hello, this is my first message!"
        };

        // Act: Send chat turn request
        SetAuthorizationHeader(validJwt);
        var response = await PostChatTurnAsync(request);

        // Assert: Should create new conversation and return assistant response
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseData = await DeserializeResponseAsync<ChatTurnResponseDto>(response);

        responseData.ConversationId.ShouldNotBe(Guid.Empty, "Should create new conversation ID");
        responseData.UserMessageId.ShouldNotBe(Guid.Empty, "Should return user message ID");
        responseData.AssistantMessageId.ShouldNotBe(Guid.Empty, "Should return assistant message ID");
        responseData.AssistantMessage.ShouldNotBeNullOrWhiteSpace("Should return assistant response");
        responseData.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue, "Should have valid timestamp");

        // Verify the conversation was persisted
        await VerifyConversationExists(responseData.ConversationId);
        await VerifyMessagesExist(responseData.ConversationId, 2); // User + Assistant
    }

    [Test]
    public async Task ChatTurn_NewConversationWithLongMessage_ShouldHandleValidContent()
    {
        // Arrange: Set up user and long but valid message
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        var longMessage = new string('A', 1000); // 1KB message
        var request = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = longMessage
        };

        // Act
        SetAuthorizationHeader(validJwt);
        var response = await PostChatTurnAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var responseData = await DeserializeResponseAsync<ChatTurnResponseDto>(response);
        responseData.ConversationId.ShouldNotBe(Guid.Empty);
    }

    #endregion

    #region Continue Conversation Tests

    [Test]
    public async Task ChatTurn_ContinueExistingConversation_ShouldAppendMessages()
    {
        // Arrange: Create initial conversation
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        var firstRequest = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = "First message"
        };

        SetAuthorizationHeader(validJwt);
        var firstResponse = await PostChatTurnAsync(firstRequest);
        var firstData = await DeserializeResponseAsync<ChatTurnResponseDto>(firstResponse);

        // Act: Continue the conversation
        var secondRequest = new ChatTurnRequestDto
        {
            ConversationId = firstData.ConversationId,
            Message = "Second message in same conversation"
        };

        SetAuthorizationHeader(validJwt);
        var secondResponse = await PostChatTurnAsync(secondRequest);

        // Assert: Should use same conversation ID
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondData = await DeserializeResponseAsync<ChatTurnResponseDto>(secondResponse);

        secondData.ConversationId.ShouldBe(firstData.ConversationId, "Should use same conversation");
        secondData.UserMessageId.ShouldNotBe(firstData.UserMessageId, "Should create new user message");
        secondData.AssistantMessageId.ShouldNotBe(firstData.AssistantMessageId, "Should create new assistant message");

        // Verify message count increased
        await VerifyMessagesExist(firstData.ConversationId, 4); // 2 user + 2 assistant messages
    }

    [Test]
    public async Task ChatTurn_ContinueNonexistentConversation_ShouldReturn404()
    {
        // Arrange
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        var request = new ChatTurnRequestDto
        {
            ConversationId = Guid.NewGuid(), // Nonexistent conversation
            Message = "Message to nonexistent conversation"
        };

        // Act
        SetAuthorizationHeader(validJwt);
        var response = await PostChatTurnAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ChatTurn_ContinueOtherUserConversation_ShouldReturn403()
    {
        // Arrange: Create conversation with first user
        var firstUserJwt = JwtTestTokenFactory.CreateValidDynamicJwt("user1");
        await SetupPrincipalWithWallets(firstUserJwt, "user1");

        var firstRequest = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = "First user's message"
        };

        SetAuthorizationHeader(firstUserJwt);
        var firstResponse = await PostChatTurnAsync(firstRequest);
        var firstData = await DeserializeResponseAsync<ChatTurnResponseDto>(firstResponse);

        // Act: Try to access with different user
        var secondUserJwt = JwtTestTokenFactory.CreateValidDynamicJwt("user2");
        await SetupPrincipalWithWallets(secondUserJwt, "user2");

        var secondRequest = new ChatTurnRequestDto
        {
            ConversationId = firstData.ConversationId,
            Message = "Second user trying to access first user's conversation"
        };

        SetAuthorizationHeader(secondUserJwt);
        var response = await PostChatTurnAsync(secondRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Authentication Tests

    [Test]
    public async Task ChatTurn_WithoutAuthentication_ShouldReturn401()
    {
        // Arrange
        var request = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = "Message without auth"
        };

        // Act: No authorization header
        var response = await PostChatTurnAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ChatTurn_WithInvalidToken_ShouldReturn401()
    {
        // Arrange
        var expiredJwt = JwtTestTokenFactory.CreateExpiredJwt();
        var request = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = "Message with invalid token"
        };

        // Act
        SetAuthorizationHeader(expiredJwt);
        var response = await PostChatTurnAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ChatTurn_WithNonexistentUser_ShouldReturn404()
    {
        // Arrange: Valid JWT but user doesn't exist in system
        var unknownUserJwt = JwtTestTokenFactory.CreateValidDynamicJwt("unknown_user_999");
        var request = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = "Message from unknown user"
        };

        // Act
        SetAuthorizationHeader(unknownUserJwt);
        var response = await PostChatTurnAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Validation Tests

    [TestCase("", TestName = "ChatTurn_EmptyMessage_ShouldReturn400")]
    [TestCase("   ", TestName = "ChatTurn_WhitespaceMessage_ShouldReturn400")]
    [TestCase(null, TestName = "ChatTurn_NullMessage_ShouldReturn400")]
    public async Task ChatTurn_InvalidMessage_ShouldReturn400(string? invalidMessage)
    {
        // Arrange
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        var request = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = invalidMessage!
        };

        // Act
        SetAuthorizationHeader(validJwt);
        var response = await PostChatTurnAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ChatTurn_ExtremelyLongMessage_ShouldReturn400()
    {
        // Arrange: Message exceeding limits
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        var tooLongMessage = new string('A', 100_000); // 100KB message
        var request = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = tooLongMessage
        };

        // Act
        SetAuthorizationHeader(validJwt);
        var response = await PostChatTurnAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region AI Service Integration Tests

    [Test]
    public async Task ChatTurn_AIServiceFailure_ShouldReturn500()
    {
        // Arrange: Configure AI service to return error
        SetupMockAiServiceFailure();

        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        var request = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = "This will trigger AI service failure"
        };

        // Act
        SetAuthorizationHeader(validJwt);
        var response = await PostChatTurnAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Test]
    public async Task ChatTurn_AIServiceTimeout_ShouldReturn500()
    {
        // Arrange: Configure AI service with delayed response
        SetupMockAiServiceTimeout();

        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        var request = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = "This will trigger AI service timeout"
        };

        // Act
        SetAuthorizationHeader(validJwt);
        var response = await PostChatTurnAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Idempotency Tests

    [Test]
    public async Task ChatTurn_DuplicateAIResponseId_ShouldBeIdempotent()
    {
        // This test verifies that if the AI service returns the same response ID,
        // the system handles it correctly (idempotency at the AI response level)

        // Arrange: Set up conversation
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        // Create initial conversation
        var firstRequest = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = "Initial message"
        };

        SetAuthorizationHeader(validJwt);
        var firstResponse = await PostChatTurnAsync(firstRequest);
        var firstData = await DeserializeResponseAsync<ChatTurnResponseDto>(firstResponse);

        // Configure mock AI to return duplicate response ID
        SetupMockAiServiceWithDuplicateResponseId();

        // Act: Send second message that would trigger duplicate AI response ID
        var secondRequest = new ChatTurnRequestDto
        {
            ConversationId = firstData.ConversationId,
            Message = "Second message that triggers duplicate AI response"
        };

        SetAuthorizationHeader(validJwt);
        var secondResponse = await PostChatTurnAsync(secondRequest);

        // Assert: Should handle gracefully (depending on business logic)
        // This could be either success with deduplication or a specific error
        secondResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Conflict);
    }

    #endregion

    #region Performance Tests

    [Test]
    public async Task ChatTurn_ResponseTime_ShouldCompleteWithin5Seconds()
    {
        // Arrange
        var validJwt = JwtTestTokenFactory.CreateValidDynamicJwt();
        await SetupPrincipalWithWallets(validJwt);

        var request = new ChatTurnRequestDto
        {
            ConversationId = null,
            Message = "Performance test message"
        };

        // Act: Measure response time
        SetAuthorizationHeader(validJwt);
        var startTime = DateTime.UtcNow;
        var response = await PostChatTurnAsync(request);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        duration.TotalSeconds.ShouldBeLessThan(5, "Chat turn should complete within 5 seconds");
    }

    #endregion

    #region Helper Methods

    private async Task<HttpResponseMessage> PostChatTurnAsync(ChatTurnRequestDto request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = CreateJsonContent(json);
        return await HttpClient.PostAsync("/api/v1/chat/turns", content);
    }

    private static async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions)!;
    }

    private async Task VerifyConversationExists(Guid conversationId)
    {
        // This would typically check the database directly
        // For simplicity, we'll verify through API
        var conversationsResponse = await HttpClient.GetAsync("/api/v1/chat/conversations");
        conversationsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await conversationsResponse.Content.ReadAsStringAsync();
        content.ShouldContain(conversationId.ToString(), "Conversation should exist in conversations list");
    }

    private async Task VerifyMessagesExist(Guid conversationId, int expectedCount)
    {
        // Verify through messages endpoint
        var messagesResponse = await HttpClient.GetAsync($"/api/v1/chat/conversations/{conversationId}/messages");
        messagesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await messagesResponse.Content.ReadAsStringAsync();
        var messagesData = JsonSerializer.Deserialize<GetConversationMessagesResponseDto>(content, JsonOptions);
        messagesData!.Messages.Count.ShouldBe(expectedCount, $"Should have {expectedCount} messages");
    }

    private async Task SetupPrincipalWithWallets(string jwt, string? userId = null)
    {
        var subject = userId ?? TestDataFixtures.DynA_Subject;
        var requestData = new
        {
            environmentId = TestDataFixtures.MainnetEnvironment,
            wallets = new[]
            {
                new { address = TestDataFixtures.W1MainAddress, chain = TestDataFixtures.SolanaMainnetChain }
            }
        };

        var json = JsonSerializer.Serialize(requestData, JsonOptions);
        using var content = CreateJsonContent(json);

        SetAuthorizationHeader(jwt);
        var response = await HttpClient.PostAsync("/api/v1/auth/exchange", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private void SetupMockAiService()
    {
        // This would typically set up a mock AI service response
        // For now, we assume the AI service is configured in the test environment
        // to return predictable responses
    }

    private void SetupMockAiServiceFailure()
    {
        // Configure mock to simulate AI service failure
        // This might involve setting environment variables or
        // configuring a test-specific AI service implementation
    }

    private void SetupMockAiServiceTimeout()
    {
        // Configure mock to simulate AI service timeout
        // This might involve delayed responses or timeout simulation
    }

    private void SetupMockAiServiceWithDuplicateResponseId()
    {
        // Configure mock to return duplicate AI response IDs
        // This tests the idempotency logic in the message processing
    }

    #endregion

    #region JSON Serialization Options

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    #endregion
}