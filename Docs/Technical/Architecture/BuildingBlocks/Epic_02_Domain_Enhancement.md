# 🏛️ Epic 2: Domain Enhancement - Rich Domain Models Implementation

**Version:** 1.0 - Tactical DDD without Event Sourcing  
**Scope:** Domain layer enhancement for BuildingBlocks/Core  
**Approach:** Rich aggregates, business rules, specifications  
**Target:** .NET 10, Production-ready domain modeling

---

## 📋 Executive Summary

This epic transforms the domain layer from anemic models to **rich domain models** following tactical DDD patterns:

- ✅ **Rich Aggregate Roots** - Encapsulated business logic without event sourcing
- ✅ **Value Objects** - Immutable domain primitives with validation
- ✅ **Business Rules Engine** - Explicit, testable business rules
- ✅ **Specification Pattern** - Composable query logic
- ✅ **Domain Services** - Complex domain operations
- ✅ **Domain Events** - Integration events (no event sourcing in MVP)

---

## 🎯 Target Architecture

### Enhanced Domain Structure
```
Core/Domain/
├── Primitives/              # Base domain types
│   ├── Entity.cs           # Base entity class
│   ├── ValueObject.cs      # Value object base
│   ├── AggregateRoot.cs    # Aggregate root base
│   └── IAuditable.cs       # Audit interfaces
├── Model/                  # Rich domain models
│   ├── AggregateRoot.cs    # Enhanced aggregate base
│   ├── Entity.cs           # Enhanced entity base
│   └── ValueObject.cs      # Enhanced value object base
├── Events/                 # Domain events (no event sourcing)
│   ├── IDomainEvent.cs     # Domain event interface
│   └── DomainEvent.cs      # Base domain event
├── Rules/                  # Business rule engine
│   ├── IBusinessRule.cs    # Business rule interface
│   ├── BusinessRule.cs     # Base business rule
│   └── RuleBuilder.cs      # Fluent rule builder
├── Specifications/         # Specification pattern
│   ├── Specification.cs    # Base specification
│   └── CommonSpecs/        # Common specifications
└── Services/               # Domain service interfaces
    └── IDomainService.cs   # Domain service marker
```

---

## 🔧 Implementation Details

### 2.1 Enhanced Entity Base Class

**File:** `Core/Domain/Primitives/Entity.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Base class for all domain entities.
/// Provides identity, audit trail, and soft delete capabilities.
/// </summary>
public abstract class Entity<TId> : IEntity<TId>, IEquatable<Entity<TId>>
    where TId : IStrongId
{
    protected Entity(TId id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version = 1;
    }
    
    protected Entity()
    {
        // For ORM frameworks that require parameterless constructor
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version = 1;
    }
    
    /// <summary>
    /// Entity identifier
    /// </summary>
    public TId Id { get; protected set; } = default!;
    
    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; protected set; }
    
    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; protected set; }
    
    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; protected set; }
    
    /// <summary>
    /// Soft delete timestamp
    /// </summary>
    public DateTime? DeletedAt { get; protected set; }
    
    /// <summary>
    /// Version for optimistic locking
    /// </summary>
    public uint Version { get; protected set; }
    
    #region Identity and Equality
    
    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        
        return Id.Equals(other.Id);
    }
    
    public override bool Equals(object? obj)
    {
        return Equals(obj as Entity<TId>);
    }
    
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
    
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return Equals(left, right);
    }
    
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !Equals(left, right);
    }
    
    #endregion
    
    #region State Management
    
    /// <summary>
    /// Update the entity's modification timestamp
    /// </summary>
    protected void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }
    
    /// <summary>
    /// Mark entity as deleted (soft delete)
    /// </summary>
    protected virtual void MarkAsDeleted()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        Touch();
    }
    
    /// <summary>
    /// Restore a soft-deleted entity
    /// </summary>
    protected virtual void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        Touch();
    }
    
    #endregion
}

/// <summary>
/// Interface for entities with strongly-typed identifiers
/// </summary>
public interface IEntity<TId> : IEntity
    where TId : IStrongId
{
    TId Id { get; }
}

/// <summary>
/// Non-generic entity interface
/// </summary>
public interface IEntity : IAuditable
{
    uint Version { get; }
    bool IsDeleted { get; }
    DateTime? DeletedAt { get; }
}

/// <summary>
/// Interface for auditable entities
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; }
    DateTime UpdatedAt { get; }
}
```

### 2.2 Rich Aggregate Root Implementation

**File:** `Core/Domain/Model/AggregateRoot.cs`

```csharp
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
    /// Ensure all invariants are satisfied
    /// </summary>
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
    /// Apply state changes with invariant checking
    /// </summary>
    protected Result<Unit> ApplyChange(Action stateChange)
    {
        ArgumentNullException.ThrowIfNull(stateChange);
        
        // Apply the change
        stateChange();
        
        // Check invariants
        var invariantResult = EnsureInvariants();
        if (invariantResult.IsFailure)
        {
            // Invariants failed - this should not happen if business rules are correct
            throw new DomainException("Invariant violation after state change", invariantResult.Error);
        }
        
        // Update modification tracking
        Touch();
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Apply async state changes with invariant checking
    /// </summary>
    protected async Task<Result<Unit>> ApplyChangeAsync(Func<Task> stateChangeAsync)
    {
        ArgumentNullException.ThrowIfNull(stateChangeAsync);
        
        // Apply the change
        await stateChangeAsync().ConfigureAwait(false);
        
        // Check invariants
        var invariantResult = EnsureInvariants();
        if (invariantResult.IsFailure)
        {
            throw new DomainException("Invariant violation after async state change", invariantResult.Error);
        }
        
        // Update modification tracking
        Touch();
        
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
```

### 2.3 Enhanced Value Object Implementation

**File:** `Core/Domain/Primitives/ValueObject.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Base class for value objects following DDD principles.
/// Immutable and compared by value equality.
/// All value objects must implement validation.
/// </summary>
public abstract record ValueObject
{
    /// <summary>
    /// Get components for equality comparison
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();
    
    /// <summary>
    /// Validate the value object state
    /// All value objects must be valid upon creation
    /// </summary>
    public abstract Validation<Unit> Validate();
    
    /// <summary>
    /// Check if the value object is valid
    /// </summary>
    public bool IsValid => Validate().IsValid;
    
    /// <summary>
    /// Get validation errors
    /// </summary>
    public IReadOnlyList<Error> ValidationErrors => Validate().Errors;
    
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }
    
    public virtual bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
            return false;
            
        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }
    
    /// <summary>
    /// Ensure the value object is valid after creation
    /// </summary>
    protected void EnsureValid()
    {
        var validation = Validate();
        if (validation.IsInvalid)
        {
            var message = string.Join("; ", validation.Errors.Select(e => e.Message));
            throw new DomainException($"Invalid {GetType().Name}: {message}");
        }
    }
}

/// <summary>
/// Base class for single-value value objects
/// </summary>
public abstract record SingleValueObject<T> : ValueObject
{
    public T Value { get; }
    
    protected SingleValueObject(T value)
    {
        Value = value;
        EnsureValid(); // Validate immediately upon creation
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
    
    public override string ToString() => Value?.ToString() ?? string.Empty;
    
    public static implicit operator T(SingleValueObject<T> valueObject)
    {
        return valueObject.Value;
    }
}

/// <summary>
/// Example: Email value object with comprehensive validation
/// </summary>
public sealed record Email : SingleValueObject<string>
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
    public const int MaxLength = 254; // RFC 5321 maximum
    
    private Email(string value) : base(value.ToLowerInvariant())
    {
    }
    
    /// <summary>
    /// Create an email with validation
    /// </summary>
    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<Email>.Failure(Error.Validation("Email cannot be empty", "EMAIL_EMPTY"));
            
        value = value.Trim().ToLowerInvariant();
        
        if (value.Length > MaxLength)
            return Result<Email>.Failure(Error.Validation($"Email cannot exceed {MaxLength} characters", "EMAIL_TOO_LONG"));
            
        if (!EmailRegex.IsMatch(value))
            return Result<Email>.Failure(Error.Validation("Invalid email format", "EMAIL_INVALID_FORMAT"));
            
        return Result<Email>.Success(new Email(value));
    }
    
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        if (string.IsNullOrWhiteSpace(Value))
            errors.Add(Error.Validation("Email cannot be empty", "EMAIL_EMPTY"));
        else
        {
            if (Value.Length > MaxLength)
                errors.Add(Error.Validation($"Email cannot exceed {MaxLength} characters", "EMAIL_TOO_LONG"));
                
            if (!EmailRegex.IsMatch(Value))
                errors.Add(Error.Validation("Invalid email format", "EMAIL_INVALID_FORMAT"));
        }
        
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
    
    /// <summary>
    /// Get the domain part of the email
    /// </summary>
    public string Domain => Value.Split('@')[1];
    
    /// <summary>
    /// Get the local part of the email
    /// </summary>
    public string LocalPart => Value.Split('@')[0];
    
    /// <summary>
    /// Check if email is from a specific domain
    /// </summary>
    public bool IsFromDomain(string domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return Domain.Equals(domain, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Example: Money value object with currency support
/// </summary>
public sealed record Money : ValueObject
{
    public decimal Amount { get; }
    public Currency Currency { get; }
    
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
        EnsureValid();
    }
    
    /// <summary>
    /// Create money with validation
    /// </summary>
    public static Result<Money> Create(decimal amount, Currency currency)
    {
        if (amount < 0)
            return Result<Money>.Failure(Error.Validation("Money amount cannot be negative", "MONEY_NEGATIVE"));
            
        if (currency == Currency.None)
            return Result<Money>.Failure(Error.Validation("Currency must be specified", "MONEY_NO_CURRENCY"));
            
        return Result<Money>.Success(new Money(amount, currency));
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
    
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        if (Amount < 0)
            errors.Add(Error.Validation("Money amount cannot be negative", "MONEY_NEGATIVE"));
            
        if (Currency == Currency.None)
            errors.Add(Error.Validation("Currency must be specified", "MONEY_NO_CURRENCY"));
        
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
    
    /// <summary>
    /// Add money (same currency only)
    /// </summary>
    public Result<Money> Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        
        if (Currency != other.Currency)
            return Result<Money>.Failure(Error.BusinessRule("Cannot add money with different currencies", "MONEY_CURRENCY_MISMATCH"));
            
        return Create(Amount + other.Amount, Currency);
    }
    
    /// <summary>
    /// Subtract money (same currency only)
    /// </summary>
    public Result<Money> Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        
        if (Currency != other.Currency)
            return Result<Money>.Failure(Error.BusinessRule("Cannot subtract money with different currencies", "MONEY_CURRENCY_MISMATCH"));
            
        var newAmount = Amount - other.Amount;
        if (newAmount < 0)
            return Result<Money>.Failure(Error.BusinessRule("Resulting amount cannot be negative", "MONEY_NEGATIVE_RESULT"));
            
        return Create(newAmount, Currency);
    }
    
    /// <summary>
    /// Multiply by scalar
    /// </summary>
    public Result<Money> Multiply(decimal multiplier)
    {
        if (multiplier < 0)
            return Result<Money>.Failure(Error.BusinessRule("Money multiplier cannot be negative", "MONEY_NEGATIVE_MULTIPLIER"));
            
        return Create(Amount * multiplier, Currency);
    }
    
    public override string ToString() => $"{Amount:F2} {Currency}";
}

/// <summary>
/// Currency enumeration
/// </summary>
public enum Currency
{
    None = 0,
    USD = 1,
    EUR = 2,
    GBP = 3,
    CAD = 4,
    AUD = 5
}
```

### 2.4 Business Rules Engine

**File:** `Core/Domain/Rules/IBusinessRule.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Represents a business rule that can be broken
/// Business rules encapsulate domain logic and constraints
/// </summary>
public interface IBusinessRule
{
    /// <summary>
    /// Unique identifier for the rule
    /// </summary>
    string Code { get; }
    
    /// <summary>
    /// Human-readable description of the rule
    /// </summary>
    string Message { get; }
    
    /// <summary>
    /// Check if the rule is currently broken
    /// </summary>
    bool IsBroken();
}
```

**File:** `Core/Domain/Rules/BusinessRule.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Base implementation for business rules
/// </summary>
public abstract record BusinessRule : IBusinessRule
{
    public abstract string Code { get; }
    public abstract string Message { get; }
    public abstract bool IsBroken();
    
    /// <summary>
    /// Convert to Result
    /// </summary>
    public Result<Unit> ToResult()
    {
        return IsBroken() 
            ? Result<Unit>.Failure(Error.BusinessRule(Message, Code))
            : Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Convert to Validation
    /// </summary>
    public Validation<Unit> ToValidation()
    {
        return IsBroken()
            ? Validation<Unit>.Invalid(Error.BusinessRule(Message, Code))
            : Validation<Unit>.Valid(Unit.Value);
    }
}

/// <summary>
/// Simple predicate-based business rule
/// </summary>
public sealed record PredicateRule : BusinessRule
{
    private readonly Func<bool> _predicate;
    
    public PredicateRule(string code, string message, Func<bool> predicate)
    {
        Code = code;
        Message = message;
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }
    
    public override string Code { get; }
    public override string Message { get; }
    
    public override bool IsBroken() => _predicate();
}

/// <summary>
/// Composite rule for combining multiple rules
/// </summary>
public sealed record CompositeRule : BusinessRule
{
    private readonly IBusinessRule[] _rules;
    private readonly string _code;
    private readonly string _message;
    private readonly CompositeRuleMode _mode;
    
    public CompositeRule(
        string code, 
        string message, 
        CompositeRuleMode mode,
        params IBusinessRule[] rules)
    {
        _code = code;
        _message = message;
        _mode = mode;
        _rules = rules ?? Array.Empty<IBusinessRule>();
    }
    
    public override string Code => _code;
    public override string Message => _message;
    
    public override bool IsBroken()
    {
        return _mode switch
        {
            CompositeRuleMode.All => _rules.All(r => r.IsBroken()),
            CompositeRuleMode.Any => _rules.Any(r => r.IsBroken()),
            CompositeRuleMode.None => !_rules.Any(r => r.IsBroken()),
            _ => throw new InvalidOperationException($"Unsupported composite rule mode: {_mode}")
        };
    }
    
    /// <summary>
    /// Get all broken rules
    /// </summary>
    public IEnumerable<IBusinessRule> GetBrokenRules()
    {
        return _rules.Where(r => r.IsBroken());
    }
    
    /// <summary>
    /// Get all rules with their status
    /// </summary>
    public IEnumerable<(IBusinessRule Rule, bool IsBroken)> GetRulesWithStatus()
    {
        return _rules.Select(r => (r, r.IsBroken()));
    }
}

/// <summary>
/// Composite rule evaluation mode
/// </summary>
public enum CompositeRuleMode
{
    /// <summary>
    /// All sub-rules must be broken for composite to be broken
    /// </summary>
    All,
    
    /// <summary>
    /// Any sub-rule broken makes composite broken
    /// </summary>
    Any,
    
    /// <summary>
    /// No sub-rules should be broken (inverse of Any)
    /// </summary>
    None
}
```

**File:** `Core/Domain/Rules/RuleBuilder.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Fluent builder for creating and composing business rules
/// </summary>
public class RuleBuilder
{
    private readonly List<IBusinessRule> _rules = new();
    
    /// <summary>
    /// Add a rule that must be true (condition must be true, or rule is broken)
    /// </summary>
    public RuleBuilder Must(bool condition, string code, string message)
    {
        _rules.Add(new PredicateRule(code, message, () => !condition));
        return this;
    }
    
    /// <summary>
    /// Add a rule with predicate that must be true
    /// </summary>
    public RuleBuilder Must(Func<bool> predicate, string code, string message)
    {
        _rules.Add(new PredicateRule(code, message, () => !predicate()));
        return this;
    }
    
    /// <summary>
    /// Add a rule that must not be true (condition must be false, or rule is broken)
    /// </summary>
    public RuleBuilder MustNot(bool condition, string code, string message)
    {
        _rules.Add(new PredicateRule(code, message, () => condition));
        return this;
    }
    
    /// <summary>
    /// Add a rule with predicate that must not be true
    /// </summary>
    public RuleBuilder MustNot(Func<bool> predicate, string code, string message)
    {
        _rules.Add(new PredicateRule(code, message, predicate));
        return this;
    }
    
    /// <summary>
    /// Add an existing business rule
    /// </summary>
    public RuleBuilder AddRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
        return this;
    }
    
    /// <summary>
    /// Add multiple existing business rules
    /// </summary>
    public RuleBuilder AddRules(params IBusinessRule[] rules)
    {
        if (rules != null)
        {
            _rules.AddRange(rules);
        }
        return this;
    }
    
    /// <summary>
    /// Build and evaluate rules, returning Result
    /// </summary>
    public Result<Unit> Build()
    {
        var brokenRules = _rules.Where(r => r.IsBroken()).ToList();
        
        if (brokenRules.Any())
        {
            var errors = brokenRules.Select(r => 
                Error.BusinessRule(r.Message, r.Code)).ToArray();
            return Result<Unit>.Failure(Error.Aggregate(errors));
        }
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Build and evaluate rules, returning Validation (accumulates all errors)
    /// </summary>
    public Validation<Unit> BuildValidation()
    {
        var brokenRules = _rules.Where(r => r.IsBroken()).ToList();
        
        if (brokenRules.Any())
        {
            var errors = brokenRules.Select(r => 
                Error.BusinessRule(r.Message, r.Code)).ToArray();
            return Validation<Unit>.Invalid(errors);
        }
        
        return Validation<Unit>.Valid(Unit.Value);
    }
    
    /// <summary>
    /// Get all rules (for inspection/debugging)
    /// </summary>
    public IReadOnlyList<IBusinessRule> GetRules() => _rules.AsReadOnly();
    
    /// <summary>
    /// Clear all rules
    /// </summary>
    public RuleBuilder Clear()
    {
        _rules.Clear();
        return this;
    }
}

/// <summary>
/// Extension methods for fluent rule composition
/// </summary>
public static class RuleBuilderExtensions
{
    /// <summary>
    /// Create a new rule builder
    /// </summary>
    public static RuleBuilder Rules() => new();
    
    /// <summary>
    /// Create a rule builder with initial rule
    /// </summary>
    public static RuleBuilder Rules(IBusinessRule initialRule)
    {
        return new RuleBuilder().AddRule(initialRule);
    }
    
    /// <summary>
    /// Validate that a value is not null
    /// </summary>
    public static RuleBuilder NotNull<T>(this RuleBuilder builder, T? value, string propertyName)
    {
        return builder.Must(
            value != null, 
            $"{propertyName.ToUpperInvariant()}_NULL", 
            $"{propertyName} cannot be null");
    }
    
    /// <summary>
    /// Validate that a string is not empty
    /// </summary>
    public static RuleBuilder NotEmpty(this RuleBuilder builder, string? value, string propertyName)
    {
        return builder.Must(
            !string.IsNullOrWhiteSpace(value), 
            $"{propertyName.ToUpperInvariant()}_EMPTY", 
            $"{propertyName} cannot be empty");
    }
    
    /// <summary>
    /// Validate that a collection is not empty
    /// </summary>
    public static RuleBuilder NotEmpty<T>(this RuleBuilder builder, IEnumerable<T>? collection, string propertyName)
    {
        return builder.Must(
            collection?.Any() == true, 
            $"{propertyName.ToUpperInvariant()}_EMPTY", 
            $"{propertyName} cannot be empty");
    }
    
    /// <summary>
    /// Validate numeric range
    /// </summary>
    public static RuleBuilder InRange<T>(this RuleBuilder builder, T value, T min, T max, string propertyName)
        where T : IComparable<T>
    {
        return builder.Must(
            value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0,
            $"{propertyName.ToUpperInvariant()}_OUT_OF_RANGE",
            $"{propertyName} must be between {min} and {max}");
    }
    
    /// <summary>
    /// Validate string length
    /// </summary>
    public static RuleBuilder MaxLength(this RuleBuilder builder, string? value, int maxLength, string propertyName)
    {
        return builder.Must(
            value?.Length <= maxLength,
            $"{propertyName.ToUpperInvariant()}_TOO_LONG",
            $"{propertyName} cannot exceed {maxLength} characters");
    }
}
```

### 2.5 Specification Pattern Implementation

**File:** `Core/Domain/Specifications/Specification.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Specifications;

/// <summary>
/// Base class for specifications following the specification pattern
/// Encapsulates query logic that can be composed and reused
/// </summary>
public abstract class Specification<T>
{
    /// <summary>
    /// Convert specification to expression for LINQ queries
    /// </summary>
    public abstract Expression<Func<T, bool>> ToExpression();
    
    /// <summary>
    /// Check if entity satisfies the specification
    /// </summary>
    public bool IsSatisfiedBy(T entity)
    {
        var predicate = ToExpression().Compile();
        return predicate(entity);
    }
    
    /// <summary>
    /// Combine with another specification using AND
    /// </summary>
    public Specification<T> And(Specification<T> specification)
    {
        return new AndSpecification<T>(this, specification);
    }
    
    /// <summary>
    /// Combine with another specification using OR
    /// </summary>
    public Specification<T> Or(Specification<T> specification)
    {
        return new OrSpecification<T>(this, specification);
    }
    
    /// <summary>
    /// Negate the specification
    /// </summary>
    public Specification<T> Not()
    {
        return new NotSpecification<T>(this);
    }
    
    /// <summary>
    /// Implicit conversion to Expression
    /// </summary>
    public static implicit operator Expression<Func<T, bool>>(Specification<T> specification)
    {
        return specification.ToExpression();
    }
}

/// <summary>
/// AND specification combinator
/// </summary>
internal sealed class AndSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;
    
    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();
        
        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.AndAlso(
            Expression.Invoke(leftExpression, parameter),
            Expression.Invoke(rightExpression, parameter));
            
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

/// <summary>
/// OR specification combinator
/// </summary>
internal sealed class OrSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;
    
    public OrSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();
        
        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.OrElse(
            Expression.Invoke(leftExpression, parameter),
            Expression.Invoke(rightExpression, parameter));
            
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

/// <summary>
/// NOT specification combinator
/// </summary>
internal sealed class NotSpecification<T> : Specification<T>
{
    private readonly Specification<T> _specification;
    
    public NotSpecification(Specification<T> specification)
    {
        _specification = specification;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = _specification.ToExpression();
        
        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.Not(Expression.Invoke(expression, parameter));
        
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

/// <summary>
/// Identity specification (always true)
/// </summary>
public sealed class TrueSpecification<T> : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return _ => true;
    }
}

/// <summary>
/// False specification (always false)
/// </summary>
public sealed class FalseSpecification<T> : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return _ => false;
    }
}
```

**File:** `Core/Domain/Specifications/CommonSpecs/CommonSpecifications.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Specifications.CommonSpecs;

/// <summary>
/// Common specifications for entities
/// </summary>
public static class CommonSpecifications
{
    /// <summary>
    /// Specification for active (non-deleted) entities
    /// </summary>
    public static Specification<T> Active<T>() where T : IEntity
    {
        return new ActiveSpecification<T>();
    }
    
    /// <summary>
    /// Specification for entities created within a date range
    /// </summary>
    public static Specification<T> CreatedBetween<T>(DateTime from, DateTime to) where T : IAuditable
    {
        return new CreatedBetweenSpecification<T>(from, to);
    }
    
    /// <summary>
    /// Specification for entities created after a specific date
    /// </summary>
    public static Specification<T> CreatedAfter<T>(DateTime date) where T : IAuditable
    {
        return new CreatedAfterSpecification<T>(date);
    }
    
    /// <summary>
    /// Specification for entities created before a specific date
    /// </summary>
    public static Specification<T> CreatedBefore<T>(DateTime date) where T : IAuditable
    {
        return new CreatedBeforeSpecification<T>(date);
    }
}

/// <summary>
/// Specification for active entities
/// </summary>
public sealed class ActiveSpecification<T> : Specification<T>
    where T : IEntity
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => !entity.IsDeleted;
    }
}

/// <summary>
/// Specification for entities created within a date range
/// </summary>
public sealed class CreatedBetweenSpecification<T> : Specification<T>
    where T : IAuditable
{
    private readonly DateTime _from;
    private readonly DateTime _to;
    
    public CreatedBetweenSpecification(DateTime from, DateTime to)
    {
        _from = from;
        _to = to;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => entity.CreatedAt >= _from && entity.CreatedAt <= _to;
    }
}

/// <summary>
/// Specification for entities created after a specific date
/// </summary>
public sealed class CreatedAfterSpecification<T> : Specification<T>
    where T : IAuditable
{
    private readonly DateTime _date;
    
    public CreatedAfterSpecification(DateTime date)
    {
        _date = date;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => entity.CreatedAt > _date;
    }
}

/// <summary>
/// Specification for entities created before a specific date
/// </summary>
public sealed class CreatedBeforeSpecification<T> : Specification<T>
    where T : IAuditable
{
    private readonly DateTime _date;
    
    public CreatedBeforeSpecification(DateTime date)
    {
        _date = date;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => entity.CreatedAt < _date;
    }
}
```

### 2.6 Domain Events Implementation

**File:** `Core/Domain/Events/IDomainEvent.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Marker interface for domain events
/// Domain events represent something important that happened in the domain
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Unique identifier for this domain event instance
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// When the event occurred
    /// </summary>
    DateTime OccurredAt { get; }
    
    /// <summary>
    /// Version of the event schema (for evolution)
    /// </summary>
    int Version { get; }
}
```

**File:** `Core/Domain/Events/DomainEvent.cs`

```csharp
namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Base implementation for domain events
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
        Version = 1;
    }
    
    protected DomainEvent(Guid eventId, DateTime occurredAt, int version = 1)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        Version = version;
    }
    
    public Guid EventId { get; }
    public DateTime OccurredAt { get; }
    public int Version { get; }
}

/// <summary>
/// Example domain event: User registered
/// </summary>
public sealed record UserRegisteredEvent : DomainEvent
{
    public UserId UserId { get; }
    public Email Email { get; }
    public DateTime RegisteredAt { get; }
    
    public UserRegisteredEvent(UserId userId, Email email)
    {
        UserId = userId;
        Email = email;
        RegisteredAt = OccurredAt;
    }
    
    private UserRegisteredEvent(
        Guid eventId, 
        DateTime occurredAt, 
        int version,
        UserId userId, 
        Email email, 
        DateTime registeredAt) : base(eventId, occurredAt, version)
    {
        UserId = userId;
        Email = email;
        RegisteredAt = registeredAt;
    }
}

/// <summary>
/// Example domain event: Order placed
/// </summary>
public sealed record OrderPlacedEvent : DomainEvent
{
    public OrderId OrderId { get; }
    public UserId UserId { get; }
    public Money TotalAmount { get; }
    public int ItemCount { get; }
    
    public OrderPlacedEvent(OrderId orderId, UserId userId, Money totalAmount, int itemCount)
    {
        OrderId = orderId;
        UserId = userId;
        TotalAmount = totalAmount;
        ItemCount = itemCount;
    }
    
    private OrderPlacedEvent(
        Guid eventId,
        DateTime occurredAt,
        int version,
        OrderId orderId,
        UserId userId,
        Money totalAmount,
        int itemCount) : base(eventId, occurredAt, version)
    {
        OrderId = orderId;
        UserId = userId;
        TotalAmount = totalAmount;
        ItemCount = itemCount;
    }
}
```

---

## 📊 Implementation Roadmap

### Week 1: Core Domain Types
- [ ] Enhanced Entity base class
- [ ] Rich AggregateRoot implementation
- [ ] Enhanced ValueObject base class
- [ ] Example value objects (Email, Money)

### Week 2: Business Rules Engine
- [ ] IBusinessRule interface and implementations
- [ ] RuleBuilder with fluent API
- [ ] Composite rules and rule combinations
- [ ] Integration with aggregates

### Week 3: Specifications and Events
- [ ] Specification pattern implementation
- [ ] Common specifications library
- [ ] Domain events (without event sourcing)
- [ ] Complete integration testing

---

## 🎯 Success Criteria

Epic 2 is complete when:

1. ✅ **Rich aggregates** properly encapsulate business logic
2. ✅ **Value objects** provide immutable domain primitives
3. ✅ **Business rules** are explicit and testable
4. ✅ **Specifications** enable composable queries
5. ✅ **Domain events** support integration patterns
6. ✅ **All invariants** are properly protected
7. ✅ **100% test coverage** for domain logic

---

## 🚀 Usage Examples

### Rich Aggregate Example
```csharp
public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderItem> _items = new();
    
    public UserId UserId { get; private set; }
    public Money TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    
    private Order() { } // EF Core
    
    public static Result<Order> Create(UserId userId)
    {
        var order = new Order
        {
            Id = OrderId.New(),
            UserId = userId,
            TotalAmount = Money.Create(0, Currency.USD).Value,
            Status = OrderStatus.Draft
        };
        
        order.RaiseDomainEvent(new OrderCreatedEvent(order.Id, userId));
        return Result<Order>.Success(order);
    }
    
    public Result<Unit> AddItem(ProductId productId, Money unitPrice, int quantity)
    {
        // Check business rules
        var ruleResult = new RuleBuilder()
            .Must(Status == OrderStatus.Draft, "ORDER_NOT_DRAFT", "Cannot modify confirmed order")
            .Must(quantity > 0, "INVALID_QUANTITY", "Quantity must be positive")
            .Must(_items.Count < 50, "TOO_MANY_ITEMS", "Order cannot exceed 50 items")
            .Build();
            
        if (ruleResult.IsFailure)
            return ruleResult;
            
        // Apply state change
        return ApplyChange(() =>
        {
            var item = OrderItem.Create(productId, unitPrice, quantity).Value;
            _items.Add(item);
            RecalculateTotal();
            
            RaiseDomainEvent(new OrderItemAddedEvent(Id, productId, quantity));
        });
    }
    
    protected override IEnumerable<IBusinessRule> GetInvariants()
    {
        yield return new PredicateRule("ORDER_MUST_HAVE_USER", "Order must have a user", 
            () => UserId == null);
        yield return new PredicateRule("ORDER_TOTAL_POSITIVE", "Order total must be positive when confirmed", 
            () => Status == OrderStatus.Confirmed && TotalAmount.Amount <= 0);
    }
    
    private void RecalculateTotal()
    {
        var total = _items.Sum(item => item.TotalPrice.Amount);
        TotalAmount = Money.Create(total, Currency.USD).Value;
    }
}
```

### Business Rules in Action
```csharp
public sealed class UserRegistrationService : IDomainService
{
    public async Task<Result<User>> RegisterUserAsync(
        Email email, 
        UserName userName,
        CancellationToken ct)
    {
        // Validate business rules
        var rules = new RuleBuilder()
            .NotNull(email, nameof(email))
            .NotNull(userName, nameof(userName))
            .Must(await IsEmailUniqueAsync(email, ct), "EMAIL_NOT_UNIQUE", "Email already exists")
            .Must(await IsUserNameUniqueAsync(userName, ct), "USERNAME_NOT_UNIQUE", "Username already taken")
            .Build();
            
        if (rules.IsFailure)
            return Result<User>.Failure(rules.Error);
            
        // Create user
        return User.Create(email, userName);
    }
}
```

### Specifications for Queries
```csharp
public class OrderQueryService
{
    public async Task<List<Order>> GetRecentOrdersAsync(
        UserId userId, 
        int days = 30,
        CancellationToken ct = default)
    {
        var spec = CommonSpecifications.Active<Order>()
            .And(new OrdersByUserSpecification(userId))
            .And(CommonSpecifications.CreatedAfter<Order>(DateTime.UtcNow.AddDays(-days)));
            
        return await _repository.FindAsync(spec, ct);
    }
}

public class OrdersByUserSpecification : Specification<Order>
{
    private readonly UserId _userId;
    
    public OrdersByUserSpecification(UserId userId)
    {
        _userId = userId;
    }
    
    public override Expression<Func<Order, bool>> ToExpression()
    {
        return order => order.UserId == _userId;
    }
}
```

---

**END OF EPIC 2: DOMAIN ENHANCEMENT**