namespace Axon.Api.Contracts.V1.Chat;

/// <summary>
/// Paginated response containing conversation list with comprehensive pagination metadata.
/// Provides all information needed for client-side pagination controls and navigation.
/// </summary>
public sealed record GetConversationsResponseDto(
    /// <summary>
    /// The list of conversations for the current page.
    /// </summary>
    IReadOnlyList<ConversationItemDto> Items,
    
    /// <summary>
    /// The current page number (1-based).
    /// </summary>
    int PageNumber,
    
    /// <summary>
    /// The number of items requested per page.
    /// </summary>
    int PageSize,
    
    /// <summary>
    /// The total number of conversations across all pages.
    /// </summary>
    long TotalCount,
    
    /// <summary>
    /// The total number of pages available.
    /// Calculated as: ceiling(TotalCount / PageSize).
    /// </summary>
    int TotalPages,
    
    /// <summary>
    /// Indicates whether there is a previous page available.
    /// True when PageNumber > 1.
    /// </summary>
    bool HasPrevious,
    
    /// <summary>
    /// Indicates whether there is a next page available.
    /// True when PageNumber < TotalPages.
    /// </summary>
    bool HasNext,
    
    /// <summary>
    /// The actual number of items in the current page.
    /// May be less than PageSize on the last page.
    /// </summary>
    int Count,
    
    /// <summary>
    /// Indicates whether the current page contains no items.
    /// </summary>
    bool IsEmpty,
    
    /// <summary>
    /// The 1-based index of the first item on the current page.
    /// 0 if there are no items.
    /// </summary>
    long FirstItemIndex,
    
    /// <summary>
    /// The 1-based index of the last item on the current page.
    /// Equal to FirstItemIndex + Count - 1.
    /// </summary>
    long LastItemIndex
);