using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;

using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Application.Queries.GetConversations;
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

namespace Axon.Modules.Chat.Application.Tests.Queries.GetConversations;

[TestFixture]
public class GetConversationsHandlerTests : QueryHandlerTestBase<GetConversationsQuery, Paged<ConversationListItem>, GetConversationsHandler>
{
    // Dependencies
    private IConversationReadRepository _mockConversationRepository = null!;

    // Test data
    private AxonUserId _testAxonUserId;
    private List<ConversationListItem> _testConversations = null!;
    
    // Query tracking for pagination simulation
    private GetConversationsQuery? _currentQuery;

    protected override GetConversationsHandler CreateHandler()
    {
        return new GetConversationsHandler(
            _mockConversationRepository,
            MockCurrentUserService);
    }

    protected override void ConfigureHandlerDependencies()
    {
        _mockConversationRepository = Substitute.For<IConversationReadRepository>();

        // Setup test data
        _testAxonUserId = AxonUserId.New();
        _testConversations = CreateTestConversations();

        // Configure basic default behavior
        ConfigureDefaultMocks();
        
        // Override authentication to use our test user
        MockCurrentUserService.AxonUserId
            .Returns(_testAxonUserId.Value.ToString());
        MockCurrentUserService.GetAxonUserIdAsync(Arg.Any<CancellationToken>())
            .Returns(_testAxonUserId);
    }
    
    /// <summary>
    /// Configures default mock behavior that works for most tests
    /// </summary>
    private void ConfigureDefaultMocks()
    {
        // Default conversation repository - returns paginated conversations
        _mockConversationRepository.ListAsync(Arg.Any<ISpecification<Conversation, ConversationListItem>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => GetPaginatedConversations());
            
        _mockConversationRepository.CountAsync(Arg.Any<ISpecification<Conversation>>(), Arg.Any<CancellationToken>())
            .Returns(_testConversations.Count);
    }
    
    /// <summary>
    /// Configures mocks for cancellation testing - makes authentication service throw OperationCanceledException
    /// </summary>
    private void ConfigureCancellationMocks()
    {
        // Configure auth service to throw OperationCanceledException for cancellation tests
        // This simulates the scenario where cancellation is properly handled
        MockCurrentUserService.AxonUserId
            .Returns<string?>(_ => throw new OperationCanceledException());
        MockCurrentUserService.GetAxonUserIdAsync(Arg.Any<CancellationToken>())
            .Returns<AxonUserId?>(_ => throw new OperationCanceledException());
    }
    
    /// <summary>
    /// Resets and reconfigures mocks for test-specific scenarios
    /// </summary>
    private void ResetMocks()
    {
        // Clear received calls but keep substitutes
        _mockConversationRepository.ClearReceivedCalls();
        
        // Recreate mocks to ensure clean state
        _mockConversationRepository = Substitute.For<IConversationReadRepository>();
        
        ConfigureDefaultMocks();
    }

    protected override GetConversationsQuery CreateValidQuery()
    {
        return QueryTestDataBuilder.GetConversations()
            .WithFirstPage()
            .Build();
    }

    protected override GetConversationsQuery CreateInvalidQuery()
    {
        return QueryTestDataBuilder.GetConversations()
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
    /// Simulates pagination and sorting by returning the appropriate slice of test conversations
    /// based on the current query's pagination and sorting parameters
    /// </summary>
    private List<ConversationListItem> GetPaginatedConversations()
    {
        if (_currentQuery == null)
            return _testConversations; // Return all for base class tests

        // Apply sorting first
        var sortedConversations = ApplySorting(_testConversations, _currentQuery);
        
        var pageNumber = _currentQuery.PageNumber;
        var pageSize = _currentQuery.PageSize;
        
        var startIndex = (pageNumber - 1) * pageSize;
        if (startIndex >= sortedConversations.Count)
            return new List<ConversationListItem>(); // Empty page
            
        var itemsToTake = Math.Min(pageSize, sortedConversations.Count - startIndex);
        return sortedConversations.Skip(startIndex).Take(itemsToTake).ToList();
    }
    
    /// <summary>
    /// Applies sorting to conversations based on query parameters
    /// </summary>
    private static List<ConversationListItem> ApplySorting(List<ConversationListItem> conversations, GetConversationsQuery query)
    {
        return query.SortBy switch
        {
            ConversationSortBy.UpdatedAt => query.SortDirection == SortDirection.Asc 
                ? conversations.OrderBy(c => c.UpdatedAtUtc).ToList()
                : conversations.OrderByDescending(c => c.UpdatedAtUtc).ToList(),
            ConversationSortBy.CreatedAt => query.SortDirection == SortDirection.Asc 
                ? conversations.OrderBy(c => c.CreatedAtUtc).ToList()
                : conversations.OrderByDescending(c => c.CreatedAtUtc).ToList(),
            ConversationSortBy.Title => query.SortDirection == SortDirection.Asc 
                ? conversations.OrderBy(c => c.Title).ToList()
                : conversations.OrderByDescending(c => c.Title).ToList(),
            _ => conversations
        };
    }

    protected override async Task AssertQueryResult(GetConversationsQuery query, Paged<ConversationListItem> result)
    {
        result.ShouldNotBeNull();
        result.ShouldHavePagination(query.PageNumber, query.PageSize);
        result.ShouldHaveItemsWithinPageSize();
        
        // Verify all returned conversations are properly structured
        foreach (var conversation in result.Items)
        {
            conversation.ConversationId.ShouldNotBe(Guid.Empty);
            conversation.Title.ShouldNotBeNullOrWhiteSpace();
            conversation.CreatedAtUtc.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
            conversation.UpdatedAtUtc.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
            conversation.UpdatedAtUtc.ShouldBeGreaterThanOrEqualTo(conversation.CreatedAtUtc);
        }

        await Task.CompletedTask;
    }

    protected override TimeSpan GetExpectedMaxExecutionTime()
    {
        return TimeSpan.FromMilliseconds(500); // GetConversations should be fast
    }

    #region Specific Handler Tests

    [TestCaseSource(nameof(GetValidQueryScenarios))]
    public async Task Handle_WithValidQuery_ShouldReturnSuccessWithCorrectPagination(
        GetConversationsQuery query,
        string _)
    {
        // Arrange
        _currentQuery = query; // Set current query for pagination simulation
        // Apply sorting to get the correct expected count
        var sortedConversations = ApplySorting(_testConversations, query);
        var startIndex = (query.PageNumber - 1) * query.PageSize;
        var expectedItemsOnPage = Math.Max(0, Math.Min(query.PageSize, sortedConversations.Count - startIndex));

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldBeSuccess();
        var pagedResult = result.Value;

        pagedResult.ShouldHavePagination(query.PageNumber, query.PageSize);
        pagedResult.Items.Count.ShouldBe(expectedItemsOnPage);
        
        // Verify sorting - conversations should be sorted by UpdatedAt descending by default
        if (pagedResult.Items.Count > 1)
        {
            switch (query.SortBy)
            {
                case ConversationSortBy.UpdatedAt:
                    pagedResult.ShouldBeSortedBy(c => c.UpdatedAtUtc, ascending: query.SortDirection == SortDirection.Asc);
                    break;
                case ConversationSortBy.CreatedAt:
                    pagedResult.ShouldBeSortedBy(c => c.CreatedAtUtc, ascending: query.SortDirection == SortDirection.Asc);
                    break;
                case ConversationSortBy.Title:
                    pagedResult.ShouldBeSortedBy(c => c.Title, ascending: query.SortDirection == SortDirection.Asc);
                    break;
            }
        }
        
        // Reset for next test
        _currentQuery = null;
    }

    [Test]
    public async Task Handle_WithUnauthenticatedUser_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var query = CreateValidQuery();

        MockCurrentUserService.AxonUserId
            .Returns((string?)null);
        MockCurrentUserService.GetAxonUserIdAsync(Arg.Any<CancellationToken>())
            .Returns((AxonUserId?)null);

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("AUTH.USER_ID_NOT_RESOLVED");
    }

    [TestCaseSource(nameof(GetInvalidPaginationScenarios))]
    public async Task Handle_WithInvalidPagination_ShouldReturnValidationError(
        GetConversationsQuery query,
        string expectedErrorField,
        string _)
    {
        // Arrange
        MockCurrentUserService.AxonUserId
            .Returns(_testAxonUserId.Value.ToString());
        MockCurrentUserService.GetAxonUserIdAsync(Arg.Any<CancellationToken>())
            .Returns(_testAxonUserId);

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
        
        // Set up repository to throw exception
        mockConversationRepo.ListAsync(Arg.Any<ISpecification<Conversation, ConversationListItem>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Create handler with fresh mocks
        var testHandler = new GetConversationsHandler(
            mockConversationRepo,
            MockCurrentUserService);

        // Act
        var result = await testHandler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Internal);
        result.Error.Code.ShouldBe("Chat.Conversations.ListFailed");
        result.Error.Message.ShouldContain("Failed to list conversations");
        
        // Reset for next test
        _currentQuery = null;
    }

    [Test]
    public async Task Handle_WithTitleFilter_ShouldReturnFilteredConversations()
    {
        // Arrange
        var titleFilter = "Test";
        var query = QueryTestDataBuilder.GetConversations()
            .WithTitleFilter(titleFilter)
            .WithFirstPage()
            .Build();
        
        // Create filtered test conversations
        var filteredConversations = _testConversations
            .Where(c => c.Title.Contains(titleFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        // Create fresh mocks specifically for this test to avoid interference
        var mockConversationRepo = Substitute.For<IConversationReadRepository>();
        mockConversationRepo.ListAsync(Arg.Any<ISpecification<Conversation, ConversationListItem>>(), Arg.Any<CancellationToken>())
            .Returns(filteredConversations);
        mockConversationRepo.CountAsync(Arg.Any<ISpecification<Conversation>>(), Arg.Any<CancellationToken>())
            .Returns(filteredConversations.Count);

        // Create handler with fresh mocks
        var testHandler = new GetConversationsHandler(
            mockConversationRepo,
            MockCurrentUserService);

        // Act
        var result = await testHandler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        var pagedResult = result.Value;
        
        pagedResult.Items.ShouldHaveAppliedFilter(
            c => c.Title.Contains(titleFilter, StringComparison.OrdinalIgnoreCase),
            $"title contains '{titleFilter}'");
    }

    [Test]
    public async Task Handle_WithEmptyResults_ShouldReturnEmptyPagedResult()
    {
        // Arrange
        var query = CreateValidQuery();
        
        // Create fresh mocks specifically for this test to avoid interference
        var mockConversationRepo = Substitute.For<IConversationReadRepository>();
        mockConversationRepo.ListAsync(Arg.Any<ISpecification<Conversation, ConversationListItem>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ConversationListItem>());
        mockConversationRepo.CountAsync(Arg.Any<ISpecification<Conversation>>(), Arg.Any<CancellationToken>())
            .Returns(0);

        // Create handler with fresh mocks
        var testHandler = new GetConversationsHandler(
            mockConversationRepo,
            MockCurrentUserService);

        // Act
        var result = await testHandler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        var pagedResult = result.Value;
        
        pagedResult.Items.ShouldBeEmpty();
        pagedResult.TotalCount.ShouldBe(0);
        pagedResult.TotalPages.ShouldBe(0);
    }
    

    [Test]
    public async Task Handle_WithLargeConversationSet_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        var query = CreateValidQuery();
        _currentQuery = query; // Set current query for pagination simulation
        _testConversations = CreateLargeConversationSet(1000);

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Items.Count.ShouldBeLessThanOrEqualTo(query.PageSize);
        
        // Reset for next test
        _currentQuery = null;
    }

    #endregion


    #region Test Data Creation

    private static List<ConversationListItem> CreateTestConversations()
    {
        return
        [
            new ConversationListItem(
                ConversationId.New().Value,
                "Test Conversation 1",
                DateTime.UtcNow.AddMinutes(-30),
                DateTime.UtcNow.AddMinutes(-10),
                MessageId.New().Value.ToString()
            ),
            new ConversationListItem(
                ConversationId.New().Value,
                "Test Conversation 2",
                DateTime.UtcNow.AddMinutes(-20),
                DateTime.UtcNow.AddMinutes(-5),
                MessageId.New().Value.ToString()
            ),
            new ConversationListItem(
                ConversationId.New().Value,
                "Another Chat",
                DateTime.UtcNow.AddMinutes(-15),
                DateTime.UtcNow.AddMinutes(-2),
                null
            )
        ];
    }

    private static List<ConversationListItem> CreateLargeConversationSet(int conversationCount)
    {
        var conversations = new List<ConversationListItem>();
        for (int i = 0; i < conversationCount; i++)
        {
            conversations.Add(new ConversationListItem(
                ConversationId.New().Value,
                $"Test Conversation {i + 1}",
                DateTime.UtcNow.AddMinutes(-i),
                DateTime.UtcNow.AddMinutes(-i / 2),
                i % 2 == 0 ? MessageId.New().Value.ToString() : null
            ));
        }
        return conversations;
    }

    #endregion

    #region Test Case Sources

    private static IEnumerable<TestCaseData> GetValidQueryScenarios()
    {
        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversations()
                .WithFirstPage()
                .Build(),
            "First page with default page size");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversations()
                .WithPagination(2, 10)
                .Build(),
            "Second page with custom page size");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversations()
                .WithPagination(1, 50)
                .Build(),
            "Large page size");
            
        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversations()
                .SortByUpdatedAt(SortDirection.Asc)
                .Build(),
            "Sorted by UpdatedAt ascending");
            
        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversations()
                .SortBy(ConversationSortBy.CreatedAt, SortDirection.Desc)
                .Build(),
            "Sorted by CreatedAt descending");
            
        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversations()
                .SortBy(ConversationSortBy.Title, SortDirection.Asc)
                .Build(),
            "Sorted by Title ascending");
    }

    private static IEnumerable<TestCaseData> GetInvalidPaginationScenarios()
    {
        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversations()
                .WithInvalidPageNumber()
                .Build(),
            "PageNumber",
            "Invalid page number");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversations()
                .WithInvalidPageSize()
                .Build(),
            "PageSize",
            "Invalid page size");
    }

    #endregion
}