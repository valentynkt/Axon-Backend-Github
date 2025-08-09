using System.Linq.Expressions;

namespace BuildingBlocks.Core.Domain.Specifications;

/// <summary>Minimal, composable specification (expression-based, infra-agnostic).</summary>
public interface ISpecification<T>
{
    Expression<Func<T, bool>> ToExpression();

    bool IsSatisfiedBy(T candidate)
        => ToExpression().Compile().Invoke(candidate);
}

/// <summary>
/// Base class enabling rich combinators and operator syntax. Caches compiled predicate per instance.
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    private Func<T, bool>? _cachedPredicate;

    public abstract Expression<Func<T, bool>> ToExpression();

    public static Specification<T> Create(Expression<Func<T, bool>> predicate)
        => new AdHocSpecification<T>(predicate);

    public Specification<T> And(ISpecification<T> other) => new AndSpecification<T>(this, other);
    public Specification<T> Or(ISpecification<T> other)  => new OrSpecification<T>(this, other);
    public Specification<T> Not()                        => new NotSpecification<T>(this);

    /// <summary>Compile once, reuse many times (LINQ-to-Objects path).</summary>
    public Func<T, bool> ToPredicate()
        => _cachedPredicate ??= ToExpression().Compile();

    public bool IsSatisfiedBy(T candidate) => ToPredicate().Invoke(candidate);

    /// <summary>Implicit conversion to Expression for seamless LINQ integration.</summary>
    public static implicit operator Expression<Func<T, bool>>(Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return specification.ToExpression();
    }

    // Operator sugar: specA & specB, specA | specB, !specA
    public static Specification<T> operator &(Specification<T> left, ISpecification<T> right) => left.And(right);
    public static Specification<T> operator |(Specification<T> left, ISpecification<T> right) => left.Or(right);
    public static Specification<T> operator !(Specification<T> inner) => inner.Not();

    private sealed class AdHocSpecification<TC> : Specification<TC>
    {
        private readonly Expression<Func<TC, bool>> _expr;
        public AdHocSpecification(Expression<Func<TC, bool>> expr) => _expr = expr ?? throw new ArgumentNullException(nameof(expr));
        public override Expression<Func<TC, bool>> ToExpression() => _expr;
    }
}

internal sealed class AndSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left, _right;
    public AndSpecification(ISpecification<T> left, ISpecification<T> right) { _left = left; _right = right; }
    public override Expression<Func<T, bool>> ToExpression()
        => _left.ToExpression().AndAlso(_right.ToExpression());
}

internal sealed class OrSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left, _right;
    public OrSpecification(ISpecification<T> left, ISpecification<T> right) { _left = left; _right = right; }
    public override Expression<Func<T, bool>> ToExpression()
        => _left.ToExpression().OrElse(_right.ToExpression());
}

internal sealed class NotSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _inner;
    public NotSpecification(ISpecification<T> inner) => _inner = inner;
    public override Expression<Func<T, bool>> ToExpression()
        => _inner.ToExpression().Not();
}

/// <summary>Always true.</summary>
public sealed class TrueSpecification<T> : Specification<T>
{
    private static readonly Expression<Func<T, bool>> _expr = _ => true;
    public override Expression<Func<T, bool>> ToExpression() => _expr;
}

/// <summary>Always false.</summary>
public sealed class FalseSpecification<T> : Specification<T>
{
    private static readonly Expression<Func<T, bool>> _expr = _ => false;
    public override Expression<Func<T, bool>> ToExpression() => _expr;
}
