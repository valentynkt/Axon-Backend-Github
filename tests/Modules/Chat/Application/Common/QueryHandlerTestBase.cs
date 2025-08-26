namespace Axon.Modules.Chat.Application.Tests.Common;

/// <summary>
/// Specialized base class for testing query handlers.
/// Provides infrastructure for testing queries that read data without side effects.
/// Follows SOLID principles with template method pattern for extensible query testing.
/// </summary>
/// <typeparam name="TQuery">The query type being tested</typeparam>
/// <typeparam name="TResult">The result type returned by the query</typeparam>
/// <typeparam name="THandler">The query handler type</typeparam>
public abstract class QueryHandlerTestBase<TQuery, TResult, THandler> : ApplicationTestBase
    where TQuery : IRequest<Result<TResult, Error>>
    where THandler : class, IRequestHandler<TQuery, Result<TResult, Error>>
{
    protected THandler Handler { get; private set; } = null!;

    protected override void OnApplicationSetUp()
    {
        Handler = CreateHandler();
        ConfigureHandlerDependencies();
    }

    /// <summary>
    /// Creates the query handler instance with its dependencies.
    /// Must be implemented by derived test classes.
    /// </summary>
    protected abstract THandler CreateHandler();

    /// <summary>
    /// Override to configure handler-specific dependencies and mocks
    /// </summary>
    protected virtual void ConfigureHandlerDependencies() { }

    /// <summary>
    /// Executes a query and returns the result
    /// </summary>
    protected async Task<Result<TResult, Error>> ExecuteQuery(
        TQuery query, 
        CancellationToken cancellationToken = default)
    {
        return await Handler.Handle(query, cancellationToken);
    }

    /// <summary>
    /// Creates a valid query for testing
    /// </summary>
    protected abstract TQuery CreateValidQuery();

    /// <summary>
    /// Creates an invalid query for negative testing
    /// </summary>
    protected abstract TQuery CreateInvalidQuery();

    /// <summary>
    /// Test template for successful query execution
    /// </summary>
    [Test]
    public async Task Handle_WithValidQuery_ShouldReturnSuccess()
    {
        // Arrange
        var query = CreateValidQuery();
        SetupQueryTestData();
        
        // Act
        var result = await ExecuteQuery(query);
        
        // Assert
        AssertSuccess(result);
        await AssertQueryResult(query, result.Value);
    }

    /// <summary>
    /// Test template for invalid query handling
    /// </summary>
    [Test]
    public async Task Handle_WithInvalidQuery_ShouldReturnFailure()
    {
        // Arrange
        var query = CreateInvalidQuery();
        
        // Act
        var result = await ExecuteQuery(query);
        
        // Assert
        AssertFailure(result);
    }
    
    /// <summary>
    /// Test template for query performance verification
    /// </summary>
    [Test]
    public async Task Handle_QueryExecution_ShouldMeetPerformanceRequirements()
    {
        // Arrange
        var query = CreateValidQuery();
        SetupQueryTestData();
        var maxExecutionTime = GetExpectedMaxExecutionTime();
        
        // Act & Assert
        await AssertExecutionTime(
            () => ExecuteQuery(query),
            maxExecutionTime,
            $"Query should execute within {maxExecutionTime.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Override to set up test data needed for query execution
    /// </summary>
    protected virtual void SetupQueryTestData() { }

    /// <summary>
    /// Override to verify query-specific results
    /// </summary>
    protected virtual Task AssertQueryResult(TQuery query, TResult result)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Override to specify expected query execution time
    /// </summary>
    protected virtual TimeSpan GetExpectedMaxExecutionTime()
    {
        return TimeSpan.FromMilliseconds(1000); // Default 1 second
    }

    /// <summary>
    /// Helper to verify pagination in query results
    /// </summary>
    protected static void AssertPaginationResult<T>(
        Paged<T> pagedResult,
        int expectedPageNumber,
        int expectedPageSize,
        int? expectedTotalCount = null)
    {
        pagedResult.PageNumber.ShouldBe(expectedPageNumber);
        pagedResult.PageSize.ShouldBe(expectedPageSize);
        pagedResult.Items.Count.ShouldBeLessThanOrEqualTo(expectedPageSize);
        
        if (expectedTotalCount.HasValue)
        {
            pagedResult.TotalCount.ShouldBe(expectedTotalCount.Value);
        }
    }

    /// <summary>
    /// Helper to verify sorting in query results
    /// </summary>
    protected static void AssertSortingResult<T, TKey>(
        IEnumerable<T> items,
        Func<T, TKey> keySelector,
        bool ascending = true) where TKey : IComparable<TKey>
    {
        var itemsList = items.ToList();
        if (itemsList.Count <= 1) return;

        var sortedItems = ascending
            ? itemsList.OrderBy(keySelector)
            : itemsList.OrderByDescending(keySelector);

        itemsList.ShouldBe(sortedItems.ToList());
    }

    /// <summary>
    /// Helper to create query test scenarios for parameterized tests
    /// </summary>
    protected static TestCaseData CreateQueryTestCase<T>(
        T query,
        bool shouldSucceed,
        string testName) where T : TQuery
    {
        return new TestCaseData(query, shouldSucceed).SetName(testName);
    }

    /// <summary>
    /// Verifies that read operations don't produce side effects
    /// </summary>
    protected void AssertNoSideEffects()
    {
        // This would verify that no state changes occurred
        // Implementation depends on your specific infrastructure
        // Could check that no writes were made to repositories, etc.
    }

    /// <summary>
    /// Helper to verify filtering in query results
    /// </summary>
    protected static void AssertFilteringResult<T>(
        IEnumerable<T> items,
        Predicate<T> filter,
        string filterDescription)
    {
        var itemsList = items.ToList();
        var allItemsMatchFilter = itemsList.All(item => filter(item));
        
        allItemsMatchFilter.ShouldBeTrue(
            $"All items should match the filter criteria: {filterDescription}");
    }
}