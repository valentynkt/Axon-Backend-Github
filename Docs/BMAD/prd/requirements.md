# Requirements

## Functional Requirements

**FR1:** The system must eliminate dual command patterns by completely removing `ExchangeTokenCommand`, `GetCurrentUserQuery`, and all associated handlers, validators, and DTOs, with `ExchangeEndpoint` and `MeEndpoint` routing directly to the correct single commands

**FR2:** All repository contract methods must be fully implemented including `GetPrincipalFingerprintAsync` with deterministic hash generation, `EnsureManyByChainAndAddressAsync` with proper batch handling, `FindVerifiedSigningOwnersAsync`, and `TouchLastSeenAsync` without ETag impact

**FR3:** The `/auth/me` endpoint must support ETag-based caching by reading `If-None-Match` headers, returning appropriate `ETag` and `Cache-Control` headers, and responding with `304 Not Modified` when content hasn't changed

**FR4:** The `/auth/exchange` endpoint must implement rate limiting at 10 requests per minute per IP address with proper HTTP headers (`Retry-After`, `X-RateLimit-*`) and metrics collection

**FR5:** All `EmailHash` references, privacy-violating dead code, unused value objects, validation helpers, and generated artifacts must be brutally removed from the entire codebase with aggressive cleanup of imports and dependencies

**FR6:** `DynamicAuthService` must be refactored with separated JWKS caching, simplified JWT validation logic, Polly-based retry policies, and comprehensive unit test coverage

**FR7:** Database configuration must include proper composite keys for `PrincipalChainDefaultConfiguration`, foreign key constraints with `RESTRICT` delete behavior, and partial unique indexes for verified+signing ownership

**FR8:** End-to-end integration tests must validate complete exchange flow, idempotency handling, conflict scenarios (409 responses), ETag behavior, and rate limiting functionality

**FR9:** OpenTelemetry tracing must be implemented across all handlers with metrics for exchange success/failure, wallet conflicts, and ETag hits/misses, plus structured logging with correlation IDs

**FR10:** API documentation must be updated with accurate OpenAPI specifications, XML documentation for public APIs, and aggressive code cleanup including unused imports, inconsistent formatting, and obsolete comments

## Non-Functional Requirements

**NFR1:** All repository operations must maintain sub-200ms response times for hot path queries using compiled EF Core queries and appropriate database indexing

**NFR2:** The system may introduce breaking changes to internal implementations while maintaining clean API contracts for the final working endpoints

**NFR3:** Database migrations must be reversible and execute successfully without data loss, with proper constraint validation and foreign key relationship integrity

**NFR4:** Test coverage must exceed 90% for all business logic components, with integration tests covering complete user workflows and aggressive removal of obsolete test code

**NFR5:** All security measures including JWT validation, rate limiting, and JWKS caching must be production-hardened with appropriate error handling and monitoring

**NFR6:** The refactored codebase must follow Clean Architecture + CQRS + DDD principles with ruthless elimination of architectural violations and single responsibility adherence

**NFR7:** Observability implementation must provide complete request lifecycle tracing with correlation IDs and actionable metrics for operational monitoring

**NFR8:** Code quality must meet project standards with zero compiler warnings, aggressive cleanup of dead code, and comprehensive error handling throughout all layers
