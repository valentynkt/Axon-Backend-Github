using FastEndpoints;

namespace BuildingBlocks.Web.Contracts;

/// <summary>
/// Represents a request that supports pagination parameters
/// </summary>
public interface IPaginated
{
    int? PageNumber { get; init; }
    int? PageSize { get; init; }
}

/// <summary>
/// Abstract base record for paginated requests with default values
/// </summary>
public abstract record BasePagedRequest : IPaginated
{
    /// <summary>
    /// The page number (1-based). Defaults to 1 if not specified.
    /// </summary>
    [QueryParam]
    public virtual int? PageNumber { get; init; } = 1;

    /// <summary>
    /// The number of items per page. Defaults to 20 if not specified.
    /// </summary>
    [QueryParam] 
    public virtual int? PageSize { get; init; } = 20;
}