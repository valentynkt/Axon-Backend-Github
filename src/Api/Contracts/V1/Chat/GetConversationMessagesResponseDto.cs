namespace Axon.Api.Contracts.V1.Chat;

/// <summary>
/// Response DTO containing paginated conversation messages with metadata.
/// </summary>
public sealed record GetConversationMessagesResponseDto(
    /// <summary>
    /// The list of messages for the current page.
    /// </summary>
    IReadOnlyList<ConversationMessageDto> Items,
    
    /// <summary>
    /// The current page number (1-based).
    /// </summary>
    int PageNumber,
    
    /// <summary>
    /// The number of items requested per page.
    /// </summary>
    int PageSize,
    
    /// <summary>
    /// The total number of messages across all pages.
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