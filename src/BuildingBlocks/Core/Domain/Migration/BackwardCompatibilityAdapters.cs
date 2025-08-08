using BuildingBlocks.Core.Domain.Model;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.Migration;

/// <summary>
/// Backward compatibility adapters for smooth migration from old patterns to Epic 2 Domain Enhancement.
/// These adapters allow existing code to continue working while gradually migrating to new patterns.
/// </summary>

#region Base Aggregate Adapter

/// <summary>
/// Adapter for existing BaseAggregate implementations to work with new AggregateRoot patterns.
/// Provides compatibility layer during migration period.
/// </summary>
/// <typeparam name="TId">The aggregate identifier type</typeparam>
[Obsolete("Use AggregateRoot<TId> directly. This adapter is for migration only.")]
public abstract class BaseAggregateAdapter<TId> : AggregateRoot<TId>
    where TId : IStrongId
{
    /// <summary>
    /// Legacy method for adding domain events - redirects to new pattern.
    /// </summary>
    [Obsolete("Use RaiseDomainEvent instead")]
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        RaiseDomainEvent(domainEvent);
    }

    /// <summary>
    /// Legacy method for clearing events - redirects to new pattern.
    /// </summary>
    [Obsolete("Events are automatically cleared by TransactionBehavior")]
    protected void ClearDomainEvents()
    {
        ClearEvents();
    }

    /// <summary>
    /// Legacy method for getting events - redirects to new pattern.
    /// </summary>
    [Obsolete("Use GetUncommittedEvents instead")]
    protected IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        return GetUncommittedEvents();
    }
}

#endregion

#region Entity Adapter

/// <summary>
/// Adapter for existing BaseEntity implementations to work with new Entity patterns.
/// </summary>
/// <typeparam name="TId">The entity identifier type</typeparam>
[Obsolete("Use Entity<TId> directly. This adapter is for migration only.")]
public abstract class BaseEntityAdapter<TId> : Entity<TId>
    where TId : IStrongId
{
    // Base entity functionality is already compatible
    // This adapter exists for consistent naming during migration
}

/// <summary>
/// Adapter for existing auditable entities.
/// </summary>
/// <typeparam name="TId">The entity identifier type</typeparam>
[Obsolete("Use Entity<TId> with IAuditable trait. This adapter is for migration only.")]
public abstract class BaseAuditableEntityAdapter<TId> : Entity<TId>
    where TId : IStrongId
{
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }
    public string? CreatedBy { get; protected set; }
    public string? UpdatedBy { get; protected set; }

    protected void MarkAsUpdated(string? updatedBy = null)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}

#endregion

#region Result Pattern Adapter

/// <summary>
/// Extension methods to bridge old Result usage with new Epic 2 patterns.
/// Provides smooth migration path for existing result handling code.
/// </summary>
public static class ResultMigrationExtensions
{
    /// <summary>
    /// Converts old validation errors to new business rule pattern.
    /// </summary>
    [Obsolete("Use RuleBuilder pattern for business rules validation")]
    public static Result<T> ValidateBusinessRules<T>(
        this Result<T> result, 
        params IBusinessRule[] rules) where T : notnull
    {
        if (result.IsFailure)
            return result;

        var brokenRules = rules.Where(rule => rule.IsBroken()).ToArray();
        if (brokenRules.Any())
        {
            var errors = brokenRules.Select(rule => 
                Error.BusinessRule(rule.Message, rule.Code)).ToArray();
            return Result<T>.Failure(Error.Multiple(errors));
        }

        return result;
    }

    /// <summary>
    /// Converts Validation result to old Result pattern for backward compatibility.
    /// </summary>
    [Obsolete("Use Validation pattern directly")]
    public static Result<T> ToLegacyResult<T>(this Validation<T> validation) where T : notnull
    {
        return validation.IsValid 
            ? Result<T>.Success(validation.Value)
            : Result<T>.Failure(Error.Multiple(validation.Errors.ToArray()));
    }

    /// <summary>
    /// Converts old Result pattern to new Validation pattern.
    /// </summary>
    [Obsolete("Use Validation pattern directly")]
    public static Validation<T> ToValidation<T>(this Result<T> result) where T : notnull
    {
        return result.IsSuccess
            ? Validation<T>.Valid(result.Value)
            : Validation<T>.Invalid(result.Error);
    }
}

#endregion

#region Command/Query Adapter

/// <summary>
/// Base class for migrating existing commands to Epic 2 patterns.
/// Provides compatibility layer while transitioning to new command structure.
/// </summary>
[Obsolete("Use CommandBase<TResponse> directly. This adapter is for migration only.")]
public abstract record LegacyCommandAdapter<TResponse> : CommandBase<TResponse>
    where TResponse : notnull
{
    /// <summary>
    /// Legacy validation method - override this in existing commands.
    /// Internally converts to new domain rules pattern.
    /// </summary>
    [Obsolete("Override ValidateDomainRules instead")]
    public virtual Result ValidateLegacy()
    {
        return Result.Success();
    }

    /// <summary>
    /// Implements new validation pattern by delegating to legacy method.
    /// </summary>
    public override Validation<Unit> ValidateDomainRules()
    {
        var legacyResult = ValidateLegacy();
        return legacyResult.IsSuccess 
            ? Validation<Unit>.Valid(Unit.Value)
            : Validation<Unit>.Invalid(legacyResult.Error);
    }

    /// <summary>
    /// Default aggregate type for commands that don't specify one.
    /// </summary>
    public override Type GetAggregateType() => typeof(object);
}

/// <summary>
/// Base class for migrating existing queries to Epic 2 patterns.
/// </summary>
[Obsolete("Use QueryBase<TResponse> directly. This adapter is for migration only.")]
public abstract record LegacyQueryAdapter<TResponse> : QueryBase<TResponse>
    where TResponse : notnull
{
    // Query compatibility is handled at the base level
    // This adapter exists for consistent naming during migration
}

#endregion

#region Business Rules Migration Helper

/// <summary>
/// Helper class for migrating inline validation to business rules pattern.
/// Provides quick conversion methods for common validation scenarios.
/// </summary>
public static class BusinessRuleMigrationHelper
{
    /// <summary>
    /// Converts inline validation to business rule pattern.
    /// </summary>
    [Obsolete("Create explicit business rule classes instead")]
    public static IBusinessRule CreateRule(
        string code, 
        string message, 
        Func<bool> condition)
    {
        return new LegacyPredicateRule(code, message, condition);
    }

    /// <summary>
    /// Creates multiple business rules from validation conditions.
    /// </summary>
    [Obsolete("Use RuleBuilder pattern instead")]
    public static IEnumerable<IBusinessRule> CreateRules(
        params (string code, string message, Func<bool> condition)[] validations)
    {
        return validations.Select(v => CreateRule(v.code, v.message, v.condition));
    }
}

/// <summary>
/// Legacy predicate rule for migration purposes.
/// </summary>
[Obsolete("Create explicit business rule classes")]
internal sealed record LegacyPredicateRule : BusinessRule
{
    private readonly Func<bool> _condition;

    public LegacyPredicateRule(string code, string message, Func<bool> condition)
    {
        Code = code;
        Message = message;
        _condition = condition;
    }

    public override string Code { get; }
    public override string Message { get; }

    public override bool IsBroken() => _condition();
}

#endregion

#region Repository Adapter

/// <summary>
/// Repository adapter for gradual migration to specification pattern.
/// Allows existing repositories to support specifications while maintaining legacy methods.
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TId">The entity identifier type</typeparam>
public abstract class LegacyRepositoryAdapter<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : IStrongId
{
    /// <summary>
    /// Legacy find method - maps to specification pattern.
    /// </summary>
    [Obsolete("Use FindAsync(Specification<TEntity>) instead")]
    public virtual async Task<List<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var specification = new ExpressionSpecification<TEntity>(predicate);
        return await FindAsync(specification, cancellationToken);
    }

    /// <summary>
    /// New specification-based find method.
    /// </summary>
    public abstract Task<List<TEntity>> FindAsync(
        Specification<TEntity> specification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy single result method.
    /// </summary>
    [Obsolete("Use FirstOrDefaultAsync(Specification<TEntity>) instead")]
    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var specification = new ExpressionSpecification<TEntity>(predicate);
        return await FirstOrDefaultAsync(specification, cancellationToken);
    }

    /// <summary>
    /// New specification-based single result method.
    /// </summary>
    public abstract Task<TEntity?> FirstOrDefaultAsync(
        Specification<TEntity> specification,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Temporary specification wrapper for expression predicates.
/// </summary>
[Obsolete("Create explicit specification classes")]
internal sealed class ExpressionSpecification<TEntity> : Specification<TEntity>
{
    private readonly Expression<Func<TEntity, bool>> _expression;

    public ExpressionSpecification(Expression<Func<TEntity, bool>> expression)
    {
        _expression = expression;
    }

    public override Expression<Func<TEntity, bool>> ToExpression() => _expression;
}

#endregion

#region Migration Checklist Helper

/// <summary>
/// Helper class to track migration progress and validate migration completeness.
/// Use this during development to ensure all migration steps are completed.
/// </summary>
public static class MigrationChecklistHelper
{
    private static readonly Dictionary<Type, MigrationStatus> _migrationStatus = new();

    /// <summary>
    /// Marks a type as migrated to Epic 2 patterns.
    /// </summary>
    public static void MarkAsMigrated<T>() where T : class
    {
        _migrationStatus[typeof(T)] = MigrationStatus.Completed;
    }

    /// <summary>
    /// Marks a type as in progress for migration.
    /// </summary>
    public static void MarkAsInProgress<T>() where T : class
    {
        _migrationStatus[typeof(T)] = MigrationStatus.InProgress;
    }

    /// <summary>
    /// Checks if a type has been migrated.
    /// </summary>
    public static bool IsMigrated<T>() where T : class
    {
        return _migrationStatus.TryGetValue(typeof(T), out var status) && 
               status == MigrationStatus.Completed;
    }

    /// <summary>
    /// Gets migration report for all tracked types.
    /// </summary>
    public static Dictionary<string, MigrationStatus> GetMigrationReport()
    {
        return _migrationStatus.ToDictionary(
            kvp => kvp.Key.Name,
            kvp => kvp.Value);
    }

    /// <summary>
    /// Validates that critical types have been migrated.
    /// Throws exception if migration is incomplete.
    /// </summary>
    public static void ValidateCriticalMigration(params Type[] criticalTypes)
    {
        var unmigrated = criticalTypes.Where(type => 
            !_migrationStatus.TryGetValue(type, out var status) || 
            status != MigrationStatus.Completed).ToArray();

        if (unmigrated.Any())
        {
            var typeNames = string.Join(", ", unmigrated.Select(t => t.Name));
            throw new InvalidOperationException(
                $"Critical types not migrated to Epic 2: {typeNames}");
        }
    }
}

/// <summary>
/// Migration status enumeration.
/// </summary>
public enum MigrationStatus
{
    NotStarted,
    InProgress,
    Completed
}

#endregion

/// <summary>
/// Attribute to mark classes as migrated to Epic 2 patterns.
/// Use for documentation and migration tracking purposes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class Epic2MigratedAttribute : Attribute
{
    public string? Notes { get; }
    public DateTime MigratedDate { get; }

    public Epic2MigratedAttribute(string? notes = null)
    {
        Notes = notes;
        MigratedDate = DateTime.UtcNow;
    }
}