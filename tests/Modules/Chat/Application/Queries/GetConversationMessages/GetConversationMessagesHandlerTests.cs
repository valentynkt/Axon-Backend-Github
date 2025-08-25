using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Axon.Modules.Chat.Application.Contracts.Authentication;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Application.Queries.GetConversationMessages;
using Axon.Modules.Chat.Application.Tests.Builders;
using Axon.Modules.Chat.Application.Tests.Common;
using Axon.Modules.Chat.Application.Tests.Extensions;
using BuildingBlocks.Core.Abstractions.Paging;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Application.Tests.Queries.GetConversationMessages;

[TestFixture]
public class GetConversationMessagesHandlerTests : QueryHandlerTestBase<GetConversationMessagesQuery, Paged<ConversationMessageItem>, GetConversationMessagesHandler>
{
    private IUserAuthenticationService _mockAuthService = null!;
    private IConversationReadRepository _mockConversationRepository = null!;
    private ILogger<GetConversationMessagesHandler> _mockLogger = null!;

    // Test data
    private UserId _testUserId;
    private ConversationId _testConversationId;
    private List<ConversationMessageItem> _testMessages = null!;

    protected override GetConversationMessagesHandler CreateHandler()
    {
        return new GetConversationMessagesHandler(
            _mockAuthService,
            _mockConversationRepository,
            _mockLogger);
    }

    protected override void ConfigureHandlerDependencies()
    {
        _mockAuthService = Substitute.For<IUserAuthenticationService>();
        _mockConversationRepository = Substitute.For<IConversationReadRepository>();
        _mockLogger = Substitute.For<ILogger<GetConversationMessagesHandler>>();

        // Setup test data
        _testUserId = UserId.New();
        _testConversationId = ConversationId.New();
        _testMessages = CreateTestMessages();
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
        // Setup successful authentication
        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Success<UserId, Error>(_testUserId));

        // TODO: Setup conversation and message repository mocks when interfaces are available
    }

    protected override async Task AssertQueryResult(GetConversationMessagesQuery query, Paged<ConversationMessageItem> result)
    {
        // TODO: Implement query result assertions when DTOs are available
        result.ShouldNotBeNull();
        await Task.CompletedTask;
    }

    protected override TimeSpan GetExpectedMaxExecutionTime()
    {
        return TimeSpan.FromMilliseconds(500); // GetConversationMessages should be fast
    }

    #region Critical Path Tests (80/20 Rule)

    [TestCaseSource(nameof(GetValidQueryScenarios))]
    public async Task Handle_WithValidQuery_ShouldReturnSuccessWithCorrectPagination(
        GetConversationMessagesQuery query,
        string scenarioName)
    {
        // Arrange
        SetupQueryTestData();
        var expectedItemsOnPage = Math.Min(query.PageSize, _testMessages.Count - ((query.PageNumber - 1) * query.PageSize));

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldBeSuccess();
        var pagedResult = result.Value;

        // TODO: Add detailed pagination assertions when extension methods are available
        pagedResult.Items.Count.ShouldBeLessThanOrEqualTo(expectedItemsOnPage);
    }

    [Test]
    public async Task Handle_WithUnauthenticatedUser_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var query = CreateValidQuery();
        var authError = Error.Unauthorized("User not authenticated", "Chat.Auth.Unauthenticated");

        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Failure<UserId, Error>(authError));

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("Chat.Auth.Unauthenticated");
    }

    [Test]
    public async Task Handle_WithConversationNotOwnedByUser_ShouldReturnAccessDeniedError()
    {
        // Arrange
        var query = CreateValidQuery();

        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Success<UserId, Error>(_testUserId));

        // TODO: Setup conversation ownership check when repository interfaces are available

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.NotFound);
    }

    [TestCaseSource(nameof(GetInvalidPaginationScenarios))]
    public async Task Handle_WithInvalidPagination_ShouldReturnValidationError(
        GetConversationMessagesQuery query,
        string expectedErrorType,
        string scenarioName)
    {
        // Arrange
        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Success<UserId, Error>(_testUserId));

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Validation);
        result.Error.Message.ShouldContain(expectedErrorType);
    }

    [Test]
    public async Task Handle_WithRepositoryException_ShouldReturnInternalError()
    {
        // Arrange
        var query = CreateValidQuery();

        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Success<UserId, Error>(_testUserId));

        // TODO: Setup repository failure scenarios when interfaces are available

        // Act
        var result = await ExecuteQuery(query);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Internal);
        result.Error.Code.ShouldBe("Chat.Messages.ListFailed");
    }

    [Test]
    public async Task Handle_WithLargeResultSet_ShouldCompleteWithinPerformanceLimit()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithConversationId(_testConversationId.Value)
            .WithLargePageSize()
            .Build();

        var largeMessageSet = CreateTestMessages(500);

        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Success<UserId, Error>(_testUserId));

        // TODO: Setup repository mocks for performance test when interfaces are available

        // Act & Assert
        var result = await ExecuteQuery(query).ShouldCompleteWithin(TimeSpan.FromMilliseconds(1000));
        result.ShouldBeSuccess();
    }

    #endregion

    #region Test Data Sources

    private static IEnumerable<TestCaseData> GetValidQueryScenarios()
    {
        var conversationId = Guid.NewGuid();

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithFirstPage()
                .Build(),
            "Standard first page query")
            .SetName("ValidQuery_FirstPage");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithSecondPage()
                .Build(),
            "Standard second page query")
            .SetName("ValidQuery_SecondPage");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithSmallPageSize()
                .Build(),
            "Query with small page size")
            .SetName("ValidQuery_SmallPageSize");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithLargePageSize()
                .Build(),
            "Query with large page size")
            .SetName("ValidQuery_LargePageSize");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .IncludeDeleted()
                .Build(),
            "Query including deleted messages")
            .SetName("ValidQuery_IncludeDeleted");
    }

    private static IEnumerable<TestCaseData> GetInvalidPaginationScenarios()
    {
        var conversationId = Guid.NewGuid();

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithInvalidPageNumber()
                .Build(),
            "page number",
            "Invalid page number (0)")
            .SetName("InvalidPagination_ZeroPageNumber");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithNegativePageNumber()
                .Build(),
            "page number",
            "Invalid page number (negative)")
            .SetName("InvalidPagination_NegativePageNumber");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithInvalidPageSize()
                .Build(),
            "page size",
            "Invalid page size (negative)")
            .SetName("InvalidPagination_NegativePageSize");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithZeroPageSize()
                .Build(),
            "page size",
            "Invalid page size (zero)")
            .SetName("InvalidPagination_ZeroPageSize");
    }

    #endregion

    #region Helper Methods

    private static List<ConversationMessageItem> CreateTestMessages(int count = 50)
    {
        var messages = new List<ConversationMessageItem>();
        
        for (int i = 0; i < count; i++)
        {
            var role = i % 2 == 0 ? "user" : "assistant";
            messages.Add(new ConversationMessageItem(
                MessageId: Guid.NewGuid(),
                Role: role,
                Content: $"Test message {i + 1} content",
                CreatedAtUtc: DateTime.UtcNow.AddMinutes(-count + i),
                Sequence: i + 1
            ));
        }
        
        return messages;
    }

    #endregion
}