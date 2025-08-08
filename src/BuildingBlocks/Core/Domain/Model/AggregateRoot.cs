using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using Error = BuildingBlocks.Core.Diagnostics.Errors.Error;

namespace BuildingBlocks.Core.Domain.Model;

/// <summary>
/// Base class for aggregate roots following tactical DDD.
/// Provides domain event support without event sourcing (MVP approach).
/// Encapsulates business logic and maintains invariants.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot<TId>
    where TId : IStrongId
{
    private readonly List<IDomainEvent> _domainEvents = new();
    private readonly List<IBusinessRule> _brokenRules = new();
    
    protected AggregateRoot(TId id) : base(id)
    {
    }
    
    protected AggregateRoot() : base()
    {
    }
    
    #region Domain Events (No Event Sourcing)
    
    /// <summary>
    /// Domain events to be dispatched after persistence
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    /// <summary>
    /// Raise a domain event to be dispatched after save
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
    
    /// <summary>
    /// Clear all domain events (called after dispatch)
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
    
    #endregion
    
    #region Business Rules
    
    /// <summary>
    /// Check business rule and record if broken
    /// </summary>
    protected Result<Unit> CheckRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        
        if (rule.IsBroken())
        {
            _brokenRules.Add(rule);
            return Result<Unit>.Failure(
                Error.BusinessRule(rule.Message, rule.Code));
        }
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Check multiple business rules
    /// </summary>
    protected Result<Unit> CheckRules(params IBusinessRule[] rules)
    {
        var errors = new List<Error>();
        
        foreach (var rule in rules)
        {
            if (rule.IsBroken())
            {
                _brokenRules.Add(rule);
                errors.Add(Error.BusinessRule(rule.Message, rule.Code));
            }
        }
        
        if (errors.Any())
        {
            return Result<Unit>.Failure(Error.Aggregate(errors.ToArray()));
        }
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Get all broken rules
    /// </summary>
    public IReadOnlyList<IBusinessRule> GetBrokenRules() => _brokenRules.AsReadOnly();
    
    #endregion
    
    #region Invariant Validation
    
    /// <summary>
    /// Validate aggregate state against all invariants
    /// </summary>
    public virtual Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        // Check all invariants
        var invariants = GetInvariants();
        foreach (var invariant in invariants)
        {
            if (invariant.IsBroken())
            {
                errors.Add(Error.BusinessRule(invariant.Message, invariant.Code));
            }
        }
        
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
    
    /// <summary>
    /// Override to provide aggregate invariants
    /// These are rules that must ALWAYS be true for the aggregate
    /// </summary>
    protected virtual IEnumerable<IBusinessRule> GetInvariants()
    {
        return Enumerable.Empty<IBusinessRule>();
    }
    
    /// <summary>
    /// Check invariants for validation purposes (not for enforcement)
    /// Use Validate() method directly instead of this helper
    /// </summary>
    [Obsolete("Use Validate() method directly. Invariants should not be used for enforcement after state changes.")]
    protected Result<Unit> EnsureInvariants()
    {
        var validation = Validate();
        return validation.IsValid 
            ? Result<Unit>.Success(Unit.Value)
            : validation.ToResultWithAggregatedError();
    }
    
    #endregion
    
    #region State Management
    
    /// <summary>
    /// Apply state changes safely - business rules must be checked BEFORE calling this
    /// This method assumes all preconditions have been validated
    /// </summary>
    protected Result<Unit> ApplyChange(Action stateChange)
    {
        ArgumentNullException.ThrowIfNull(stateChange);
        
        // Apply the state change (preconditions must be checked by caller)
        stateChange();
        
        // Update modification tracking
        Touch();
        
        // Invariants should always be satisfied if business rules were checked properly
        // We can optionally validate in DEBUG builds for development safety
#if DEBUG
        var validation = Validate();
        if (validation.IsInvalid)
        {
            throw new InvalidOperationException(
                $"Invariant violation detected after state change. This indicates incorrect business rule validation. " +
                $"Errors: {string.Join("; ", validation.Errors.Select(e => e.Message))}");
        }
#endif
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Apply async state changes safely - business rules must be checked BEFORE calling this
    /// This method assumes all preconditions have been validated
    /// </summary>
    protected async Task<Result<Unit>> ApplyChangeAsync(Func<Task> stateChangeAsync)
    {
        ArgumentNullException.ThrowIfNull(stateChangeAsync);
        
        // Apply the async state change (preconditions must be checked by caller)
        await stateChangeAsync().ConfigureAwait(false);
        
        // Update modification tracking
        Touch();
        
        // Invariants should always be satisfied if business rules were checked properly
#if DEBUG
        var validation = Validate();
        if (validation.IsInvalid)
        {
            throw new InvalidOperationException(
                $"Invariant violation detected after async state change. This indicates incorrect business rule validation. " +
                $"Errors: {string.Join("; ", validation.Errors.Select(e => e.Message))}");
        }
#endif
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    #endregion
    
    #region Lifecycle Events
    
    /// <summary>
    /// Called before the aggregate is persisted
    /// Override to perform pre-save operations
    /// </summary>
    protected virtual void OnBeforeSave()
    {
        // Default implementation does nothing
        // Override in derived classes as needed
    }
    
    /// <summary>
    /// Called after the aggregate is persisted
    /// Override to perform post-save operations
    /// </summary>
    protected virtual void OnAfterSave()
    {
        // Default implementation does nothing
        // Override in derived classes as needed
    }
    
    #endregion
}

/// <summary>
/// Interface for aggregate roots
/// </summary>
public interface IAggregateRoot<TId> : IEntity<TId>
    where TId : IStrongId
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
    Validation<Unit> Validate();
    IReadOnlyList<IBusinessRule> GetBrokenRules();
}
