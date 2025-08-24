#nullable enable

using System.Linq.Expressions;
using Ardalis.Specification;
using Axon.Modules.Chat.Application.Common.Pagination;

namespace Axon.Modules.Chat.Application.Specifications.Base;

/// <summary>
/// Extension methods for ISpecificationBuilder to provide common query patterns.
/// Ensures EF Core-translatable expressions and consistent query building.
/// </summary>
public static class SpecBuilderExtensions
{
    /// <summary>
    /// Applies paging configuration to the specification builder.
    /// Configures Skip and Take operations based on page parameters.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="builder">The specification builder to configure</param>
    /// <param name="page">The page configuration</param>
    /// <returns>The configured specification builder for method chaining</returns>
    public static ISpecificationBuilder<T> WithPaging<T>(
        this ISpecificationBuilder<T> builder,
        Page page)
        where T : class
    {
        return builder
            .Skip(page.Skip)
            .Take(page.Size);
    }

    /// <summary>
    /// Adds a safe equality filter to the specification.
    /// Generates EF Core-translatable expressions for property equality comparisons.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <typeparam name="TProp">The property type to compare</typeparam>
    /// <param name="builder">The specification builder to configure</param>
    /// <param name="propertySelector">Expression to select the property to compare</param>
    /// <param name="value">The value to compare against</param>
    /// <returns>The configured specification builder for method chaining</returns>
    public static ISpecificationBuilder<T> EqualTo<T, TProp>(
        this ISpecificationBuilder<T> builder,
        Expression<Func<T, TProp>> propertySelector,
        TProp value)
        where T : class
    {
        // Create a safe equality comparison expression
        var parameter = propertySelector.Parameters[0];
        var property = propertySelector.Body;
        var constant = Expression.Constant(value, typeof(TProp));
        var equality = Expression.Equal(property, constant);
        var lambda = Expression.Lambda<Func<T, bool>>(equality, parameter);
        
        return builder.Where(lambda);
    }

    /// <summary>
    /// Adds an ownership filter to the specification for multi-tenant scenarios.
    /// Ensures queries only return entities belonging to the specified owner.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="builder">The specification builder to configure</param>
    /// <param name="ownerSelector">Expression to select the owner identifier property</param>
    /// <param name="ownerId">The owner identifier to filter by</param>
    /// <returns>The configured specification builder for method chaining</returns>
    public static ISpecificationBuilder<T> WhereOwner<T>(
        this ISpecificationBuilder<T> builder,
        Expression<Func<T, Guid>> ownerSelector,
        Guid ownerId)
        where T : class
    {
        return builder.EqualTo(ownerSelector, ownerId);
    }

    /// <summary>
    /// Adds a text search filter using case-insensitive contains matching.
    /// Generates EF Core-translatable expressions for string searching.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="builder">The specification builder to configure</param>
    /// <param name="propertySelector">Expression to select the string property to search</param>
    /// <param name="searchText">The text to search for</param>
    /// <returns>The configured specification builder for method chaining</returns>
    public static ISpecificationBuilder<T> ContainsText<T>(
        this ISpecificationBuilder<T> builder,
        Expression<Func<T, string>> propertySelector,
        string searchText)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return builder;

        var parameter = propertySelector.Parameters[0];
        var property = propertySelector.Body;
        var searchValue = Expression.Constant(searchText.ToLowerInvariant());
        
        // Generate: property != null && property.ToLower().Contains(searchValue)
        var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var toLower = Expression.Call(property, nameof(string.ToLower), null);
        var contains = Expression.Call(toLower, nameof(string.Contains), null, searchValue);
        var combined = Expression.AndAlso(nullCheck, contains);
        
        var lambda = Expression.Lambda<Func<T, bool>>(combined, parameter);
        return builder.Where(lambda);
    }


    /// <summary>
    /// Adds a date range filter to the specification.
    /// Generates EF Core-translatable expressions for date comparisons.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="builder">The specification builder to configure</param>
    /// <param name="dateSelector">Expression to select the date property</param>
    /// <param name="fromDate">The start date (inclusive), null for no start limit</param>
    /// <param name="toDate">The end date (inclusive), null for no end limit</param>
    /// <returns>The configured specification builder for method chaining</returns>
    public static ISpecificationBuilder<T> WhereDateRange<T>(
        this ISpecificationBuilder<T> builder,
        Expression<Func<T, DateTime>> dateSelector,
        DateTime? fromDate,
        DateTime? toDate)
        where T : class
    {
        if (fromDate.HasValue)
        {
            builder = builder.Where(CreateDateComparisonExpression(dateSelector, fromDate.Value, true));
        }

        if (toDate.HasValue)
        {
            builder = builder.Where(CreateDateComparisonExpression(dateSelector, toDate.Value, false));
        }

        return builder;
    }

    private static Expression<Func<T, bool>> CreateDateComparisonExpression<T>(
        Expression<Func<T, DateTime>> dateSelector,
        DateTime compareDate,
        bool isGreaterThanOrEqual)
    {
        var parameter = dateSelector.Parameters[0];
        var property = dateSelector.Body;
        var constant = Expression.Constant(compareDate);
        
        var comparison = isGreaterThanOrEqual
            ? Expression.GreaterThanOrEqual(property, constant)
            : Expression.LessThanOrEqual(property, constant);
            
        return Expression.Lambda<Func<T, bool>>(comparison, parameter);
    }
}