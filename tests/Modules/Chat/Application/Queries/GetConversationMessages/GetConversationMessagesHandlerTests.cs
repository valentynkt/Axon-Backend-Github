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
using Axon.Modules.Chat.Domain.Tests.Extensions;
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
    // Dependencies
    private IUserAuthenticationService _mockAuthService = null!;
    private IConversationReadRepository _mockConversationRepository = null!;
    private IMessageReadRepository _mockMessageRepository = null!;
    private IChatTelemetry _mockTelemetry = null!;
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
            _mockMessageRepository,
            _mockTelemetry,
            _mockLogger);
    }

    protected override void ConfigureHandlerDependencies()
    {
        _mockAuthService = Substitute.For<IUserAuthenticationService>();
        _mockConversationRepository = Substitute.For<IConversationReadRepository>();
        _mockMessageRepository = Substitute.For<IMessageReadRepository>();
        _mockTelemetry = Substitute.For<IChatTelemetry>();
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
        string _)
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
        string _)
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
        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Success<UserId, Error>(_testUserId));

        // TODO: Setup repository to throw exception when interfaces are available

        // Act & Assert
        // TODO: Verify exception handling when repository mocks are available
        await Task.CompletedTask;
    }

    #endregion

    #region Performance Tests

    [Test]
    public async Task Handle_WithLargeMessageSet_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        var query = CreateValidQuery();
        _testMessages = CreateLargeMessageSet(1000);
        SetupQueryTestData();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await ExecuteQuery(query);
        stopwatch.Stop();

        // Assert
        result.ShouldBeSuccess();
        stopwatch.Elapsed.ShouldBeLessThan(GetExpectedMaxExecutionTime());
    }

    #endregion

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