using System.Linq.Expressions;

namespace BuildingBlocks.Core.Domain.Specifications;

/// <summary>
/// Base class for specifications following the specification pattern.
/// Encapsulates query logic that can be composed and reused with LINQ integration.
/// Supports both in-memory and database query scenarios.
/// </summary>
/// <typeparam name="T">The type of entity this specification applies to</typeparam>
public abstract class Specification<T>
{
    /// <summary>
    /// Convert specification to expression for LINQ queries.
    /// This is the core method that must be implemented by derived classes.
    /// </summary>
    /// <returns>Expression that can be used in LINQ queries</returns>
    public abstract Expression<Func<T, bool>> ToExpression();
    
    /// <summary>
    /// Check if an entity satisfies the specification using in-memory evaluation.
    /// Use this for single entity checks or when working with in-memory collections.
    /// </summary>
    /// <param name="entity">The entity to check</param>
    /// <returns>True if the entity satisfies the specification</returns>
    public bool IsSatisfiedBy(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        var predicate = ToExpression().Compile();
        return predicate(entity);
    }
    
    /// <summary>
    /// Combine with another specification using AND logic.
    /// Both specifications must be satisfied.
    /// </summary>
    /// <param name="specification">The specification to combine with</param>
    /// <returns>A new specification representing the AND combination</returns>
    public Specification<T> And(Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return new AndSpecification<T>(this, specification);
    }
    
    /// <summary>
    /// Combine with another specification using OR logic.
    /// Either specification can be satisfied.
    /// </summary>
    /// <param name="specification">The specification to combine with</param>
    /// <returns>A new specification representing the OR combination</returns>
    public Specification<T> Or(Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return new OrSpecification<T>(this, specification);
    }
    
    /// <summary>
    /// Create a negated version of this specification.
    /// The result will be satisfied when this specification is NOT satisfied.
    /// </summary>
    /// <returns>A new specification representing the negation</returns>
    public Specification<T> Not()
    {
        return new NotSpecification<T>(this);
    }
    
    /// <summary>
    /// Implicit conversion to Expression for seamless LINQ integration.
    /// Allows specifications to be used directly in LINQ queries.
    /// </summary>
    /// <param name="specification">The specification to convert</param>
    public static implicit operator Expression<Func<T, bool>>(Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return specification.ToExpression();
    }
    
    /// <summary>
    /// Compile the specification to a predicate function for in-memory use.
    /// Useful for performance when the same specification will be used multiple times.
    /// </summary>
    /// <returns>A compiled predicate function</returns>
    public Func<T, bool> Compile()
    {
        return ToExpression().Compile();
    }
}

/// <summary>
/// AND specification combinator - both specifications must be satisfied.
/// </summary>
/// <typeparam name="T">The type of entity</typeparam>
internal sealed class AndSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;
    
    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();
        
        // Use expression parameter rewriter for proper composition
        var parameter = Expression.Parameter(typeof(T), "x");
        var leftBody = new ParameterRewriter(leftExpression.Parameters[0], parameter).Visit(leftExpression.Body);
        var rightBody = new ParameterRewriter(rightExpression.Parameters[0], parameter).Visit(rightExpression.Body);
        
        var body = Expression.AndAlso(leftBody!, rightBody!);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

/// <summary>
/// OR specification combinator - either specification can be satisfied.
/// </summary>
/// <typeparam name="T">The type of entity</typeparam>
internal sealed class OrSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;
    
    public OrSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();
        
        // Use expression parameter rewriter for proper composition
        var parameter = Expression.Parameter(typeof(T), "x");
        var leftBody = new ParameterRewriter(leftExpression.Parameters[0], parameter).Visit(leftExpression.Body);
        var rightBody = new ParameterRewriter(rightExpression.Parameters[0], parameter).Visit(rightExpression.Body);
        
        var body = Expression.OrElse(leftBody!, rightBody!);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

/// <summary>
/// NOT specification combinator - negates the wrapped specification.
/// </summary>
/// <typeparam name="T">The type of entity</typeparam>
internal sealed class NotSpecification<T> : Specification<T>
{
    private readonly Specification<T> _specification;
    
    public NotSpecification(Specification<T> specification)
    {
        _specification = specification ?? throw new ArgumentNullException(nameof(specification));
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = _specification.ToExpression();
        
        var parameter = Expression.Parameter(typeof(T), "x");
        var body = new ParameterRewriter(expression.Parameters[0], parameter).Visit(expression.Body);
        var negatedBody = Expression.Not(body!);
        
        return Expression.Lambda<Func<T, bool>>(negatedBody, parameter);
    }
}

/// <summary>
/// Identity specification - always returns true.
/// Useful as a neutral element in specification composition.
/// </summary>
/// <typeparam name="T">The type of entity</typeparam>
public sealed class TrueSpecification<T> : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return _ => true;
    }
}

/// <summary>
/// False specification - always returns false.
/// Useful for creating empty result sets or as a base for OR combinations.
/// </summary>
/// <typeparam name="T">The type of entity</typeparam>
public sealed class FalseSpecification<T> : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return _ => false;
    }
}

/// <summary>
/// Expression parameter rewriter for proper expression tree composition.
/// This ensures that when combining expressions, parameter references are correctly updated.
/// </summary>
internal sealed class ParameterRewriter : ExpressionVisitor
{
    private readonly ParameterExpression _oldParameter;
    private readonly ParameterExpression _newParameter;

    public ParameterRewriter(ParameterExpression oldParameter, ParameterExpression newParameter)
    {
        _oldParameter = oldParameter;
        _newParameter = newParameter;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node == _oldParameter ? _newParameter : base.VisitParameter(node);
    }
}