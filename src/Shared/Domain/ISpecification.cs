using System.Linq.Expressions;

namespace Axon.Shared.Domain;

/// <summary>
/// Specification pattern interface for expressing business rules
/// </summary>
/// <typeparam name="T">The type of entity the specification applies to</typeparam>
public interface ISpecification<T>
{
    /// <summary>
    /// Determines whether the specified entity satisfies the specification
    /// </summary>
    /// <param name="entity">The entity to evaluate</param>
    /// <returns>True if the entity satisfies the specification; otherwise, false</returns>
    bool IsSatisfiedBy(T entity);

    /// <summary>
    /// Gets the expression tree representation of the specification
    /// </summary>
    /// <returns>An expression that can be used in LINQ queries</returns>
    Expression<Func<T, bool>> ToExpression();
}