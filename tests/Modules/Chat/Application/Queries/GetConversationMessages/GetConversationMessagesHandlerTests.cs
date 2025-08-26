using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;

using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Application.Queries.GetConversationMessages;
using Axon.Modules.Chat.Application.Tests.Builders;
using Axon.Modules.Chat.Application.Tests.Common;
using Axon.Modules.Chat.Application.Tests.Extensions;
using Axon.Modules.Chat.Domain.Tests.Extensions;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Application.Tests.Queries.GetConversationMessages;

[TestFixture]
public class GetConversationMessagesHandlerTests : QueryHandlerTestBase<GetConversationMessagesQuery, Paged<ConversationMessageItem>, GetConversationMessagesHandler>
{
    // Dependencies
    private IConversationReadRepository _mockConversationRepository = null!;
    private IMessageReadRepository _mockMessageRepository = null!;

    // Test data
    private UserId _testUserId;
    private ConversationId _testConversationId;
    private List<ConversationMessageItem> _testMessages = null!;
    
    // Query tracking for pagination simulation
    private GetConversationMessagesQuery? _currentQuery;

    protected override GetConversationMessagesHandler CreateHandler()
    {
        return new GetConversationMessagesHandler(
            _mockConversationRepository,
            _mockMessageRepository,
            MockCurrentUserService);
    }

    protected override void ConfigureHandlerDependencies()
    {
        _mockConversationRepository = Substitute.For<IConversationReadRepository>();
        _mockMessageRepository = Substitute.For<IMessageReadRepository>();

        // Setup test data
        _testUserId = UserId.New();
        _testConversationId = ConversationId.New();
        _testMessages = CreateTestMessages();

        // Configure basic default behavior without cancellation interference
        ConfigureDefaultMocks();
        
        // Override authentication to use our test user
        MockCurrentUserService.UserId
            .Returns(_testUserId.Value.ToString());
    }
    
    /// <summary>
    /// Configures default mock behavior that works for most tests
    /// </summary>
    private void ConfigureDefaultMocks()
    {
        // Default conversation ownership - user owns conversation
        _mockConversationRepository.AnyAsync(Arg.Any<ISpecification<Conversation>>(), Arg.Any<CancellationToken>())
            .Returns(true);
            
        // Default message repository - returns paginated messages
        _mockMessageRepository.ListAsync(Arg.Any<ISpecification<Message, ConversationMessageItem>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => GetPaginatedMessages());
            
        _mockMessageRepository.CountAsync(Arg.Any<ISpecification<Message>>(), Arg.Any<CancellationToken>())
            .Returns(_testMessages.Count);
    }
    
    /// <summary>
    /// Resets and reconfigures mocks for test-specific scenarios
    /// </summary>
    private void ResetMocks()
    {
        // Clear received calls but keep substitutes
        _mockConversationRepository.ClearReceivedCalls();
        _mockMessageRepository.ClearReceivedCalls();
        
        // Recreate mocks to ensure clean state
        _mockConversationRepository = Substitute.For<IConversationReadRepository>();
        _mockMessageRepository = Substitute.For<IMessageReadRepository>();
        
        ConfigureDefaultMocks();
    }

    protected override GetConversationMessagesQuery CreateValidQuery()
    {
        return QueryTestDataBuilder.GetConversationMessages()
            .WithConversationId(_testConversationId.Value)
            .WithFirstPage()
            .Build();
    }

    protected override GetConversationMessagesQuery CreateInvalidQuery()
    {
        return QueryTestDataBuilder.GetConversationMessages()
            .WithEmptyConversationId()
            .WithInvalidPageNumber()
            .Build();
    }

    protected override void SetupQueryTestData()
    {
        // Ensure clean mock state for each query test
        // This is called by base class tests to prepare test data
        _currentQuery = null; // Reset query context
    }
    
    /// <summary>
    /// Simulates pagination by returning the appropriate slice of test messages
    /// based on the current query's pagination parameters
    /// </summary>
    private List<ConversationMessageItem> GetPaginatedMessages()
    {
        if (_currentQuery == null)
            return _testMessages; // Return all for base class tests
            
        var pageNumber = _currentQuery.PageNumber;
        var pageSize = _currentQuery.PageSize;
        
        var startIndex = (pageNumber - 1) * pageSize;
        if (startIndex >= _testMessages.Count)
            return new List<ConversationMessageItem>(); // Empty page
            
        var itemsToTake = Math.Min(pageSize, _testMessages.Count - startIndex);
        return _testMessages.Skip(startIndex).Take(itemsToTake).ToList();
    }

    protected override async Task AssertQueryResult(GetConversationMessagesQuery query, Paged<ConversationMessageItem> result)
    {
        result.ShouldNotBeNull();
        result.ShouldHavePagination(query.PageNumber, query.PageSize);
        result.ShouldHaveItemsWithinPageSize();
        
        // Verify all returned messages belong to the correct conversation
        foreach (var message in result.Items)
        {
            // The message should be properly structured
            message.MessageId.ShouldNotBe(Guid.Empty);
            message.Role.ShouldNotBeNullOrWhiteSpace();
            message.Content.ShouldNotBeNullOrWhiteSpace();
            message.CreatedAtUtc.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
            message.Sequence.ShouldBeGreaterThan(0);
        }

        await Task.CompletedTask;
    }

    protected override TimeSpan GetExpectedMaxExecutionTime()
    {
        return TimeSpan.FromMilliseconds(500); // GetConversationMessages should be fast
    }

    #region Specific Handler Tests

    [TestCaseSource(nameof(GetValidQueryScenarios))]
    public async Task Handle_WithValidQuery_ShouldReturnSuccessWithCorrectPagination(
        GetConversationMessagesQuery query,
        string _)
    {
        // Arrange
        SetupQueryTestData();
        _currentQuery = query; // Set current query for pagination simulation AFTER SetupQueryTestData
        
        var startIndex = (query.PageNumber - 1) * query.PageSize;
        var expectedItemsOnPage = Math.Max(0, Math.Min(query.PageSize, _testMessages.Count - startIndex));

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldBeSuccess();
        var pagedResult = result.Value;

        pagedResult.ShouldHavePagination(query.PageNumber, query.PageSize);
        pagedResult.Items.Count.ShouldBe(expectedItemsOnPage); // Should match exactly now
        pagedResult.ShouldBeSortedBy(m => m.Sequence, ascending: true);
        
        // Reset for next test
        _currentQuery = null;
    }

    [Test]
    public async Task Handle_WithUnauthenticatedUser_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var query = CreateValidQuery();

        MockCurrentUserService.UserId
            .Returns((string?)null);

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("Chat.Auth.Unauthenticated");
    }

    [Test]
    public async Task Handle_WithConversationNotOwnedByUser_ShouldReturnAccessDeniedError()
    {
        // Arrange - Create a separate handler with fresh mocks for this test
        var query = CreateValidQuery();
        
        // Create fresh mocks specifically for this test
        var mockConversationRepo = Substitute.For<IConversationReadRepository>();
        var mockMessageRepo = Substitute.For<IMessageReadRepository>();
        
        // Set up the conversation repository to deny access - using ReturnsForAnyArgs to prevent spec execution
        mockConversationRepo.AnyAsync(default!, default)
            .ReturnsForAnyArgs(false);

        // Create handler with fresh mocks
        var testHandler = new GetConversationMessagesHandler(
            mockConversationRepo,
            mockMessageRepo,
            MockCurrentUserService);

        // Act
        var result = await testHandler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Chat.Conversation.AccessDenied");
    }

    [TestCaseSource(nameof(GetInvalidPaginationScenarios))]
    public async Task Handle_WithInvalidPagination_ShouldReturnValidationError(
        GetConversationMessagesQuery query,
        string expectedErrorField,
        string _)
    {
        // Arrange
        MockCurrentUserService.UserId
            .Returns(_testUserId.Value.ToString());

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Validation);
        // Check if the error message mentions the problematic field or value
        if (expectedErrorField == "PageNumber")
        {
            result.Error.Message.ShouldContain("page number", Case.Insensitive);
        }
        else if (expectedErrorField == "PageSize")
        {
            result.Error.Message.ShouldContain("page size", Case.Insensitive);
        }
    }

    [Test]
    public async Task Handle_WithRepositoryException_ShouldReturnInternalError()
    {
        // Arrange - Create a separate handler with fresh mocks for this test
        var query = CreateValidQuery();
        _currentQuery = query; // Set for context
        
        // Create fresh mocks specifically for this test
        var mockConversationRepo = Substitute.For<IConversationReadRepository>();
        var mockMessageRepo = Substitute.For<IMessageReadRepository>();
        
        // Set up conversation repository to succeed (ownership check passes)
        mockConversationRepo.AnyAsync(default!, default)
            .ReturnsForAnyArgs(true);
        
        // Set up message repository to throw exception
        mockMessageRepo.ListAsync(default!, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("Database connection failed"));

        // Create handler with fresh mocks
        var testHandler = new GetConversationMessagesHandler(
            mockConversationRepo,
            mockMessageRepo,
            MockCurrentUserService);

        // Act
        var result = await testHandler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Internal);
        result.Error.Code.ShouldBe("Chat.Messages.ListFailed");
        result.Error.Message.ShouldContain("Failed to retrieve conversation messages");
        
        // Reset for next test
        _currentQuery = null;
    }

    #endregion
    

    [Test]
    public async Task Handle_WithLargeMessageSet_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        var query = CreateValidQuery();
        _testMessages = CreateLargeMessageSet(1000);
        SetupQueryTestData();
        _currentQuery = query; // Set current query for pagination simulation AFTER SetupQueryTestData

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Items.Count.ShouldBeLessThanOrEqualTo(query.PageSize);
        
        // Reset for next test
        _currentQuery = null;
    }

    #region Test Data Creation

    private static List<ConversationMessageItem> CreateTestMessages()
    {
        return
        [
            new ConversationMessageItem(
                MessageId.New().Value,
                "User",
                "Test user message",
                DateTime.UtcNow.AddMinutes(-10),
                1
            ),
            new ConversationMessageItem(
                MessageId.New().Value,
                "Assistant", 
                "Test assistant response",
                DateTime.UtcNow.AddMinutes(-5),
                2
            )
        ];
    }

    private static List<ConversationMessageItem> CreateLargeMessageSet(int messageCount)
    {
        var messages = new List<ConversationMessageItem>();
        for (int i = 0; i < messageCount; i++)
        {
            messages.Add(new ConversationMessageItem(
                MessageId.New().Value,
                i % 2 == 0 ? "User" : "Assistant",
                $"Test message {i + 1}",
                DateTime.UtcNow.AddMinutes(-i),
                i + 1
            ));
        }
        return messages;
    }

    #endregion

    #region Test Case Sources

    private static IEnumerable<TestCaseData> GetValidQueryScenarios()
    {
        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(ConversationId.New().Value)
                .WithFirstPage()
                .Build(),
            "First page with default page size");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(ConversationId.New().Value)
                .WithPagination(2, 10)
                .Build(),
            "Second page with custom page size");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(ConversationId.New().Value)
                .WithPagination(1, 50)
                .Build(),
            "Large page size");
    }

    private static IEnumerable<TestCaseData> GetInvalidPaginationScenarios()
    {
        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(ConversationId.New().Value)
                .WithInvalidPageNumber()
                .Build(),
            "PageNumber",
            "Invalid page number");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(ConversationId.New().Value)
                .WithInvalidPageSize()
                .Build(),
            "PageSize",
            "Invalid page size");
    }

    #endregion
}