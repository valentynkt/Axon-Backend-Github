using Axon.Modules.Chat.Application.Services;
using Axon.Shared.Domain;
using BuildingBlocks.Core.Domain.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that automatically populates audit fields for IAuditable entities
/// Uses direct backing field access for optimal performance as specified in SPARC architecture
/// Implements lines 328-396 from PHASE3_TASK3_EVENT_SOURCING_CQRS_ARCHITECTURE.md
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AuditSaveChangesInterceptor> _logger;

    public AuditSaveChangesInterceptor(
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<AuditSaveChangesInterceptor> logger)
    {
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Intercepts synchronous SaveChanges operations to update audit fields
    /// Implementation of lines 342-356 from SPARC specification
    /// </summary>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Intercepts asynchronous SaveChangesAsync operations to update audit fields
    /// Implementation of lines 342-356 from SPARC specification
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Updates audit fields for all IAuditable entities in the change tracker
    /// Uses direct backing field access for optimal performance per SPARC architecture
    /// Implementation of lines 362-395 from SPARC specification
    /// </summary>
    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null)
        {
            _logger.LogWarning("DbContext is null in audit interceptor");
            return;
        }

        var currentTime = _dateTimeProvider.UtcNow;
        var currentUser = _currentUserService.GetUserIdOrDefault("SYSTEM");

        var auditableEntries = context.ChangeTracker.Entries()
            .Where(entry => entry.Entity is IAuditable && 
                           (entry.State == EntityState.Added || entry.State == EntityState.Modified))
            .ToList();

        foreach (var entry in auditableEntries)
        {
            try
            {
                if (entry.State == EntityState.Added)
                {
                    // Use direct backing field access for creation audit fields
                    entry.Property("_createdAtUtc").CurrentValue = currentTime;
                    entry.Property("_createdBy").CurrentValue = currentUser;
                    entry.Property("_updatedAtUtc").CurrentValue = currentTime;
                    entry.Property("_updatedBy").CurrentValue = currentUser;

                    _logger.LogDebug("Set creation audit fields for entity {EntityType} with user {UserId}", 
                        entry.Entity.GetType().Name, currentUser);
                }
                else if (entry.State == EntityState.Modified)
                {
                    // Use direct backing field access for update audit fields only
                    entry.Property("_updatedAtUtc").CurrentValue = currentTime;
                    entry.Property("_updatedBy").CurrentValue = currentUser;

                    _logger.LogDebug("Set update audit fields for entity {EntityType} with user {UserId}", 
                        entry.Entity.GetType().Name, currentUser);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, 
                    "Failed to update audit fields for entity {EntityType}. Entity may not have audit backing fields configured.", 
                    entry.Entity.GetType().Name);
            }
        }

        if (auditableEntries.Any())
        {
            _logger.LogDebug("Updated audit fields for {Count} entities", auditableEntries.Count);
        }
    }
}