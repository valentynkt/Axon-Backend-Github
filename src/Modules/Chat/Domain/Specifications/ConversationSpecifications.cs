using System.Linq.Expressions;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using BuildingBlocks.Core.Domain.Specifications;

namespace Axon.Modules.Chat.Domain.Specifications;

/// <summary>
/// Specification for filtering conversations by owner.
/// </summary>
public sealed class ConversationsByOwnerSpec : Specification<Conversation.Conversation>
{
    private readonly UserId _ownerId;

    public ConversationsByOwnerSpec(UserId ownerId)
    {
        _ownerId = ownerId;
    }

    public override Expression<Func<Conversation.Conversation, bool>> ToExpression()
    {
        return conversation => conversation.OwnerId == _ownerId;
    }
}

/// <summary>
/// Specification for filtering active conversations.
/// </summary>
public sealed class ActiveConversationsSpec : Specification<Conversation.Conversation>
{
    public override Expression<Func<Conversation.Conversation, bool>> ToExpression()
    {
        return conversation => conversation.Status == ConversationStatus.Active;
    }
}

/// <summary>
/// Specification for filtering conversations by status.
/// </summary>
public sealed class ConversationsByStatusSpec : Specification<Conversation.Conversation>
{
    private readonly ConversationStatus _status;

    public ConversationsByStatusSpec(ConversationStatus status)
    {
        _status = status;
    }

    public override Expression<Func<Conversation.Conversation, bool>> ToExpression()
    {
        return conversation => conversation.Status == _status;
    }
}

/// <summary>
/// Specification for filtering conversations created within a date range.
/// </summary>
public sealed class ConversationsCreatedBetweenSpec : Specification<Conversation.Conversation>
{
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;

    public ConversationsCreatedBetweenSpec(DateTime fromDate, DateTime toDate)
    {
        _fromDate = fromDate;
        _toDate = toDate;
    }

    public override Expression<Func<Conversation.Conversation, bool>> ToExpression()
    {
        return conversation => conversation.CreatedAt >= _fromDate && 
                              conversation.CreatedAt <= _toDate;
    }
}

/// <summary>
/// Specification for filtering conversations by minimum message count.
/// </summary>
public sealed class ConversationsWithMinimumMessagesSpec : Specification<Conversation.Conversation>
{
    private readonly int _minMessageCount;

    public ConversationsWithMinimumMessagesSpec(int minMessageCount)
    {
        _minMessageCount = minMessageCount;
    }

    public override Expression<Func<Conversation.Conversation, bool>> ToExpression()
    {
        return conversation => conversation.MessageCount >= _minMessageCount;
    }
}

/// <summary>
/// Specification for filtering conversations by title search.
/// </summary>
public sealed class ConversationTitleSearchSpec : Specification<Conversation.Conversation>
{
    private readonly string _searchTerm;

    public ConversationTitleSearchSpec(string searchTerm)
    {
        _searchTerm = searchTerm?.Trim() ?? string.Empty;
    }

    public override Expression<Func<Conversation.Conversation, bool>> ToExpression()
    {
        if (string.IsNullOrWhiteSpace(_searchTerm))
        {
            return conversation => true; // No filtering if search term is empty
        }

        return conversation => conversation.Title.Contains(_searchTerm);
    }
}

/// <summary>
/// Specification for filtering recently active conversations.
/// </summary>
public sealed class RecentlyActiveConversationsSpec : Specification<Conversation.Conversation>
{
    private readonly TimeSpan _recentWindow;

    public RecentlyActiveConversationsSpec(TimeSpan? recentWindow = null)
    {
        _recentWindow = recentWindow ?? TimeSpan.FromDays(7);
    }

    public override Expression<Func<Conversation.Conversation, bool>> ToExpression()
    {
        var cutoffDate = DateTime.UtcNow - _recentWindow;
        return conversation => conversation.UpdatedAt >= cutoffDate;
    }
}

/// <summary>
/// Specification combining owner and active status filters - common query pattern.
/// </summary>
public sealed class ActiveConversationsByOwnerSpec : Specification<Conversation.Conversation>
{
    private readonly UserId _ownerId;

    public ActiveConversationsByOwnerSpec(UserId ownerId)
    {
        _ownerId = ownerId;
    }

    public override Expression<Func<Conversation.Conversation, bool>> ToExpression()
    {
        return conversation => conversation.OwnerId == _ownerId && 
                              conversation.Status == ConversationStatus.Active;
    }
}

/// <summary>
/// Complex specification for conversation search with multiple criteria.
/// Demonstrates composition of multiple conditions.
/// </summary>
public sealed class ConversationSearchSpec : Specification<Conversation.Conversation>
{
    private readonly UserId _ownerId;
    private readonly string? _titleSearch;
    private readonly ConversationStatus? _status;
    private readonly DateTime? _fromDate;
    private readonly DateTime? _toDate;
    private readonly int? _minMessages;

    public ConversationSearchSpec(
        UserId ownerId,
        string? titleSearch = null,
        ConversationStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? minMessages = null)
    {
        _ownerId = ownerId;
        _titleSearch = titleSearch?.Trim();
        _status = status;
        _fromDate = fromDate;
        _toDate = toDate;
        _minMessages = minMessages;
    }

    public override Expression<Func<Conversation.Conversation, bool>> ToExpression()
    {
        return conversation =>
            conversation.OwnerId == _ownerId &&
            (_titleSearch == null || conversation.Title.Contains(_titleSearch)) &&
            (_status == null || conversation.Status == _status) &&
            (_fromDate == null || conversation.CreatedAt >= _fromDate) &&
            (_toDate == null || conversation.CreatedAt <= _toDate) &&
            (_minMessages == null || conversation.MessageCount >= _minMessages);
    }
}

/// <summary>
/// Specification factory providing common specification combinations.
/// Reduces code duplication and provides consistent query patterns.
/// </summary>
public static class ConversationSpecifications
{
    /// <summary>
    /// Get all active conversations for a user.
    /// </summary>
    public static Specification<Conversation.Conversation> ActiveByOwner(UserId ownerId)
    {
        return new ActiveConversationsByOwnerSpec(ownerId);
    }

    /// <summary>
    /// Get recent conversations for a user.
    /// </summary>
    public static Specification<Conversation.Conversation> RecentByOwner(
        UserId ownerId, 
        TimeSpan? recentWindow = null)
    {
        return CommonSpecifications.Active<Conversation.Conversation>()
            .And(new ConversationsByOwnerSpec(ownerId))
            .And(new RecentlyActiveConversationsSpec(recentWindow));
    }

    /// <summary>
    /// Get conversations with activity in a date range.
    /// </summary>
    public static Specification<Conversation.Conversation> ByOwnerAndDateRange(
        UserId ownerId,
        DateTime fromDate,
        DateTime toDate)
    {
        return new ConversationsByOwnerSpec(ownerId)
            .And(new ConversationsCreatedBetweenSpec(fromDate, toDate));
    }

    /// <summary>
    /// Search conversations with multiple criteria.
    /// </summary>
    public static Specification<Conversation.Conversation> Search(
        UserId ownerId,
        string? titleSearch = null,
        ConversationStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? minMessages = null)
    {
        return new ConversationSearchSpec(
            ownerId, titleSearch, status, fromDate, toDate, minMessages);
    }

    /// <summary>
    /// Get conversations suitable for archiving (old and inactive).
    /// </summary>
    public static Specification<Conversation.Conversation> EligibleForArchiving(
        TimeSpan? inactivityPeriod = null)
    {
        var period = inactivityPeriod ?? TimeSpan.FromDays(90);
        var cutoffDate = DateTime.UtcNow - period;

        return new ConversationsByStatusSpec(ConversationStatus.Completed)
            .And(CommonSpecifications.CreatedBefore<Conversation.Conversation>(cutoffDate));
    }

    /// <summary>
    /// Get high-activity conversations for analytics.
    /// </summary>
    public static Specification<Conversation.Conversation> HighActivity(
        int minMessageCount = 50)
    {
        return CommonSpecifications.Active<Conversation.Conversation>()
            .And(new ConversationsWithMinimumMessagesSpec(minMessageCount));
    }
}