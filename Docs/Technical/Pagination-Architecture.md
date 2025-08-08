# Axon Backend Pagination Architecture

**Version:** 2.0
**Status:** Final
**Owner:** System Architect
**Date:** 2025-08-08

## 1. Introduction

### 1.1. Philosophy and Design Goals

This document provides a comprehensive technical overview of the pagination system within the Axon Backend. The system is architected around several core principles:

-   **Flexibility:** To provide the right tool for the job, the framework supports both traditional **offset-based pagination** and modern **cursor-based pagination**.
-   **Performance:** The design prioritizes efficiency, especially in the cursor-based approach, which avoids the performance degradation common with large offsets in databases.
-   **Developer Experience:** By providing a clear set of abstractions (`PageQueryBase`, `PagedResult`, etc.), the system simplifies the implementation of pagination, reduces boilerplate code, and ensures consistency across the application.
-   **Robustness & Type Safety:** The use of immutable records and strong typing minimizes common pagination bugs and makes the system more predictable and maintainable.
-   **Clean Architecture & CQRS Alignment:** The components are designed to integrate seamlessly with the project's existing Clean Architecture and CQRS patterns.

### 1.2. When to Use Which Pagination Strategy

Choosing the correct pagination strategy is crucial for performance and user experience.

| Feature / Use Case        | Offset-Based Pagination                               | Cursor-Based (Keyset) Pagination                      |
| ------------------------- | ----------------------------------------------------- | ----------------------------------------------------- |
| **User Experience**       | Navigating to specific pages (e.g., "Go to page 5").  | Infinite scrolling, "Load More" buttons.              |
| **Dataset Size**          | Best for small to medium-sized, static datasets.      | Ideal for very large or rapidly changing datasets.    |
| **Performance**           | Can become slow as the page number (offset) increases. | Maintains consistent, fast performance at any depth.   |
| **Data Consistency**      | Prone to skipping/showing duplicate items if data changes between requests. | More resilient to real-time data changes.             |
| **Implementation Complexity** | Simpler to implement for basic use cases.             | More complex, requires careful handling of cursors and sorting. |
| **Canonical URLs**        | Page URLs are stable (e.g., `/items?page=3`).         | Page URLs are not stable; they depend on the cursor.  |

**Decision Guide:**
-   Use **Offset-Based Pagination** for administration panels, reports, or any UI where users need to jump to arbitrary pages.
-   Use **Cursor-Based Pagination** for user-facing feeds, timelines, or any list that loads dynamically as the user scrolls.

---

## 2. Core Concepts & Components

This section details the key interfaces, records, and classes that constitute the pagination framework.

### 2.1. Strategy 1: Offset-Based Pagination

#### `IPageRequest`
A simple interface defining the contract for a standard pagination request.
-   `PageNumber` (int): The 1-based page number to retrieve.
-   `PageSize` (int): The number of items per page.
-   `Filters` (string?): Legacy support for Sieve-based filtering. **Recommendation: Use explicit query properties instead.**
-   `SortOrder` (string?): Legacy support for Sieve-based sorting. **Recommendation: Use `SortBy` from `ISortablePageQuery` instead.**

#### `IPageQuery<TResponse>`
Combines `IPageRequest` with `IQuery<TResponse>` from the CQRS pattern, creating a unified interface for queries that return a paginated result.

#### `PagedResult<T>`
The standard immutable container for offset-paginated data. It provides a snapshot of a single page.
-   `Items` (IReadOnlyList<T>): The collection of items for the current page.
-   `Meta` (PaginationMeta): A rich metadata object containing all pagination details. All properties below are delegated from this object.
-   `TotalCount` (int): The total number of items available across all pages.
-   `PageNumber` (int): The number of the current page (1-based).
-   `PageSize` (int): The requested number of items per page.
-   `TotalPages` (int): The total number of pages, calculated as `Ceiling(TotalCount / PageSize)`.
-   `HasPrevious` (bool): True if `PageNumber > 1`.
-   `HasNext` (bool): True if `PageNumber < TotalPages`.
-   `CurrentPageSize` (int): The actual number of items in the `Items` list. This may be less than `PageSize` on the last page.
-   `CurrentStartIndex` (int): The 1-based index of the first item on the current page within the total set. Calculated as `(PageNumber - 1) * PageSize + 1`.
-   `CurrentEndIndex` (int): The 1-based index of the last item on the current page. Calculated as `CurrentStartIndex + CurrentPageSize - 1`.

#### `PaginationMeta`
An immutable record holding the detailed metadata for an offset-paginated result set. It performs validation in its factory `Create` method to ensure data integrity (e.g., page number > 0).
-   `Metadata` (IReadOnlyDictionary<string, object>?): An optional dictionary for passing through supplementary data, such as query execution time, trace IDs, or applied sorting criteria for debugging.

---

### 2.2. Strategy 2: Cursor-Based Pagination

#### `ICursorPageQuery<TResponse>`
The core interface for cursor-based pagination queries.
-   `Cursor` (string?): An opaque string that encodes the position of the last item from the previous page. If null or empty, the query starts from the beginning of the dataset.
-   `Size` (int): The number of items to retrieve per page.
-   `SortBy` (IReadOnlyList<SortCriteria>): **This is mandatory for cursor pagination.** It defines the order of the data. To prevent data loss or duplication, the sort criteria **must** include a unique, sequential key (like a timestamp combined with a unique ID).

#### `CursorPagedResult<T>`
The immutable container for cursor-paginated data.
-   `Items` (IReadOnlyList<T>): The collection of items for the current page.
-   `NextCursor` (string?): The cursor to be used to fetch the next page. This is generated from the last item of the current page's data. If `null`, this is the last page.
-   `HasNextPage` (bool): A boolean flag indicating if more pages are available.
-   `Metadata` (IReadOnlyDictionary<string, object>?): Optional dictionary for additional data.

---

### 2.3. Shared Abstractions

#### `SortCriteria` & `SortDirection`
-   **`SortCriteria`**: An immutable record representing a single sort instruction (`PropertyName`, `Direction`). It translates directly to a segment of a database `ORDER BY` clause.
-   **`SortDirection`**: An enum for `Ascending` (0) and `Descending` (1).

#### `ISortablePageQuery<TResponse>`
An interface that enhances `IPageQuery` with advanced, type-safe sorting.
-   `SortBy` (IReadOnlyList<SortCriteria>): A list of sorting criteria explicitly provided by the client.
-   `DefaultSort` (IReadOnlyList<SortCriteria>): A fallback list defined in the query itself, ensuring a consistent default order when the client doesn't specify one.
-   `EffectiveSortBy` (IReadOnlyList<SortCriteria>): A convenience property that intelligently returns `SortBy` if provided, otherwise falls back to `DefaultSort`. This should be the property used in query handlers.

#### `PageQueryBase<TResponse>`
An abstract record providing a robust foundation for offset-based queries.
-   **Validation:** Automatically validates that `Page` > 0 and `Size` is within a permitted range (default 1-100).
-   **Calculation:** Provides `Skip` (`(Page - 1) * Size`) and `Take` (`Size`) properties for direct use in database queries.
-   **Caching:** Includes a built-in caching mechanism. The cache key is automatically generated from the query name, page, size, and effective sort order to ensure uniqueness.
-   **Dynamic Page Size:** The `GetMaxPageSize()` method allows for overriding the maximum page size via request metadata, enabling scenarios like data exports where a larger page size is needed under controlled conditions.

---

## 3. Implementation Guide & Patterns

### 3.1. Implementing an Offset-Based Query

1.  **Define Query:**
    ```csharp
    // In Application layer
    public record GetUsersQuery : PageQueryBase<PagedResult<UserDto>>
    {
        public string? DepartmentFilter { get; init; }
        public override IReadOnlyList<SortCriteria> DefaultSort => 
            new[] { SortCriteria.Descending("CreatedAt") };
    }
    ```

2.  **Implement Handler:**
    ```csharp
    // In Application layer
    public class GetUsersQueryHandler : IQueryHandler<GetUsersQuery, PagedResult<UserDto>>
    {
        public async Task<PagedResult<UserDto>> Handle(GetUsersQuery query, CancellationToken ct)
        {
            // 1. Get IQueryable from repository
            var queryable = _userRepository.GetQueryable();

            // 2. Apply filters
            if (!string.IsNullOrEmpty(query.DepartmentFilter))
            {
                queryable = queryable.Where(u => u.Department == query.DepartmentFilter);
            }

            // 3. Get total count *before* pagination
            var totalCount = await queryable.CountAsync(ct);

            // 4. Apply sorting and pagination
            var sortedAndPagedQuery = queryable
                .ApplySort(query.EffectiveSortBy) // Assumes an extension method
                .Skip(query.Skip)
                .Take(query.Take);

            // 5. Execute query and map to DTOs
            var users = await sortedAndPagedQuery.ToListAsync(ct);
            var userDtos = _mapper.Map<IReadOnlyList<UserDto>>(users);

            // 6. Create and return result
            return PagedResult.Create(userDtos, query.Page, query.Size, totalCount);
        }
    }
    ```

### 3.2. Implementing a Cursor-Based Query

1.  **Define Query:**
    ```csharp
    // In Application layer
    public record GetUserFeedQuery : ICursorPageQuery<CursorPagedResult<UserFeedItemDto>>
    {
        public string? Cursor { get; init; }
        public int Size { get; init; } = 25;
        
        // CRITICAL: Sort order must be deterministic and include a unique key.
        public IReadOnlyList<SortCriteria> SortBy { get; init; } = new[]
        {
            SortCriteria.Descending("CreatedAt"),
            SortCriteria.Descending("Id") // Unique tie-breaker
        };
    }
    ```

2.  **Implement Handler (Conceptual):**
    ```csharp
    // In Application layer
    public class GetUserFeedQueryHandler : IQueryHandler<GetUserFeedQuery, CursorPagedResult<UserFeedItemDto>>
    {
        public async Task<CursorPagedResult<UserFeedItemDto>> Handle(GetUserFeedQuery query, CancellationToken ct)
        {
            var queryable = _feedRepository.GetQueryable();

            // 1. Decode cursor to get the values of the last item from the previous page
            var (lastCreatedAt, lastId) = DecodeCursor(query.Cursor);

            // 2. Build the WHERE clause based on the cursor values and sort order
            if (lastCreatedAt.HasValue && lastId.HasValue)
            {
                // This logic is for (CreatedAt DESC, Id DESC)
                queryable = queryable.Where(item => 
                    item.CreatedAt < lastCreatedAt.Value ||
                    (item.CreatedAt == lastCreatedAt.Value && item.Id < lastId.Value)
                );
            }

            // 3. Apply sorting
            var sortedQuery = queryable.ApplySort(query.SortBy);

            // 4. Fetch one extra item to check if there's a next page
            var items = await sortedQuery.Take(query.Size + 1).ToListAsync(ct);

            // 5. Check for next page and prepare the result set
            bool hasNextPage = items.Count > query.Size;
            var resultItems = items.Take(query.Size).ToList();

            // 6. Generate the next cursor from the last item in the result set
            string? nextCursor = null;
            if (hasNextPage)
            {
                var lastItem = resultItems.Last();
                nextCursor = EncodeCursor(lastItem.CreatedAt, lastItem.Id);
            }
            
            // 7. Map to DTOs and create result
            var dtos = _mapper.Map<IReadOnlyList<UserFeedItemDto>>(resultItems);
            return CursorPagedResult.Create(dtos, nextCursor, hasNextPage);
        }

        // Helper methods for cursor logic
        private string EncodeCursor(DateTime createdAt, Guid id) =>
            Convert.ToBase64String(System.Text.Json.JsonSerializer.Serialize(new { createdAt, id }));

        private (DateTime?, Guid?) DecodeCursor(string? cursor) { /* ... */ }
    }
    ```

---

## 4. Advanced Topics & Best Practices

-   **Database Indexing:** For pagination to be performant, you **must** have database indexes on the columns used for sorting and filtering. For cursor-based pagination, a composite index covering all `SortBy` columns is highly recommended.
-   **Avoiding N+1 Problems:** When mapping your entities to DTOs, be careful to avoid lazy loading that can lead to N+1 queries. Use `Include` (in EF Core) or projections (`Select`) to eagerly load all necessary data in the initial query.
-   **Cursor Stability:** Cursors are based on the values of the sorted fields. If these values change for an item, it can affect pagination. This is why it's best to sort by immutable fields like creation timestamps and unique IDs.
-   **Transaction and Data Snapshots:** For absolute consistency in offset-based pagination, you might consider running the `COUNT(*)` and the data retrieval query within the same transaction with a `SERIALIZABLE` or `SNAPSHOT` isolation level, though this has performance implications. Cursor-based pagination is generally less affected by this issue.
-   **Security:** Always validate and sanitize user-provided filter and sort parameters to prevent SQL injection or other attacks, especially if using raw string interpolation (which should be avoided).

---

## 5. Glossary

-   **Offset Pagination:** A method that uses `SKIP` and `TAKE` (or `OFFSET` and `LIMIT`) to retrieve pages. Simple but can be slow on large datasets.
-   **Cursor (Keyset) Pagination:** A method that uses a "cursor" (a pointer to a specific record) to fetch the next set of items. Highly performant and consistent.
-   **Deterministic Sorting:** A sort order that always returns items in the exact same sequence. This is achieved by including a unique key as the final sort criterion.
-   **Idempotency:** In the context of pagination, this means that requesting the same page multiple times should yield the same result, assuming the underlying data hasn't changed. Cursor pagination is more idempotent than offset pagination in a dynamic dataset.
-   **Opaque Cursor:** A cursor whose internal structure is not meaningful to the client. It should be treated as a simple string to be passed back to the server.