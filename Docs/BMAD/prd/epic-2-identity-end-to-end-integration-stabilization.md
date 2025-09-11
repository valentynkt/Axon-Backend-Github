# Epic 2: Identity End-to-End Integration & Stabilization

**Epic Goal:** Complete the Identity module stabilization by eliminating dual patterns, simplifying over-engineered services, and implementing missing critical functionality (ETag caching, rate limiting).

## Story 2.1: Clean Application Layer - Remove Dual Patterns

**As a** developer maintaining the Identity module,  
**I want** to eliminate dual command/query patterns and route APIs directly to correct handlers,  
**so that** the codebase follows single responsibility and eliminates maintenance confusion.

### Acceptance Criteria

1. Delete entire `/Commands/ExchangeToken/` folder: `ExchangeTokenCommand.cs`, `ExchangeTokenCommandHandler.cs`, `ExchangeTokenCommandValidator.cs`
2. Delete entire `/Queries/GetCurrentUser/` folder: `GetCurrentUserQuery.cs`, `GetCurrentUserQueryHandler.cs`, `GetCurrentUserQueryValidator.cs`  
3. Update `ExchangeEndpoint` to route directly to `ExchangeCredentialCommand` 
4. Update `MeEndpoint` (rename from `GetCurrentUserEndpoint`) to route directly to `GetMyPrincipalQuery`
5. Remove all unused imports and DTOs related to deleted commands/queries
6. Verify both endpoints work end-to-end with single command/query paths

## Story 2.2: Complete ETag Implementation

**As a** client application,  
**I want** HTTP ETag caching on `/auth/me` endpoint,  
**so that** I avoid unnecessary data transfers when my principal data hasn't changed.

### Acceptance Criteria

1. `MeEndpoint` reads `If-None-Match` header and passes to query
2. `GetMyPrincipalQuery` accepts optional `IfNoneMatch` parameter  
3. Handler returns `304 Not Modified` when ETag matches current fingerprint
4. Successful responses include `ETag` header with principal fingerprint
5. Include `Cache-Control` headers for caching guidance
6. Test complete flow: 200 → 304 → data change → 200

## Story 2.3: Add Rate Limiting

**As a** system administrator,  
**I want** rate limiting on `/auth/exchange` to prevent abuse,  
**so that** the authentication system stays stable under load.

### Acceptance Criteria

1. Configure ASP.NET Core rate limiting middleware in `Program.cs`
2. Apply 10 requests/minute per IP to `/auth/exchange` endpoint
3. Return `429 Too Many Requests` with `Retry-After` header
4. Add basic rate limiting metrics
5. Test rate limiting triggers correctly

## Story 2.4: Remove Dead Code 

**As a** developer maintaining clean code,  
**I want** all `EmailHash` and obsolete code removed,  
**so that** the codebase is maintainable and privacy-compliant.

### Acceptance Criteria

1. Delete `EmailHash` value object file completely
2. Remove all `EmailHash` references from source code
3. Clean up unused imports and validation helpers
4. Remove generated artifacts and build warnings
5. Confirm no PII is persisted anywhere

## Story 2.5: **[CRITICAL]** Simplify DynamicAuthService

**As a** developer working with JWT validation,  
**I want** a simplified, well-structured DynamicAuthService with separated concerns,  
**so that** authentication is reliable and maintainable.

### Acceptance Criteria

1. **Extract JWKS caching:** Create `IJwksService` and `JwksService` to handle all JWKS key fetching and caching logic (lines 140-257 in current code)
2. **Simplify main service:** `DynamicAuthService.ValidateTokenAsync` focuses only on JWT validation using injected `IJwksService`
3. **Replace manual retry:** Remove manual retry loops (lines 151-165), use Polly retry policies in `JwksService`
4. **Separate claim logic:** Keep claim normalization in `IDynamicClaimNormalizer`, remove from main validation flow
5. **Single responsibility:** Each service has one clear purpose - JWKS fetching, JWT validation, claim normalization
6. **Add unit tests:** Test each service independently with >90% coverage
7. **Integration test:** End-to-end JWT validation with mocked JWKS endpoint

## Story 2.6: Add Basic Observability

**As a** DevOps engineer,  
**I want** essential logging and metrics for Identity operations,  
**so that** I can monitor production health and troubleshoot issues.

### Acceptance Criteria

1. Add structured logging with correlation IDs to all handlers
2. Add basic metrics: `exchange_success`, `exchange_failure`, `etag_hits`, `etag_misses`
3. Log critical events: JWT validation failures, rate limit hits, wallet conflicts
4. Ensure all logs include correlation IDs for request tracing
