using System.Linq.Expressions;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Domain.Specifications;

/// <summary>
/// Extension methods for specifications to improve usability and integration.
/// These methods provide fluent APIs for common specification operations.
/// </summary>
public static class SpecificationExtensions
{
    /// <summary>
    /// Apply specification to an IQueryable for database queries.
    /// This is the primary method for using specifications with Entity Framework.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="queryable">The IQueryable to filter</param>
    /// <param name="specification">The specification to apply</param>
    /// <returns>Filtered IQueryable</returns>
    public static IQueryable<T> Where<T>(this IQueryable<T> queryable, Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(specification);
        
        return queryable.Where(specification.ToExpression());
    }
    
    /// <summary>
    /// Apply specification to an IEnumerable for in-memory filtering.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="enumerable">The IEnumerable to filter</param>
    /// <param name="specification">The specification to apply</param>
    /// <returns>Filtered IEnumerable</returns>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> enumerable, Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        ArgumentNullException.ThrowIfNull(specification);
        
        var predicate = specification.Compile();
        return enumerable.Where(predicate);
    }
    
    /// <summary>
    /// Check if any item in the collection satisfies the specification.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="enumerable">The collection to check</param>
    /// <param name="specification">The specification to test</param>
    /// <returns>True if any item satisfies the specification</returns>
    public static bool Any<T>(this IEnumerable<T> enumerable, Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        ArgumentNullException.ThrowIfNull(specification);
        
        var predicate = specification.Compile();
        return enumerable.Any(predicate);
    }
    
    /// <summary>
    /// Check if all items in the collection satisfy the specification.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="enumerable">The collection to check</param>
    /// <param name="specification">The specification to test</param>
    /// <returns>True if all items satisfy the specification</returns>
    public static bool All<T>(this IEnumerable<T> enumerable, Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        ArgumentNullException.ThrowIfNull(specification);
        
        var predicate = specification.Compile();
        return enumerable.All(predicate);
    }
    
    /// <summary>
    /// Count items in the collection that satisfy the specification.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="enumerable">The collection to count</param>
    /// <param name="specification">The specification to test</param>
    /// <returns>Count of items that satisfy the specification</returns>
    public static int Count<T>(this IEnumerable<T> enumerable, Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        ArgumentNullException.ThrowIfNull(specification);
        
        var predicate = specification.Compile();
        return enumerable.Count(predicate);
    }
    
    /// <summary>
    /// Find the first item that satisfies the specification, or return a failed Result.
    /// This provides a functional approach to finding items.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="enumerable">The collection to search</param>
    /// <param name="specification">The specification to test</param>
    /// <param name="errorIfNotFound">Error to return if no item is found</param>
    /// <returns>Result containing the first matching item or an error</returns>
    public static Result<T> FirstOrError<T>(
        this IEnumerable<T> enumerable, 
        Specification<T> specification,
        Error? errorIfNotFound = null)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        ArgumentNullException.ThrowIfNull(specification);
        
        var predicate = specification.Compile();
        var item = enumerable.FirstOrDefault(predicate);
        
        if (item is null)
        {
            var error = errorIfNotFound ?? Error.NotFound("No item matches the specification", "ITEM_NOT_FOUND");
            return Result<T>.Failure(error);
        }
        
        return Result<T>.Success(item);
    }
    
    /// <summary>
    /// Find a single item that satisfies the specification, or return a failed Result.
    /// Fails if zero or more than one item matches.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="enumerable">The collection to search</param>
    /// <param name="specification">The specification to test</param>
    /// <param name="errorIfNotFound">Error to return if no item is found</param>
    /// <param name="errorIfMultiple">Error to return if multiple items are found</param>
    /// <returns>Result containing the single matching item or an error</returns>
    public static Result<T> SingleOrError<T>(
        this IEnumerable<T> enumerable, 
        Specification<T> specification,
        Error? errorIfNotFound = null,
        Error? errorIfMultiple = null)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        ArgumentNullException.ThrowIfNull(specification);
        
        var predicate = specification.Compile();
        var items = enumerable.Where(predicate).Take(2).ToList();
        
        return items.Count switch
        {
            0 => Result<T>.Failure(errorIfNotFound ?? Error.NotFound("No item matches the specification", "ITEM_NOT_FOUND")),
            1 => Result<T>.Success(items[0]),
            _ => Result<T>.Failure(errorIfMultiple ?? Error.Validation("Multiple items match the specification", "MULTIPLE_ITEMS_FOUND"))
        };
    }
    
    /// <summary>
    /// Validate that all items in the collection satisfy the specification.
    /// Returns a Validation result that accumulates errors for all failing items.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="enumerable">The collection to validate</param>
    /// <param name="specification">The specification to test</param>
    /// <param name="errorMessage">Error message template (can include {index} placeholder)</param>
    /// <param name="errorCode">Error code for validation failures</param>
    /// <returns>Validation result</returns>
    public static Validation<IReadOnlyList<T>> ValidateAll<T>(
        this IEnumerable<T> enumerable, 
        Specification<T> specification,
        string errorMessage = "Item at index {index} does not satisfy specification",
        string errorCode = "SPECIFICATION_VALIDATION_FAILED")
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        ArgumentNullException.ThrowIfNull(specification);
        
        var items = enumerable.ToList();
        var predicate = specification.Compile();
        var errors = new List<Error>();
        
        for (int i = 0; i < items.Count; i++)
        {
            if (!predicate(items[i]))
            {
                var message = errorMessage.Replace("{index}", i.ToString());
                errors.Add(Error.Validation(message, errorCode));
            }
        }
        
        return errors.Count != 0
            ? Validation<IReadOnlyList<T>>.Invalid(errors)
            : Validation<IReadOnlyList<T>>.Valid(items.AsReadOnly());
    }
}

/// <summary>
/// Static factory methods for creating common specifications.
/// </summary>
public static class Spec
{
    /// <summary>
    /// Create a specification from a lambda expression.
    /// This is a convenience method for creating simple specifications inline.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="predicate">The predicate expression</param>
    /// <returns>A specification wrapping the predicate</returns>
    public static Specification<T> From<T>(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new ExpressionSpecification<T>(predicate);
    }
    
    /// <summary>
    /// Create a specification that is always true.
    /// Useful as a neutral element in specification composition.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>A specification that always returns true</returns>
    public static Specification<T> True<T>()
    {
        return new TrueSpecification<T>();
    }
    
    /// <summary>
    /// Create a specification that is always false.
    /// Useful for creating empty result sets.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>A specification that always returns false</returns>
    public static Specification<T> False<T>()
    {
        return new FalseSpecification<T>();
    }
}

/// <summary>
/// Simple specification that wraps an expression.
/// This is used by the Spec.From factory method.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
internal sealed class ExpressionSpecification<T> : Specification<T>
{
    private readonly Expression<Func<T, bool>> _expression;
    
    public ExpressionSpecification(Expression<Func<T, bool>> expression)
    {
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        return _expression;
    }
}