using System.Linq.Expressions;

namespace Axon.Shared.Domain;

/// <summary>
/// Base class for implementing the Specification pattern
/// </summary>
/// <typeparam name="T">The type of entity the specification applies to</typeparam>
public abstract class Specification<T> : ISpecification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T entity)
    {
        var predicate = ToExpression().Compile();
        return predicate(entity);
    }

    /// <summary>
    /// Combines two specifications using logical AND
    /// </summary>
    public Specification<T> And(Specification<T> other)
    {
        return new AndSpecification<T>(this, other);
    }

    /// <summary>
    /// Combines two specifications using logical OR
    /// </summary>
    public Specification<T> Or(Specification<T> other)
    {
        return new OrSpecification<T>(this, other);
    }

    /// <summary>
    /// Negates the current specification
    /// </summary>
    public Specification<T> Not()
    {
        return new NotSpecification<T>(this);
    }

    public static implicit operator Expression<Func<T, bool>>(Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return specification.ToExpression();
    }
}

/// <summary>
/// Specification that combines two specifications with logical AND
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
        var leftVisitor = new ReplaceExpressionVisitor(leftExpression.Parameters[0], parameter);
        var rightVisitor = new ReplaceExpressionVisitor(rightExpression.Parameters[0], parameter);

        var left = leftVisitor.Visit(leftExpression.Body);
        var right = rightVisitor.Visit(rightExpression.Body);

        return Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(left!, right!), parameter);
    }
}

/// <summary>
/// Specification that combines two specifications with logical OR
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
        var leftVisitor = new ReplaceExpressionVisitor(leftExpression.Parameters[0], parameter);
        var rightVisitor = new ReplaceExpressionVisitor(rightExpression.Parameters[0], parameter);

        var left = leftVisitor.Visit(leftExpression.Body);
        var right = rightVisitor.Visit(rightExpression.Body);

        return Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(left!, right!), parameter);
    }
}

/// <summary>
/// Specification that negates another specification
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
        var negated = Expression.Not(expression.Body);
        return Expression.Lambda<Func<T, bool>>(negated, expression.Parameters);
    }
}

/// <summary>
/// Expression visitor for replacing parameter expressions
/// </summary>
internal sealed class ReplaceExpressionVisitor : ExpressionVisitor
{
    private readonly Expression _oldValue;
    private readonly Expression _newValue;

    public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
    {
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public override Expression? Visit(Expression? node)
    {
        return node == _oldValue ? _newValue : base.Visit(node);
    }
}