using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Model;
using BuildingBlocks.Core.Domain.Model.Traits;

namespace BuildingBlocks.Core.Domain.Specifications;

/// <summary>
/// Common specifications for entities that implement standard interfaces.
/// These specifications can be combined with domain-specific specifications.
/// </summary>
public static class CommonSpecifications
{
    /// <summary>
    /// Specification for active (non-deleted) entities.
    /// Only applies to entities that implement ISoftDeletable.
    /// </summary>
    /// <typeparam name="T">Entity type that implements ISoftDeletable</typeparam>
    /// <returns>Specification that filters out soft-deleted entities</returns>
    public static Specification<T> Active<T>() 
        where T : ISoftDeletable
    {
        return new ActiveSpecification<T>();
    }
    
    /// <summary>
    /// Specification for soft-deleted entities.
    /// Only applies to entities that implement ISoftDeletable.
    /// </summary>
    /// <typeparam name="T">Entity type that implements ISoftDeletable</typeparam>
    /// <returns>Specification that filters for soft-deleted entities</returns>
    public static Specification<T> Deleted<T>() 
        where T : ISoftDeletable
    {
        return new DeletedSpecification<T>();
    }
    
    /// <summary>
    /// Specification for entities created within a date range.
    /// Only applies to entities that implement IAuditable.
    /// </summary>
    /// <typeparam name="T">Entity type that implements IAuditable</typeparam>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <returns>Specification for entities created within the date range</returns>
    public static Specification<T> CreatedBetween<T>(DateTime from, DateTime to) 
        where T : IAuditable
    {
        return new CreatedBetweenSpecification<T>(from, to);
    }
    
    /// <summary>
    /// Specification for entities created after a specific date.
    /// Only applies to entities that implement IAuditable.
    /// </summary>
    /// <typeparam name="T">Entity type that implements IAuditable</typeparam>
    /// <param name="date">The date after which entities were created</param>
    /// <returns>Specification for entities created after the date</returns>
    public static Specification<T> CreatedAfter<T>(DateTime date) 
        where T : IAuditable
    {
        return new CreatedAfterSpecification<T>(date);
    }
    
    /// <summary>
    /// Specification for entities created before a specific date.
    /// Only applies to entities that implement IAuditable.
    /// </summary>
    /// <typeparam name="T">Entity type that implements IAuditable</typeparam>
    /// <param name="date">The date before which entities were created</param>
    /// <returns>Specification for entities created before the date</returns>
    public static Specification<T> CreatedBefore<T>(DateTime date) 
        where T : IAuditable
    {
        return new CreatedBeforeSpecification<T>(date);
    }
    
    /// <summary>
    /// Specification for entities updated within a date range.
    /// Only applies to entities that implement IAuditable.
    /// </summary>
    /// <typeparam name="T">Entity type that implements IAuditable</typeparam>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <returns>Specification for entities updated within the date range</returns>
    public static Specification<T> UpdatedBetween<T>(DateTime from, DateTime to) 
        where T : IAuditable
    {
        return new UpdatedBetweenSpecification<T>(from, to);
    }
    
    /// <summary>
    /// Specification for entities with a specific version.
    /// Only applies to entities that implement IVersion.
    /// </summary>
    /// <typeparam name="T">Entity type that implements IVersion</typeparam>
    /// <param name="version">The version to match</param>
    /// <returns>Specification for entities with the specified version</returns>
    public static Specification<T> WithVersion<T>(uint version) 
        where T : IVersion
    {
        return new VersionSpecification<T>(version);
    }
    
    /// <summary>
    /// Specification for entities with version greater than specified value.
    /// Only applies to entities that implement IVersion.
    /// </summary>
    /// <typeparam name="T">Entity type that implements IVersion</typeparam>
    /// <param name="version">The minimum version (exclusive)</param>
    /// <returns>Specification for entities with version greater than specified</returns>
    public static Specification<T> VersionGreaterThan<T>(uint version) 
        where T : IVersion
    {
        return new VersionGreaterThanSpecification<T>(version);
    }
}

// Implementation classes

/// <summary>
/// Specification for active (non-soft-deleted) entities.
/// </summary>
/// <typeparam name="T">Entity type that implements ISoftDeletable</typeparam>
internal sealed class ActiveSpecification<T> : Specification<T>
    where T : ISoftDeletable
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => !entity.IsDeleted;
    }
}

/// <summary>
/// Specification for soft-deleted entities.
/// </summary>
/// <typeparam name="T">Entity type that implements ISoftDeletable</typeparam>
internal sealed class DeletedSpecification<T> : Specification<T>
    where T : ISoftDeletable
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => entity.IsDeleted;
    }
}

/// <summary>
/// Specification for entities created within a date range.
/// </summary>
/// <typeparam name="T">Entity type that implements IAuditable</typeparam>
internal sealed class CreatedBetweenSpecification<T> : Specification<T>
    where T : IAuditable
{
    private readonly DateTime _from;
    private readonly DateTime _to;
    
    public CreatedBetweenSpecification(DateTime from, DateTime to)
    {
        if (from > to)
        {
            throw new ArgumentException("From date cannot be greater than to date", nameof(from));
        }
        
        _from = from;
        _to = to;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => entity.CreatedAt >= _from && entity.CreatedAt <= _to;
    }
}

/// <summary>
/// Specification for entities created after a specific date.
/// </summary>
/// <typeparam name="T">Entity type that implements IAuditable</typeparam>
internal sealed class CreatedAfterSpecification<T> : Specification<T>
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
/// Specification for entities created before a specific date.
/// </summary>
/// <typeparam name="T">Entity type that implements IAuditable</typeparam>
internal sealed class CreatedBeforeSpecification<T> : Specification<T>
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

/// <summary>
/// Specification for entities updated within a date range.
/// </summary>
/// <typeparam name="T">Entity type that implements IAuditable</typeparam>
internal sealed class UpdatedBetweenSpecification<T> : Specification<T>
    where T : IAuditable
{
    private readonly DateTime _from;
    private readonly DateTime _to;
    
    public UpdatedBetweenSpecification(DateTime from, DateTime to)
    {
        if (from > to)
        {
            throw new ArgumentException("From date cannot be greater than to date", nameof(from));
        }
        
        _from = from;
        _to = to;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => entity.UpdatedAt >= _from && entity.UpdatedAt <= _to;
    }
}

/// <summary>
/// Specification for entities with a specific version.
/// </summary>
/// <typeparam name="T">Entity type that implements IVersion</typeparam>
internal sealed class VersionSpecification<T> : Specification<T>
    where T : IVersion
{
    private readonly uint _version;
    
    public VersionSpecification(uint version)
    {
        _version = version;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => entity.Version == _version;
    }
}

/// <summary>
/// Specification for entities with version greater than specified value.
/// </summary>
/// <typeparam name="T">Entity type that implements IVersion</typeparam>
internal sealed class VersionGreaterThanSpecification<T> : Specification<T>
    where T : IVersion
{
    private readonly uint _version;
    
    public VersionGreaterThanSpecification(uint version)
    {
        _version = version;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        return entity => entity.Version > _version;
    }
}