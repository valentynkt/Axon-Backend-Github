using BuildingBlocks.Core.Domain.Primitives;
using System.Linq.Expressions;

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
