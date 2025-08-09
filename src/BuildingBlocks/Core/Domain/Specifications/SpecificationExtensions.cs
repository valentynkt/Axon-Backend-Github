using System.Linq.Expressions;

namespace BuildingBlocks.Core.Domain.Specifications;

public static class SpecificationExtensions
{
    /// <summary>Apply a spec to an <see cref="IEnumerable{T}"/> using compiled predicate (fast LINQ-to-Objects).</summary>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, ISpecification<T> spec)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(spec);
        var predicate = (spec as Specification<T>)?.ToPredicate() ?? spec.ToExpression().Compile();
        return source.Where(predicate);
    }

    /// <summary>Apply a spec to an <see cref="IQueryable{T}"/>; stays as expression (EF-friendly, infra-agnostic).</summary>
    public static IQueryable<T> Where<T>(this IQueryable<T> source, ISpecification<T> spec)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(spec);
        return source.Where(spec.ToExpression());
    }

    public static bool Any<T>(this IEnumerable<T> source, ISpecification<T> spec)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(spec);
        var predicate = (spec as Specification<T>)?.ToPredicate() ?? spec.ToExpression().Compile();
        return source.Any(predicate);
    }

    public static bool Any<T>(this IQueryable<T> source, ISpecification<T> spec)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(spec);
        return source.Any(spec.ToExpression());
    }

    public static bool All<T>(this IEnumerable<T> source, ISpecification<T> spec)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(spec);
        var predicate = (spec as Specification<T>)?.ToPredicate() ?? spec.ToExpression().Compile();
        return source.All(predicate);
    }

    public static int Count<T>(this IEnumerable<T> source, ISpecification<T> spec)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(spec);
        var predicate = (spec as Specification<T>)?.ToPredicate() ?? spec.ToExpression().Compile();
        return source.Count(predicate);
    }

    public static T? FirstOrDefault<T>(this IEnumerable<T> source, ISpecification<T> spec)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(spec);
        var predicate = (spec as Specification<T>)?.ToPredicate() ?? spec.ToExpression().Compile();
        return source.FirstOrDefault(predicate);
    }

    public static T? FirstOrDefault<T>(this IQueryable<T> source, ISpecification<T> spec)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(spec);
        return source.FirstOrDefault(spec.ToExpression());
    }

    // Lambda combinators used by the internal spec combinators
    public static Expression<Func<T, bool>> AndAlso<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        => left.Compose(right, Expression.AndAlso);

    public static Expression<Func<T, bool>> OrElse<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        => left.Compose(right, Expression.OrElse);

    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expr)
    {
        var param = expr.Parameters[0];
        var body  = Expression.Not(expr.Body);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    private static Expression<Func<T, bool>> Compose<T>(this Expression<Func<T, bool>> first,
        Expression<Func<T, bool>> second, Func<Expression, Expression, Expression> merge)
    {
        var map = first.Parameters.Select((f, i) => new { f, s = second.Parameters[i] })
                                  .ToDictionary(p => p.s, p => p.f);
        var secondBody = new ParameterReplacer(map).Visit(second.Body)!;
        return Expression.Lambda<Func<T, bool>>(merge(first.Body, secondBody), first.Parameters);
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly IDictionary<ParameterExpression, ParameterExpression> _map;
        public ParameterReplacer(IDictionary<ParameterExpression, ParameterExpression> map) => _map = map;
        protected override Expression VisitParameter(ParameterExpression node)
            => _map.TryGetValue(node, out var replacement) ? replacement : base.VisitParameter(node);
    }
}
