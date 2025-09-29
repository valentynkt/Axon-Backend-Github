using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.Services.ConversationAccessService;
using Axon.Modules.Chat.Application.Tests.Common;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Axon.Modules.Chat.Integration;

/// <summary>
/// Integration tests for Chat and Identity module interactions.
/// Tests authentication, authorization, and cross-module data consistency.
/// Ensures chat functionality properly integrates with user identity management.
/// </summary>
[TestFixture]
public class ChatIdentityIntegrationTests : ApplicationTestBase
{
    private IConversationRepository _mockConversationRepository = null!;
    private IAuthenticationService _mockAuthenticationService = null!;
    private ILogger<ConversationAccessService> _mockLogger = null!;
    private ConversationAccessService _conversationAccessService = null!;

    // Test data
    private AxonUserId _testUserId = null!;
    private AxonUserId _otherUserId = null!;
    private ConversationId _testConversationId = null!;
    private Conversation _testConversation = null!;

    protected override void OnSetUp()
    {
        // Create mocks
        _mockConversationRepository = Substitute.For<IConversationRepository>();
        _mockAuthenticationService = Substitute.For<IAuthenticationService>();
        _mockLogger = Substitute.For<ILogger<ConversationAccessService>>();

        // Create service under test
        _conversationAccessService = new ConversationAccessService(
            _mockConversationRepository,
            _mockAuthenticationService,
            _mockLogger);

        // Setup test data
        SetupTestData();
        SetupDefaultMockBehaviors();
    }

    #region User Authentication Integration Tests

    [Test]
    public async Task ValidateUserAccessToConversation_ValidUser_ShouldReturnSuccess()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            _testUserId,
            _testConversationId,
            cancellationToken);

        // Assert
        result.ShouldBeSuccess();

        // Verify interactions
        await _mockAuthenticationService.Received(1).ValidateUserExistsAsync(_testUserId, cancellationToken);
        await _mockConversationRepository.Received(1).GetByIdAsync(_testConversationId, cancellationToken);
    }

    [Test]
    public async Task ValidateUserAccessToConversation_NonexistentUser_ShouldReturnFailure()
    {
        // Arrange
        var nonexistentUserId = CreateAxonUserId();\n        var cancellationToken = CancellationToken.None;

        _mockAuthenticationService\n            .ValidateUserExistsAsync(nonexistentUserId, cancellationToken)\n            .Returns(Result.Failure<Unit, Error>(Error.NotFound(\"User not found\", \"USER_NOT_FOUND\")));

        // Act
        var result = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            nonexistentUserId,
            _testConversationId,
            cancellationToken);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe(\"USER_NOT_FOUND\");

        // Should not check conversation if user doesn't exist
        await _mockConversationRepository.DidNotReceive().GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ValidateUserAccessToConversation_NonexistentConversation_ShouldReturnFailure()
    {
        // Arrange
        var nonexistentConversationId = ConversationId.New();
        var cancellationToken = CancellationToken.None;

        _mockConversationRepository\n            .GetByIdAsync(nonexistentConversationId, cancellationToken)\n            .Returns(Result.Failure<Conversation, Error>(Error.NotFound(\"Conversation not found\", \"CONVERSATION_NOT_FOUND\")));

        // Act
        var result = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            _testUserId,
            nonexistentConversationId,
            cancellationToken);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe(\"CONVERSATION_NOT_FOUND\");
    }

    [Test]
    public async Task ValidateUserAccessToConversation_DifferentOwner_ShouldReturnForbidden()
    {
        // Arrange: Create conversation owned by different user
        var otherUserConversation = CreateConversationBuilder()
            .WithOwner(_otherUserId)
            .WithUserMessage(\"Other user's message\")
            .Build();

        var otherConversationId = otherUserConversation.Id;
        var cancellationToken = CancellationToken.None;

        _mockConversationRepository\n            .GetByIdAsync(otherConversationId, cancellationToken)\n            .Returns(Result.Success<Conversation, Error>(otherUserConversation));

        // Act: Try to access with different user
        var result = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            _testUserId, // Different user than owner
            otherConversationId,
            cancellationToken);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    #endregion

    #region Cross-Module Data Consistency Tests

    [Test]
    public async Task ValidateUserAccessToConversation_UserIdFormatConsistency_ShouldWork()
    {
        // This test ensures that AxonUserId is handled consistently across modules

        // Arrange: Create user ID in different formats that should be equivalent
        var userIdString = _testUserId.Value.ToString();
        var reconstructedUserId = new AxonUserId(Guid.Parse(userIdString));
        var cancellationToken = CancellationToken.None;

        // Act: Both should work the same way
        var result1 = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            _testUserId,
            _testConversationId,
            cancellationToken);

        var result2 = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            reconstructedUserId,
            _testConversationId,
            cancellationToken);

        // Assert: Both should succeed and behave identically
        result1.ShouldBeSuccess();
        result2.ShouldBeSuccess();

        // Verify same number of authentication calls
        await _mockAuthenticationService.Received(2).ValidateUserExistsAsync(Arg.Any<AxonUserId>(), cancellationToken);
    }

    [Test]
    public async Task ValidateUserAccessToConversation_ConversationIdConsistency_ShouldWork()
    {
        // This test ensures ConversationId handling is consistent

        // Arrange: Create conversation ID in different equivalent formats
        var conversationIdString = _testConversationId.Value.ToString();
        var reconstructedConversationId = new ConversationId(Guid.Parse(conversationIdString));
        var cancellationToken = CancellationToken.None;

        // Mock repository to handle both IDs
        _mockConversationRepository\n            .GetByIdAsync(reconstructedConversationId, cancellationToken)\n            .Returns(Result.Success<Conversation, Error>(_testConversation));

        // Act
        var result1 = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            _testUserId,
            _testConversationId,
            cancellationToken);

        var result2 = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            _testUserId,
            reconstructedConversationId,
            cancellationToken);

        // Assert
        result1.ShouldBeSuccess();
        result2.ShouldBeSuccess();
    }

    #endregion

    #region Authentication Service Integration Edge Cases

    [Test]
    public async Task ValidateUserAccessToConversation_AuthServiceThrows_ShouldPropagateException()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _mockAuthenticationService\n            .ValidateUserExistsAsync(_testUserId, cancellationToken)\n            .ThrowsAsync(new InvalidOperationException(\"Identity service unavailable\"));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            _conversationAccessService.ValidateUserAccessToConversationAsync(
                _testUserId,
                _testConversationId,
                cancellationToken));

        exception.Message.ShouldBe(\"Identity service unavailable\");
    }

    [Test]
    public async Task ValidateUserAccessToConversation_AuthServiceTimeout_ShouldPropagateException()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _mockAuthenticationService\n            .ValidateUserExistsAsync(_testUserId, cancellationToken)\n            .ThrowsAsync(new TimeoutException(\"Authentication timeout\"));

        // Act & Assert
        var exception = await Should.ThrowAsync<TimeoutException>(() =>
            _conversationAccessService.ValidateUserAccessToConversationAsync(
                _testUserId,
                _testConversationId,
                cancellationToken));

        exception.Message.ShouldBe(\"Authentication timeout\");
    }

    #endregion

    #region Repository Integration Edge Cases

    [Test]
    public async Task ValidateUserAccessToConversation_RepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _mockConversationRepository\n            .GetByIdAsync(_testConversationId, cancellationToken)\n            .ThrowsAsync(new InvalidOperationException(\"Database connection failed\"));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            _conversationAccessService.ValidateUserAccessToConversationAsync(
                _testUserId,
                _testConversationId,
                cancellationToken));

        exception.Message.ShouldBe(\"Database connection failed\");
    }

    #endregion

    #region Concurrent Access Tests

    [Test]
    public async Task ValidateUserAccessToConversation_ConcurrentRequests_ShouldHandleCorrectly()
    {
        // Arrange: Simulate concurrent access validation for same user/conversation
        var cancellationToken = CancellationToken.None;
        var tasks = new List<Task<Result<Unit, Error>>>();

        // Act: Make multiple concurrent calls
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_conversationAccessService.ValidateUserAccessToConversationAsync(
                _testUserId,
                _testConversationId,
                cancellationToken));
        }

        var results = await Task.WhenAll(tasks);

        // Assert: All should succeed
        foreach (var result in results)
        {
            result.ShouldBeSuccess();
        }

        // Should have made expected number of calls (might be cached in real implementation)
        await _mockAuthenticationService.Received(5).ValidateUserExistsAsync(_testUserId, cancellationToken);
        await _mockConversationRepository.Received(5).GetByIdAsync(_testConversationId, cancellationToken);
    }

    [Test]
    public async Task ValidateUserAccessToConversation_DifferentUsersConversations_ShouldIsolateCorrectly()
    {
        // Arrange: Set up multiple users and conversations
        var user1Conversation = CreateConversationBuilder()
            .WithOwner(_testUserId)
            .WithUserMessage(\"User 1 message\")
            .Build();

        var user2Conversation = CreateConversationBuilder()
            .WithOwner(_otherUserId)
            .WithUserMessage(\"User 2 message\")
            .Build();

        var cancellationToken = CancellationToken.None;

        _mockConversationRepository\n            .GetByIdAsync(user1Conversation.Id, cancellationToken)\n            .Returns(Result.Success<Conversation, Error>(user1Conversation));

        _mockConversationRepository\n            .GetByIdAsync(user2Conversation.Id, cancellationToken)\n            .Returns(Result.Success<Conversation, Error>(user2Conversation));

        // Act: Validate correct access patterns
        var validAccess1 = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            _testUserId, user1Conversation.Id, cancellationToken);

        var validAccess2 = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            _otherUserId, user2Conversation.Id, cancellationToken);

        var invalidAccess1 = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            _testUserId, user2Conversation.Id, cancellationToken);

        var invalidAccess2 = await _conversationAccessService.ValidateUserAccessToConversationAsync(
            _otherUserId, user1Conversation.Id, cancellationToken);

        // Assert: Valid access should succeed, invalid should fail
        validAccess1.ShouldBeSuccess();
        validAccess2.ShouldBeSuccess();
        invalidAccess1.ShouldBeFailure();
        invalidAccess2.ShouldBeFailure();

        invalidAccess1.Error.Type.ShouldBe(ErrorType.Forbidden);
        invalidAccess2.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    #endregion

    #region Cancellation Token Integration

    [Test]
    public async Task ValidateUserAccessToConversation_CancellationRequested_ShouldPropagateCancellation()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var cancellationToken = cancellationTokenSource.Token;

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
            _conversationAccessService.ValidateUserAccessToConversationAsync(
                _testUserId,
                _testConversationId,
                cancellationToken));
    }

    [Test]
    public async Task ValidateUserAccessToConversation_CancellationDuringAuth_ShouldPropagateCancellation()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _mockAuthenticationService\n            .ValidateUserExistsAsync(_testUserId, cancellationToken)\n            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
            _conversationAccessService.ValidateUserAccessToConversationAsync(
                _testUserId,
                _testConversationId,
                cancellationToken));
    }

    #endregion

    #region Helper Methods

    private void SetupTestData()
    {
        _testUserId = CreateAxonUserId();
        _otherUserId = CreateAxonUserId();
        _testConversationId = ConversationId.New();

        _testConversation = CreateConversationBuilder()
            .WithOwner(_testUserId)
            .WithUserMessage(\"Test message\")
            .Build();
    }

    private void SetupDefaultMockBehaviors()
    {
        // Authentication service returns success for valid users
        _mockAuthenticationService\n            .ValidateUserExistsAsync(_testUserId, Arg.Any<CancellationToken>())\n            .Returns(Result.Success<Unit, Error>(Unit.Value));

        _mockAuthenticationService\n            .ValidateUserExistsAsync(_otherUserId, Arg.Any<CancellationToken>())\n            .Returns(Result.Success<Unit, Error>(Unit.Value));

        // Repository returns test conversation
        _mockConversationRepository\n            .GetByIdAsync(_testConversationId, Arg.Any<CancellationToken>())\n            .Returns(Result.Success<Conversation, Error>(_testConversation));
    }

    #endregion
}