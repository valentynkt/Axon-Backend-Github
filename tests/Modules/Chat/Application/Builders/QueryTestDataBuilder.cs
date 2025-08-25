namespace Axon.Modules.Chat.Application.Tests.Builders;

/// <summary>
/// Builder for creating test queries with configurable properties.
/// Follows the Builder pattern to provide fluent API for test data creation.
/// Extensible design allows for easy addition of new query types.
/// </summary>
public static class QueryTestDataBuilder
{
    /// <summary>
    /// Creates a builder for GetConversationsQuery
    /// </summary>
    public static GetConversationsQueryBuilder GetConversations() => new();
}

/// <summary>
/// Builder for creating GetConversationsQuery test instances
/// </summary>
public class GetConversationsQueryBuilder
{
    private int _pageNumber = 1;
    private int _pageSize = 10;
    private ConversationSortBy _sortBy = ConversationSortBy.UpdatedAt;
    private SortDirection _sortDirection = SortDirection.Desc;
    private string? _titleContains;

    public GetConversationsQueryBuilder WithPagination(int pageNumber, int pageSize)
    {
        _pageNumber = pageNumber;
        _pageSize = pageSize;
        return this;
    }

    public GetConversationsQueryBuilder WithFirstPage(int pageSize = 10)
    {
        _pageNumber = 1;
        _pageSize = pageSize;
        return this;
    }

    public GetConversationsQueryBuilder WithSecondPage(int pageSize = 10)
    {
        _pageNumber = 2;
        _pageSize = pageSize;
        return this;
    }

    public GetConversationsQueryBuilder WithLargePageSize()
    {
        _pageSize = 1000;
        return this;
    }

    public GetConversationsQueryBuilder WithInvalidPageNumber()
    {
        _pageNumber = 0;
        return this;
    }

    public GetConversationsQueryBuilder WithInvalidPageSize()
    {
        _pageSize = -1;
        return this;
    }

    public GetConversationsQueryBuilder SortBy(ConversationSortBy sortBy, SortDirection direction = SortDirection.Desc)
    {
        _sortBy = sortBy;
        _sortDirection = direction;
        return this;
    }

    public GetConversationsQueryBuilder SortByUpdatedAt(SortDirection direction = SortDirection.Desc)
    {
        _sortBy = ConversationSortBy.UpdatedAt;
        _sortDirection = direction;
        return this;
    }

    public GetConversationsQueryBuilder WithTitleFilter(string titleContains)
    {
        _titleContains = titleContains;
        return this;
    }

    public GetConversationsQueryBuilder WithEmptyTitleFilter()
    {
        _titleContains = string.Empty;
        return this;
    }

    public GetConversationsQuery Build()
    {
        return new GetConversationsQuery(
            PageNumber: _pageNumber,
            PageSize: _pageSize,
            SortBy: _sortBy,
            SortDirection: _sortDirection,
            TitleContains: _titleContains);
    }
}