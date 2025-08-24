namespace Axon.Modules.Chat.Application.Common.Sorting;

/// <summary>
/// Defines the available fields for sorting conversations.
/// </summary>
public enum ConversationSortBy
{
    /// <summary>
    /// Sort by the last update timestamp (most recently modified conversations).
    /// This is the default sorting option.
    /// </summary>
    UpdatedAt,

    /// <summary>
    /// Sort by creation timestamp (oldest/newest conversations).
    /// </summary>
    CreatedAt,

    /// <summary>
    /// Sort by conversation title alphabetically.
    /// </summary>
    Title
}