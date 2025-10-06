using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Application.Contracts.Persistence;

/// <summary>
/// Unified database context for Chat module.
/// Supports both read and write operations with appropriate optimizations.
/// </summary>
public interface IChatDbContext : IDbContext
{
    /// <summary>Logical module name (schema/diagnostics separation).</summary>
    string ModuleName { get; }

    /// <summary>Conversations aggregate root.</summary>
    DbSet<Conversation> Conversations { get; }

    /// <summary>
    /// No-tracking queryable for read operations (optimized for queries).
    /// Use this for read-only operations to improve performance.
    /// </summary>
    IQueryable<TEntity> Query<TEntity>() where TEntity : class;

    /// <summary>Collect domain events from tracked aggregates.</summary>
    new IReadOnlyList<IDomainEvent> GetDomainEvents();

    /// <summary>Clear tracked domain events after publication.</summary>
    void ClearDomainEvents();
}
