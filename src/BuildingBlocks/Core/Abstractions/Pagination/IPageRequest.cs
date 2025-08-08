using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BuildingBlocks.Core.Abstractions.Pagination;

public interface IPageRequest
{
    // Legacy names kept for compatibility (many handlers might depend on them).
    // Prefer using Page going forward.
    [Obsolete("Use Page (1-based) instead of PageNumber.")]
    int PageNumber { get; init; }

    int PageSize { get; init; } // server caps will be enforced by PageQueryBase

    // Added for scalable pagination. Default interface impl avoids breaking existing implementers.
    bool IncludeTotalCount => false;

    // Typed sorting preferred; server will inject a unique tiebreaker.
    IReadOnlyList<SortCriteria>? SortBy => null;

    // Legacy Sieve-style props (string-based). Keep but discourage.
    [Obsolete("Use explicit filter properties + SortBy.")]
    string? Filters => null;

    [Obsolete("Use SortBy.")]
    string? SortOrder => null;
}