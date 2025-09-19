using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Specifications;

namespace Axon.Modules.Chat.Domain.Tests.Builders;

/// <summary>
/// Fluent builder for creating Conversation Specifications in tests.
/// Provides realistic defaults and allows customization for specific specification testing scenarios.
/// </summary>
public class ConversationSpecificationBuilder
{
    private AxonUserId? _ownerId;
    private ConversationStatus? _status;
    private string? _titleContains;
    private string? _searchTerm;
    private int? _minimumMessages;
    private DateTimeOffset? _createdAfter;
    private DateTimeOffset? _createdBefore;
    private DateTimeOffset? _updatedSince;

    /// <summary>
    /// Creates a new specification builder with no filters.
    /// </summary>
    public static ConversationSpecificationBuilder New() => new();

    /// <summary>
    /// Creates a specification builder for active conversations by a specific owner.
    /// </summary>
    public static ConversationSpecificationBuilder ActiveByOwner(AxonUserId ownerId) => 
        new ConversationSpecificationBuilder()
            .ForOwner(ownerId)
            .WithStatus(ConversationStatus.Active);

    /// <summary>
    /// Creates a specification builder for search scenarios.
    /// </summary>
    public static ConversationSpecificationBuilder ForSearch(string searchTerm) =>
        new ConversationSpecificationBuilder().WithSearchTerm(searchTerm);

    /// <summary>
    /// Sets the owner filter.
    /// </summary>
    public ConversationSpecificationBuilder ForOwner(AxonUserId ownerId)
    {
        _ownerId = ownerId;
        return this;
    }

    /// <summary>
    /// Sets the status filter.
    /// </summary>
    public ConversationSpecificationBuilder WithStatus(ConversationStatus status)
    {
        _status = status;
        return this;
    }

    /// <summary>
    /// Sets the title contains filter.
    /// </summary>
    public ConversationSpecificationBuilder WithTitleContaining(string titleContains)
    {
        _titleContains = titleContains;
        return this;
    }

    /// <summary>
    /// Sets the search term filter.
    /// </summary>
    public ConversationSpecificationBuilder WithSearchTerm(string searchTerm)
    {
        _searchTerm = searchTerm;
        return this;
    }

    /// <summary>
    /// Sets the minimum messages filter.
    /// </summary>
    public ConversationSpecificationBuilder WithMinimumMessages(int minimumMessages)
    {
        _minimumMessages = minimumMessages;
        return this;
    }

    /// <summary>
    /// Sets the created after date filter.
    /// </summary>
    public ConversationSpecificationBuilder CreatedAfter(DateTimeOffset createdAfter)
    {
        _createdAfter = createdAfter;
        return this;
    }

    /// <summary>
    /// Sets the created before date filter.
    /// </summary>
    public ConversationSpecificationBuilder CreatedBefore(DateTimeOffset createdBefore)
    {
        _createdBefore = createdBefore;
        return this;
    }

    /// <summary>
    /// Sets the updated since date filter.
    /// </summary>
    public ConversationSpecificationBuilder UpdatedSince(DateTimeOffset updatedSince)
    {
        _updatedSince = updatedSince;
        return this;
    }

    /// <summary>
    /// Creates a date range filter (created between two dates).
    /// </summary>
    public ConversationSpecificationBuilder CreatedBetween(DateTimeOffset startDate, DateTimeOffset endDate)
    {
        _createdAfter = startDate;
        _createdBefore = endDate;
        return this;
    }

    /// <summary>
    /// Builds the ConversationsByOwnerSpec specification.
    /// </summary>
    public ConversationsByOwnerSpec BuildByOwnerSpec()
    {
        if (_ownerId == null)
            throw new InvalidOperationException("Owner ID is required for ConversationsByOwnerSpec");

        return new ConversationsByOwnerSpec(_ownerId.Value);
    }

    /// <summary>
    /// Builds the ConversationsByStatusSpec specification.
    /// </summary>
    public ConversationsByStatusSpec BuildByStatusSpec()
    {
        if (_status == null)
            throw new InvalidOperationException("Status is required for ConversationsByStatusSpec");

        return new ConversationsByStatusSpec(_status.Value);
    }

    /// <summary>
    /// Builds the ActiveByOwnerSpec specification.
    /// </summary>
    public ActiveByOwnerSpec BuildActiveByOwnerSpec()
    {
        if (_ownerId == null)
            throw new InvalidOperationException("Owner ID is required for ActiveByOwnerSpec");

        return new ActiveByOwnerSpec(_ownerId.Value);
    }

    /// <summary>
    /// Builds the ConversationTitleContainsSpec specification.
    /// </summary>
    public ConversationTitleContainsSpec BuildTitleContainsSpec()
    {
        if (string.IsNullOrEmpty(_titleContains))
            throw new InvalidOperationException("Title contains text is required for ConversationTitleContainsSpec");

        return new ConversationTitleContainsSpec(_titleContains);
    }

    /// <summary>
    /// Builds the SearchConversationsSpec specification.
    /// </summary>
    public SearchConversationsSpec BuildSearchSpec()
    {
        if (_ownerId == null)
            throw new InvalidOperationException("Owner ID is required for SearchConversationsSpec");
            
        if (string.IsNullOrEmpty(_searchTerm))
            throw new InvalidOperationException("Search term is required for SearchConversationsSpec");

        return new SearchConversationsSpec(
            _ownerId.Value,
            _searchTerm,
            _status,
            _createdAfter,
            _createdBefore,
            _minimumMessages);
    }

    /// <summary>
    /// Builds the ConversationsWithMinimumMessagesSpec specification.
    /// </summary>
    public ConversationsWithMinimumMessagesSpec BuildMinimumMessagesSpec()
    {
        if (_minimumMessages == null)
            throw new InvalidOperationException("Minimum messages count is required for ConversationsWithMinimumMessagesSpec");

        return new ConversationsWithMinimumMessagesSpec(_minimumMessages.Value);
    }

    /// <summary>
    /// Builds the ConversationsCreatedBetweenSpec specification.
    /// </summary>
    public ConversationsCreatedBetweenSpec BuildCreatedBetweenSpec()
    {
        if (_createdAfter == null || _createdBefore == null)
            throw new InvalidOperationException("Both created after and created before dates are required for ConversationsCreatedBetweenSpec");

        return new ConversationsCreatedBetweenSpec(_createdAfter.Value, _createdBefore.Value);
    }

    /// <summary>
    /// Builds the ConversationsCreatedBetweenForOwnerSpec specification.
    /// </summary>
    public ConversationsCreatedBetweenForOwnerSpec BuildCreatedBetweenForOwnerSpec()
    {
        if (_ownerId == null || _createdAfter == null || _createdBefore == null)
            throw new InvalidOperationException("Owner ID and both created dates are required for ConversationsCreatedBetweenForOwnerSpec");

        return new ConversationsCreatedBetweenForOwnerSpec(_ownerId.Value, _createdAfter.Value, _createdBefore.Value);
    }

    /// <summary>
    /// Builds the ConversationsUpdatedSinceSpec specification.
    /// </summary>
    public ConversationsUpdatedSinceSpec BuildUpdatedSinceSpec()
    {
        if (_updatedSince == null)
            throw new InvalidOperationException("Updated since date is required for ConversationsUpdatedSinceSpec");

        return new ConversationsUpdatedSinceSpec(_updatedSince.Value);
    }

    /// <summary>
    /// Builds the RecentlyUpdatedByOwnerSpec specification.
    /// </summary>
    public RecentlyUpdatedByOwnerSpec BuildRecentlyUpdatedByOwnerSpec()
    {
        if (_ownerId == null || _updatedSince == null)
            throw new InvalidOperationException("Owner ID and updated since date are required for RecentlyUpdatedByOwnerSpec");

        return new RecentlyUpdatedByOwnerSpec(_ownerId.Value, _updatedSince.Value);
    }

    /// <summary>
    /// Factory methods for common specification testing scenarios.
    /// </summary>
    public static class CommonScenarios
    {
        /// <summary>
        /// Creates a spec for finding recent active conversations for the default user.
        /// </summary>
        public static ActiveByOwnerSpec RecentActiveForDefaultUser() =>
            ActiveByOwner(TestConstants.Users.DefaultOwnerId).BuildActiveByOwnerSpec();

        /// <summary>
        /// Creates a spec for conversations containing a specific term.
        /// </summary>
        public static ConversationTitleContainsSpec WithTestTitle() =>
            ForSearch(TestConstants.Specifications.TitleContains)
                .WithTitleContaining(TestConstants.Specifications.TitleContains)
                .BuildTitleContainsSpec();

        /// <summary>
        /// Creates a spec for conversations with minimum message count.
        /// </summary>
        public static ConversationsWithMinimumMessagesSpec WithMinimumTestMessages() =>
            New().WithMinimumMessages(TestConstants.Specifications.MinimumMessages)
                 .BuildMinimumMessagesSpec();

        /// <summary>
        /// Creates a spec for conversations created in a test date range.
        /// </summary>
        public static ConversationsCreatedBetweenSpec InTestDateRange() =>
            New().CreatedBetween(
                    TestConstants.Specifications.CreatedAfter,
                    TestConstants.Specifications.CreatedBefore)
                 .BuildCreatedBetweenSpec();
    }
}