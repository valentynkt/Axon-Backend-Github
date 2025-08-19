using Asp.Versioning;
using Axon.Modules.Chat.ReadModels;
using BuildingBlocks.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Axon.Api.Controllers.OData;

[ApiVersion("1.0")]
[Route("odata/v1")]
public class ConversationsController : ODataController
{
    private readonly IReadRepository<ConversationReadModel> _conversationRepository;
    private readonly IReadRepository<MessageReadModel> _messageRepository;
    private readonly ILogger<ConversationsController> _logger;

    public ConversationsController(
        IReadRepository<ConversationReadModel> conversationRepository,
        IReadRepository<MessageReadModel> messageRepository,
        ILogger<ConversationsController> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get conversations with OData query support
    /// Supports: $filter, $orderby, $top, $skip, $count, $select
    /// Server-driven paging via $skiptoken is automatically enabled
    /// </summary>
    [HttpGet("Conversations")]
    [EnableQuery(PageSize = 50, MaxTop = 100, AllowedQueryOptions = AllowedQueryOptions.All)]
    public IQueryable<ConversationReadModel> GetConversations()
    {
        _logger.LogDebug("Fetching conversations with OData query");
        
        // Repository already applies user scope and stable ordering
        return _conversationRepository.Query();
    }

    /// <summary>
    /// Get a single conversation by ID
    /// </summary>
    [HttpGet("Conversations({key})")]
    [EnableQuery(AllowedQueryOptions = AllowedQueryOptions.Select | AllowedQueryOptions.Expand)]
    public ActionResult<ConversationReadModel> GetConversation([FromRoute] Guid key)
    {
        _logger.LogDebug("Fetching conversation {ConversationId}", key);
        
        var conversation = _conversationRepository.Query(c => c.Id == key).FirstOrDefault();
        
        if (conversation == null)
        {
            return NotFound();
        }
        
        return Ok(conversation);
    }

    /// <summary>
    /// Get messages for a specific conversation
    /// Supports full OData query capabilities
    /// </summary>
    [HttpGet("Conversations({key})/Messages")]
    [EnableQuery(PageSize = 50, MaxTop = 100)]
    public IQueryable<MessageReadModel> GetMessages([FromRoute] Guid key)
    {
        _logger.LogDebug("Fetching messages for conversation {ConversationId}", key);
        
        // Verify conversation exists and user has access (via repository filtering)
        var conversationExists = _conversationRepository.Query(c => c.Id == key).Any();
        
        if (!conversationExists)
        {
            // Return empty queryable instead of throwing to maintain OData compatibility
            return Enumerable.Empty<MessageReadModel>().AsQueryable();
        }
        
        return _messageRepository.Query(m => m.ConversationId == key);
    }
}