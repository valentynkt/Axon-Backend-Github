# Story 05: Enhanced Pagination Support with Metadata and Caching

**Story ID:** AXON-CQRS-005  
**Epic:** Epic_04_CQRS_Foundation  
**Priority:** P2 - Medium  
**Estimated Effort:** 4 hours  
**Dependencies:** Story_01 (W3C TraceContext and Metadata), Story_02 (Caching)  
**Target Sprint:** Current  

---

## 📋 User Story

**As a** backend developer implementing paginated queries,  
**I want** enhanced pagination support that leverages metadata context, caching, and provides rich sorting capabilities,  
**So that** I can implement high-performance, tenant-aware paginated queries with consistent user experience.

---

## 🎯 Story Context

### Existing System Integration

- **Current State:** 
  - Basic `IPageQuery<T>` interface exists
  - `PageQueryBase<T>` record with basic pagination
  - Simple `PagedResult<T>` wrapper
  - No metadata integration
  - Limited sorting support

- **Integration Points:**
  - Existing pagination infrastructure
  - Query caching from Story 02
  - Metadata context from Story 01
  - Entity Framework integration

- **Technology Stack:** 
  - .NET 10, C# 12
  - Entity Framework Core
  - LINQ expressions
  - Result<T> pattern

- **Architectural Layer:** BuildingBlocks/Core/Abstractions/Pagination

### Patterns to Follow

```csharp
// Existing pagination pattern
public interface IPageQuery<TResponse> : IPageRequest, IQuery<TResponse>
{
    // Basic pagination properties
}

public abstract record PageQueryBase<TResponse> : RequestBase<Result<TResponse>>, IPageQuery<TResponse>
{
    // Basic implementation
}
```

---

## ✅ Acceptance Criteria

### Functional Requirements

1. **Enhanced Pagination Interface**
   - [ ] Support for multiple sort criteria with direction
   - [ ] Cursor-based pagination for large datasets
   - [ ] Metadata-aware page size limits per tenant
   - [ ] Dynamic filtering based on metadata context

2. **Smart Caching Integration**
   - [ ] Cache pagination metadata separately from data
   - [ ] Intelligent cache invalidation on data changes
   - [ ] Cache warming for common pagination patterns
   - [ ] Per-tenant cache isolation

3. **Performance Optimizations**
   - [ ] Efficient COUNT queries with caching
   - [ ] Skip COUNT for cursor-based pagination
   - [ ] Optimized sorting with database indexes
   - [ ] Batched prefetching for related data

4. **Rich Metadata Support**
   - [ ] Include pagination context in traces
   - [ ] Tenant-specific page size defaults
   - [ ] User preference integration
   - [ ] Feature flag controlled pagination behavior

### Non-Functional Requirements

1. **Performance**
   - [ ] Support for datasets > 1M records
   - [ ] < 100ms response time for cached pages
   - [ ] Minimal memory usage for large result sets
   - [ ] Efficient database query generation

2. **Usability**
   - [ ] Consistent pagination across all queries
   - [ ] Self-describing pagination metadata
   - [ ] Easy navigation (next/prev/first/last)
   - [ ] URL-friendly pagination parameters

3. **Scalability**
   - [ ] Horizontal scaling with cache sharding
   - [ ] Database connection pooling
   - [ ] Async processing support
   - [ ] Rate limiting integration

---

## 🔧 Technical Implementation

### Files to Create/Modify

```yaml
Enhanced_Files:
  - src/BuildingBlocks/Core/Abstractions/Pagination/IPageQuery.cs
  - src/BuildingBlocks/Core/Abstractions/Pagination/PageQueryBase.cs
  - src/BuildingBlocks/Core/Abstractions/Pagination/PagedResult.cs

New_Files:
  - src/BuildingBlocks/Core/Abstractions/Pagination/ISortablePageQuery.cs
  - src/BuildingBlocks/Core/Abstractions/Pagination/ICursorPageQuery.cs
  - src/BuildingBlocks/Core/Abstractions/Pagination/SortCriteria.cs
  - src/BuildingBlocks/Core/Abstractions/Pagination/CursorPagination.cs
  - src/BuildingBlocks/Application/Pagination/PaginationContext.cs
  - src/BuildingBlocks/Infrastructure/Persistence/Pagination/EnhancedPaginationExtensions.cs

Tests:
  - tests/BuildingBlocks.Tests/Core/Pagination/PaginationTests.cs
  - tests/BuildingBlocks.Tests/Infrastructure/PaginationExtensionsTests.cs
```

### Implementation Steps

#### Step 1: Enhanced Pagination Interfaces

```csharp
public interface ISortablePageQuery<TResponse> : IPageQuery<TResponse>
{
    /// <summary>
    /// Multiple sort criteria with precedence
    /// </summary>
    IReadOnlyList<SortCriteria> SortBy { get; }
    
    /// <summary>
    /// Default sorting when none specified
    /// </summary>
    IReadOnlyList<SortCriteria> DefaultSort { get; }
}

public interface ICursorPageQuery<TResponse> : IQuery<TResponse>
{
    /// <summary>
    /// Cursor for position-based pagination
    /// </summary>
    string? Cursor { get; }
    
    /// <summary>
    /// Number of items to fetch
    /// </summary>
    int Size { get; }
    
    /// <summary>
    /// Sort criteria for cursor positioning
    /// </summary>
    IReadOnlyList<SortCriteria> SortBy { get; }
}

public record SortCriteria(string PropertyName, SortDirection Direction = SortDirection.Ascending)
{
    public static SortCriteria Ascending(string propertyName) => new(propertyName, SortDirection.Ascending);
    public static SortCriteria Descending(string propertyName) => new(propertyName, SortDirection.Descending);
}

public enum SortDirection
{
    Ascending = 0,
    Descending = 1
}
```

#### Step 2: Enhanced PageQueryBase with Metadata Support

```csharp
public abstract record PageQueryBase<TResponse> : RequestBase<Result<TResponse>>, ISortablePageQuery<TResponse>
    where TResponse : notnull
{
    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public virtual int Page { get; init; } = 1;
    
    /// <summary>
    /// Items per page with metadata-aware defaults
    /// </summary>
    public virtual int Size { get; init; } = GetDefaultPageSize();
    
    /// <summary>
    /// Sorting criteria
    /// </summary>
    public virtual IReadOnlyList<SortCriteria> SortBy { get; init; } = Array.Empty<SortCriteria>();
    
    /// <summary>
    /// Default sorting when none specified
    /// </summary>
    public virtual IReadOnlyList<SortCriteria> DefaultSort => Array.Empty<SortCriteria>();
    
    /// <summary>
    /// Caching enabled by default for paginated queries
    /// </summary>
    public override bool UseCache { get; init; } = true;
    
    /// <summary>
    /// Shorter cache duration for paginated data
    /// </summary>
    public override TimeSpan? CacheDuration { get; init; } = TimeSpan.FromMinutes(2);
    
    /// <summary>
    /// Cache key includes pagination parameters
    /// </summary>
    public override string CacheKeyPrefix => $"{GetType().Name}:p{Page}:s{Size}:{GetSortKey()}";
    
    /// <summary>
    /// Skip and Take calculations
    /// </summary>
    public int Skip => Math.Max(0, (Page - 1) * Size);
    public int Take => Math.Max(1, Math.Min(Size, GetMaxPageSize()));
    
    /// <summary>
    /// Effective sort criteria (combining explicit and default)
    /// </summary>
    public IReadOnlyList<SortCriteria> EffectiveSortBy => 
        SortBy.Any() ? SortBy : DefaultSort;
    
    private int GetDefaultPageSize()
    {
        // Check metadata for tenant-specific defaults
        if (Metadata.TryGetValue("TenantId", out var tenantId))
        {
            return GetTenantPageSize(tenantId.ToString()) ?? 20;
        }
        
        return 20;
    }
    
    private int GetMaxPageSize()
    {
        // Feature flag for larger page sizes
        if (Metadata.TryGetValue("FeatureFlags", out var flags) &&
            flags is IReadOnlyDictionary<string, bool> flagDict &&
            flagDict.GetValueOrDefault("LargePageSizes", false))
        {
            return 1000;
        }
        
        return 100;
    }
    
    private string GetSortKey()
    {
        if (!EffectiveSortBy.Any()) return "default";
        
        return string.Join(",", EffectiveSortBy.Select(s => $"{s.PropertyName}:{s.Direction}"));
    }
    
    private int? GetTenantPageSize(string? tenantId)
    {
        // This could be retrieved from configuration or database
        return tenantId switch
        {
            "enterprise-tenant" => 50,
            "premium-tenant" => 30,
            _ => null
        };
    }
}
```

#### Step 3: Enhanced PagedResult with Rich Metadata

```csharp
public record PagedResult<T> : IResult
{
    public IReadOnlyList<T> Items { get; }
    public PaginationMetadata Pagination { get; }
    public bool IsFailure => false;
    public Error? Error => null;
    
    public PagedResult(
        IReadOnlyList<T> items, 
        PaginationMetadata pagination)
    {
        Items = items;
        Pagination = pagination;
    }
    
    public static PagedResult<T> Create(
        IReadOnlyList<T> items,
        int page,
        int size,
        long totalCount,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var paginationMetadata = new PaginationMetadata(
            page, size, totalCount, items.Count, metadata);
            
        return new PagedResult<T>(items, paginationMetadata);
    }
}

public record PaginationMetadata
{
    public int CurrentPage { get; }
    public int PageSize { get; }
    public long TotalCount { get; }
    public int ItemCount { get; }
    public int TotalPages { get; }
    public bool HasNextPage { get; }
    public bool HasPreviousPage { get; }
    public int? NextPage { get; }
    public int? PreviousPage { get; }
    public IReadOnlyDictionary<string, object> Metadata { get; }
    
    public PaginationMetadata(
        int currentPage, 
        int pageSize, 
        long totalCount, 
        int itemCount,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        CurrentPage = currentPage;
        PageSize = pageSize;
        TotalCount = totalCount;
        ItemCount = itemCount;
        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        HasNextPage = currentPage < TotalPages;
        HasPreviousPage = currentPage > 1;
        NextPage = HasNextPage ? currentPage + 1 : null;
        PreviousPage = HasPreviousPage ? currentPage - 1 : null;
        Metadata = metadata ?? new Dictionary<string, object>().AsReadOnly();
    }
}
```

#### Step 4: Enhanced EF Core Extensions

```csharp
public static class EnhancedPaginationExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        ISortablePageQuery<PagedResult<T>> pageQuery,
        CancellationToken cancellationToken = default)
    {
        // Apply sorting
        var sortedQuery = ApplySorting(query, pageQuery.EffectiveSortBy);
        
        // Get total count with caching
        var totalCount = await GetTotalCountAsync(sortedQuery, pageQuery, cancellationToken);
        
        // Apply pagination
        var items = await sortedQuery
            .Skip(pageQuery.Skip)
            .Take(pageQuery.Take)
            .ToListAsync(cancellationToken);
        
        // Create rich metadata
        var metadata = new Dictionary<string, object>
        {
            ["TraceId"] = pageQuery.TraceId ?? "unknown",
            ["QueryTime"] = DateTimeOffset.UtcNow,
            ["SortCriteria"] = pageQuery.EffectiveSortBy,
            ["CacheHit"] = false // Will be updated by caching behavior
        };
        
        if (pageQuery.Metadata.TryGetValue("TenantId", out var tenantId))
        {
            metadata["TenantId"] = tenantId;
        }
        
        return PagedResult<T>.Create(
            items.AsReadOnly(),
            pageQuery.Page,
            pageQuery.Size,
            totalCount,
            metadata.AsReadOnly());
    }
    
    public static async Task<CursorPagedResult<T>> ToCursorPagedResultAsync<T>(
        this IQueryable<T> query,
        ICursorPageQuery<CursorPagedResult<T>> cursorQuery,
        Func<T, string> cursorSelector,
        CancellationToken cancellationToken = default)
    {
        // Apply sorting
        var sortedQuery = ApplySorting(query, cursorQuery.SortBy);
        
        // Apply cursor filtering
        if (!string.IsNullOrEmpty(cursorQuery.Cursor))
        {
            sortedQuery = ApplyCursorFilter(sortedQuery, cursorQuery.Cursor, cursorQuery.SortBy);
        }
        
        // Fetch one extra item to determine if there are more results
        var items = await sortedQuery
            .Take(cursorQuery.Size + 1)
            .ToListAsync(cancellationToken);
        
        var hasNextPage = items.Count > cursorQuery.Size;
        var resultItems = hasNextPage ? items.Take(cursorQuery.Size).ToList() : items;
        
        var nextCursor = hasNextPage && resultItems.Any() 
            ? cursorSelector(resultItems.Last()) 
            : null;
        
        return new CursorPagedResult<T>(
            resultItems.AsReadOnly(),
            nextCursor,
            hasNextPage);
    }
    
    private static IQueryable<T> ApplySorting<T>(
        IQueryable<T> query, 
        IReadOnlyList<SortCriteria> sortCriteria)
    {
        if (!sortCriteria.Any())
            return query;
        
        IOrderedQueryable<T>? orderedQuery = null;
        
        foreach (var sort in sortCriteria)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, sort.PropertyName);
            var lambda = Expression.Lambda(property, parameter);
            
            var methodName = orderedQuery == null
                ? (sort.Direction == SortDirection.Ascending ? "OrderBy" : "OrderByDescending")
                : (sort.Direction == SortDirection.Ascending ? "ThenBy" : "ThenByDescending");
            
            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.Type);
            
            orderedQuery = (IOrderedQueryable<T>)method.Invoke(null, new object[] { orderedQuery ?? query, lambda })!;
        }
        
        return orderedQuery ?? query;
    }
    
    private static async Task<long> GetTotalCountAsync<T>(
        IQueryable<T> query,
        ISortablePageQuery<PagedResult<T>> pageQuery,
        CancellationToken cancellationToken)
    {
        // Use cached count if available and query supports caching
        if (pageQuery.UseCache)
        {
            var countCacheKey = $"{pageQuery.CacheKeyPrefix}:count";
            // Implementation would integrate with caching behavior
        }
        
        return await query.LongCountAsync(cancellationToken);
    }
}

public record CursorPagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public string? NextCursor { get; }
    public bool HasNextPage { get; }
    
    public CursorPagedResult(IReadOnlyList<T> items, string? nextCursor, bool hasNextPage)
    {
        Items = items;
        NextCursor = nextCursor;
        HasNextPage = hasNextPage;
    }
}
```

#### Step 5: Usage Example

```csharp
public record GetUsersQuery : PageQueryBase<PagedResult<UserDto>>
{
    public string? SearchTerm { get; init; }
    public UserStatus? Status { get; init; }
    
    // Default sorting by creation date
    public override IReadOnlyList<SortCriteria> DefaultSort => 
        new[] { SortCriteria.Descending("CreatedAt") };
    
    // Enable caching with longer duration for user lists
    public override TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

// Handler implementation
public class GetUsersHandler : IQueryHandler<GetUsersQuery, PagedResult<UserDto>>
{
    private readonly IReadDbContext _dbContext;
    
    public async Task<Result<PagedResult<UserDto>>> Handle(
        GetUsersQuery query, 
        CancellationToken cancellationToken)
    {
        var usersQuery = _dbContext.Query<User>()
            .Where(u => query.SearchTerm == null || u.Name.Contains(query.SearchTerm))
            .Where(u => query.Status == null || u.Status == query.Status);
        
        // Apply tenant filtering from metadata
        if (query.Metadata.TryGetValue("TenantId", out var tenantId))
        {
            usersQuery = usersQuery.Where(u => u.TenantId == tenantId.ToString());
        }
        
        var pagedResult = await usersQuery
            .Select(u => new UserDto(u.Id, u.Name, u.Email, u.Status))
            .ToPagedResultAsync(query, cancellationToken);
        
        return Result<PagedResult<UserDto>>.Success(pagedResult);
    }
}

// Usage with sorting
var query = new GetUsersQuery
{
    Page = 2,
    Size = 25,
    SearchTerm = "john",
    SortBy = new[]
    {
        SortCriteria.Ascending("Name"),
        SortCriteria.Descending("CreatedAt")
    }
}.WithMetadata<GetUsersQuery>("TenantId", "tenant-123");
```

---

## 🧪 Testing Requirements

### Unit Tests

```csharp
[Fact]
public void Should_Calculate_Pagination_Metadata_Correctly()
{
    // Arrange
    var items = Enumerable.Range(1, 15).Select(i => $"Item {i}").ToList().AsReadOnly();
    
    // Act
    var result = PagedResult<string>.Create(items, page: 2, size: 10, totalCount: 95);
    
    // Assert
    result.Pagination.CurrentPage.Should().Be(2);
    result.Pagination.TotalPages.Should().Be(10);
    result.Pagination.HasNextPage.Should().BeTrue();
    result.Pagination.HasPreviousPage.Should().BeTrue();
    result.Pagination.NextPage.Should().Be(3);
    result.Pagination.PreviousPage.Should().Be(1);
}

[Fact]
public void Should_Apply_Tenant_Specific_Page_Size_Defaults()
{
    // Arrange
    var query = new TestPageQuery
    {
        Metadata = new Dictionary<string, object>
        {
            ["TenantId"] = "enterprise-tenant"
        }.AsReadOnly()
    };
    
    // Act
    var defaultSize = query.Size;
    
    // Assert
    defaultSize.Should().Be(50); // Enterprise tenant default
}
```

### Integration Tests

```csharp
[Fact]
public async Task Should_Apply_Sorting_And_Pagination_To_EF_Query()
{
    // Arrange
    using var context = CreateTestDbContext();
    await SeedTestData(context);
    
    var query = new GetUsersQuery
    {
        Page = 2,
        Size = 5,
        SortBy = new[] { SortCriteria.Descending("CreatedAt") }
    };
    
    // Act
    var result = await context.Users
        .ToPagedResultAsync(query, CancellationToken.None);
    
    // Assert
    result.Items.Should().HaveCount(5);
    result.Pagination.CurrentPage.Should().Be(2);
    result.Items.Should().BeInDescendingOrder(u => u.CreatedAt);
}
```

---

## 📐 Architecture Considerations

### Caching Strategy for Pagination

1. **Data Caching**: Cache actual page results
2. **Count Caching**: Cache total count separately with longer TTL
3. **Metadata Caching**: Cache pagination metadata
4. **Invalidation**: Smart invalidation on data changes

### Performance Optimization

```csharp
// Efficient counting for large datasets
SELECT COUNT(*) FROM (
    SELECT 1 FROM Users 
    WHERE TenantId = @tenantId 
    LIMIT 10001  -- Stop counting at reasonable limit
) as counted;

// Cursor-based pagination for real-time data
SELECT * FROM Users 
WHERE CreatedAt > @cursor 
ORDER BY CreatedAt 
LIMIT @size;
```

### Database Indexing Strategy

```sql
-- Composite indexes for common pagination patterns
CREATE INDEX IX_Users_TenantId_CreatedAt ON Users (TenantId, CreatedAt DESC);
CREATE INDEX IX_Users_TenantId_Name_CreatedAt ON Users (TenantId, Name, CreatedAt DESC);
```

---

## 📦 Definition of Done

- [ ] Enhanced pagination interfaces implemented
- [ ] Metadata-aware PageQueryBase with tenant defaults
- [ ] Rich PagedResult with navigation metadata
- [ ] EF Core extensions with sorting support
- [ ] Cursor-based pagination for large datasets
- [ ] Unit tests with 100% coverage
- [ ] Integration tests with EF Core
- [ ] Performance benchmarks documented
- [ ] Database indexing recommendations

---

## 🔄 Migration Strategy

### Phase 1: Backward Compatible Enhancement
- Deploy enhanced interfaces alongside existing ones
- Existing queries continue to work unchanged
- New queries can opt-in to enhanced features

### Phase 2: Feature Adoption
```csharp
// Before: Basic pagination
public record GetUsersQuery : PageQueryBase<PagedResult<UserDto>>
{
    // Basic implementation
}

// After: Enhanced with sorting and metadata
public record GetUsersQuery : PageQueryBase<PagedResult<UserDto>>
{
    public override IReadOnlyList<SortCriteria> DefaultSort => 
        new[] { SortCriteria.Descending("CreatedAt") };
    
    public override TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}
```

### Phase 3: Performance Optimization
- Implement cursor-based pagination for high-volume queries
- Add specialized indexes based on usage patterns
- Optimize caching strategies

---

## 📊 Success Metrics

- Page load time < 100ms for cached results
- Support for 10M+ record datasets
- Zero pagination-related bugs in production
- 90%+ cache hit rate for common page requests

---

## 🚀 Follow-up Stories

1. **Story 06**: GraphQL Integration with Relay Pagination
2. **Story 07**: Real-time Pagination with SignalR
3. **Story 08**: Advanced Filtering and Search Integration
4. **Story 09**: Pagination Analytics and Optimization