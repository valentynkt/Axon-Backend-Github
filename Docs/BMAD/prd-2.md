# Epic 2: Identity End-to-End Integration & Stabilization - Product Requirements Document (PRD)

## Goals and Background Context

### Goals
- Eliminate dual command/query patterns causing architectural confusion and maintenance overhead
- Complete missing infrastructure components to achieve full end-to-end Identity module functionality  
- Remove all dead code and privacy violations (EmailHash references) from the codebase
- Implement proper caching with ETag support for optimal API performance
- Add comprehensive rate limiting and security measures for production readiness
- Establish complete observability and monitoring for the Identity module
- Achieve clean, maintainable architecture aligned with Clean Architecture + CQRS + DDD principles

### Background Context

The Axon Backend Identity module was implemented during Epic 1 (Stories 1.1-1.6) but lacks production-ready completeness. Analysis revealed critical gaps: dual command patterns (`ExchangeTokenCommand`/`ExchangeCredentialCommand`, `GetCurrentUserQuery`/`GetMyPrincipalQuery`), incomplete infrastructure implementations, missing ETag caching, absent rate limiting, and privacy-violating dead code. These issues prevent end-to-end functionality for the core exchange and me endpoints, creating technical debt that impacts maintainability and production readiness.

This stabilization epic addresses architectural misalignments between API, Application, and Infrastructure layers while completing the missing functionality required for a production-quality Identity module that supports the Solana Co-Pilot platform's authentication and wallet management needs.

### Change Log
| Date | Version | Description | Author |
|------|---------|-------------|---------|
| 2024-01-11 | 1.0 | Initial PRD for Identity Module Stabilization | John (PM) |

## Requirements

### Functional Requirements

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

### Non-Functional Requirements

**NFR1:** All repository operations must maintain sub-200ms response times for hot path queries using compiled EF Core queries and appropriate database indexing

**NFR2:** The system may introduce breaking changes to internal implementations while maintaining clean API contracts for the final working endpoints

**NFR3:** Database migrations must be reversible and execute successfully without data loss, with proper constraint validation and foreign key relationship integrity

**NFR4:** Test coverage must exceed 90% for all business logic components, with integration tests covering complete user workflows and aggressive removal of obsolete test code

**NFR5:** All security measures including JWT validation, rate limiting, and JWKS caching must be production-hardened with appropriate error handling and monitoring

**NFR6:** The refactored codebase must follow Clean Architecture + CQRS + DDD principles with ruthless elimination of architectural violations and single responsibility adherence

**NFR7:** Observability implementation must provide complete request lifecycle tracing with correlation IDs and actionable metrics for operational monitoring

**NFR8:** Code quality must meet project standards with zero compiler warnings, aggressive cleanup of dead code, and comprehensive error handling throughout all layers

## Technical Assumptions

### Repository Structure: Monorepo
The existing modular monolith structure under `src/Modules/Identity` will be maintained, allowing focused work within the Identity module boundaries while leveraging shared BuildingBlocks.

### Service Architecture
**Modular Monolith with Clean Architecture + CQRS + DDD** - Maintaining the established .NET 10 FastEndpoints architecture with MediatR command/query dispatch, ensuring Epic 2 changes align with existing patterns while eliminating dual command violations.

### Testing Requirements
**Full Testing Pyramid** - Comprehensive testing approach with unit tests (>90% coverage for business logic), integration tests using Testcontainers, and end-to-end API tests. NUnit + Shouldly + NSubstitute + Testcontainers stack will be used consistently.

### Additional Technical Assumptions and Requests

- **.NET 10 Preview Maintenance**: Continue using .NET 10 preview 5.25277.114 with FastEndpoints for API layer consistency
- **Entity Framework Core 10**: Leverage compiled queries for hot path optimizations and maintain PostgreSQL as primary database
- **Result Pattern Consistency**: All new code must use `Result<T, Error>` pattern from CSharpFunctionalExtensions for error handling
- **Strong ID Pattern Enforcement**: Maintain type-safe `StrongId<T>` patterns throughout, ensuring no primitive obsession in new implementations
- **OpenTelemetry Integration**: All new handlers must include distributed tracing with correlation IDs for production observability
- **FluentValidation Standards**: All command/query validation must use FluentValidation with consistent error message formatting
- **Polly Resilience**: Replace manual retry logic in DynamicAuthService with Polly policies for standardized resilience patterns
- **System.Text.Json Consistency**: Maintain camelCase naming policy and custom converters for StrongIds and Result types
- **Memory Caching Strategy**: Use Microsoft.Extensions.Caching.Memory for JWKS caching with appropriate expiration policies
- **Rate Limiting Implementation**: Use ASP.NET Core 7+ built-in rate limiting middleware with per-IP tracking
- **Database Migration Strategy**: All schema changes must be reversible with proper foreign key constraints and cascade behaviors
- **Code Style Enforcement**: File-scoped namespaces, target-typed new expressions, and nullable reference types enabled throughout

## Epic List

### Epic 2: Identity End-to-End Integration & Stabilization

*Goal:* Complete the Identity module to achieve production-ready end-to-end functionality with clean architecture alignment, eliminating all dual patterns and dead code while implementing missing infrastructure components.

## Epic 2: Identity End-to-End Integration & Stabilization

**Epic Goal:** Complete the Identity module stabilization by eliminating dual patterns, simplifying over-engineered services, and implementing missing critical functionality (ETag caching, rate limiting).

### Story 2.1: Clean Application Layer - Remove Dual Patterns

**As a** developer maintaining the Identity module,  
**I want** to eliminate dual command/query patterns and route APIs directly to correct handlers,  
**so that** the codebase follows single responsibility and eliminates maintenance confusion.

#### Acceptance Criteria

1. Delete entire `/Commands/ExchangeToken/` folder: `ExchangeTokenCommand.cs`, `ExchangeTokenCommandHandler.cs`, `ExchangeTokenCommandValidator.cs`
2. Delete entire `/Queries/GetCurrentUser/` folder: `GetCurrentUserQuery.cs`, `GetCurrentUserQueryHandler.cs`, `GetCurrentUserQueryValidator.cs`  
3. Update `ExchangeEndpoint` to route directly to `ExchangeCredentialCommand` 
4. Update `MeEndpoint` (rename from `GetCurrentUserEndpoint`) to route directly to `GetMyPrincipalQuery`
5. Remove all unused imports and DTOs related to deleted commands/queries
6. Verify both endpoints work end-to-end with single command/query paths

### Story 2.2: Complete ETag Implementation

**As a** client application,  
**I want** HTTP ETag caching on `/auth/me` endpoint,  
**so that** I avoid unnecessary data transfers when my principal data hasn't changed.

#### Acceptance Criteria

1. `MeEndpoint` reads `If-None-Match` header and passes to query
2. `GetMyPrincipalQuery` accepts optional `IfNoneMatch` parameter  
3. Handler returns `304 Not Modified` when ETag matches current fingerprint
4. Successful responses include `ETag` header with principal fingerprint
5. Include `Cache-Control` headers for caching guidance
6. Test complete flow: 200 → 304 → data change → 200

### Story 2.3: Add Rate Limiting

**As a** system administrator,  
**I want** rate limiting on `/auth/exchange` to prevent abuse,  
**so that** the authentication system stays stable under load.

#### Acceptance Criteria

1. Configure ASP.NET Core rate limiting middleware in `Program.cs`
2. Apply 10 requests/minute per IP to `/auth/exchange` endpoint
3. Return `429 Too Many Requests` with `Retry-After` header
4. Add basic rate limiting metrics
5. Test rate limiting triggers correctly

### Story 2.4: Remove Dead Code 

**As a** developer maintaining clean code,  
**I want** all `EmailHash` and obsolete code removed,  
**so that** the codebase is maintainable and privacy-compliant.

#### Acceptance Criteria

1. Delete `EmailHash` value object file completely
2. Remove all `EmailHash` references from source code
3. Clean up unused imports and validation helpers
4. Remove generated artifacts and build warnings
5. Confirm no PII is persisted anywhere

### Story 2.5: **[CRITICAL]** Simplify DynamicAuthService

**As a** developer working with JWT validation,  
**I want** a simplified, well-structured DynamicAuthService with separated concerns,  
**so that** authentication is reliable and maintainable.

#### Acceptance Criteria

1. **Extract JWKS caching:** Create `IJwksService` and `JwksService` to handle all JWKS key fetching and caching logic (lines 140-257 in current code)
2. **Simplify main service:** `DynamicAuthService.ValidateTokenAsync` focuses only on JWT validation using injected `IJwksService`
3. **Replace manual retry:** Remove manual retry loops (lines 151-165), use Polly retry policies in `JwksService`
4. **Separate claim logic:** Keep claim normalization in `IDynamicClaimNormalizer`, remove from main validation flow
5. **Single responsibility:** Each service has one clear purpose - JWKS fetching, JWT validation, claim normalization
6. **Add unit tests:** Test each service independently with >90% coverage
7. **Integration test:** End-to-end JWT validation with mocked JWKS endpoint

### Story 2.6: Add Basic Observability

**As a** DevOps engineer,  
**I want** essential logging and metrics for Identity operations,  
**so that** I can monitor production health and troubleshoot issues.

#### Acceptance Criteria

1. Add structured logging with correlation IDs to all handlers
2. Add basic metrics: `exchange_success`, `exchange_failure`, `etag_hits`, `etag_misses`
3. Log critical events: JWT validation failures, rate limit hits, wallet conflicts
4. Ensure all logs include correlation IDs for request tracing

## Checklist Results Report

### Executive Summary

- **Overall PRD Completeness:** 85% 
- **MVP Scope Appropriateness:** Just Right (focused stabilization scope)
- **Readiness for Architecture Phase:** Ready (technical constraints are clear)
- **Most Critical Gaps:** Limited user research context (acceptable for stabilization work)

### Category Analysis Table

| Category                         | Status  | Critical Issues |
| -------------------------------- | ------- | --------------- |
| 1. Problem Definition & Context  | PASS    | None - clear stabilization goals |
| 2. MVP Scope Definition          | PASS    | Well-focused on critical fixes |
| 3. User Experience Requirements  | PASS    | Backend-focused, minimal UX impact |
| 4. Functional Requirements       | PASS    | Clear, testable requirements |
| 5. Non-Functional Requirements   | PASS    | Appropriate for infrastructure work |
| 6. Epic & Story Structure        | PASS    | Well-sized, sequential stories |
| 7. Technical Guidance            | PASS    | Leverages existing tech stack |
| 8. Cross-Functional Requirements | PARTIAL | Could benefit from deployment guidance |
| 9. Clarity & Communication       | PASS    | Clear, developer-focused language |

### Top Issues by Priority

**HIGH Priority:**
- Story 2.5 (DynamicAuthService refactoring) is marked CRITICAL but has significant complexity - ensure architect review of service separation approach

**MEDIUM Priority:**  
- Deployment procedure documentation could be more specific for the simplified changes
- Integration between new rate limiting and existing middleware pipeline needs validation

**LOW Priority:**
- Could benefit from more specific error handling patterns for the new services

### MVP Scope Assessment

**Strengths:**
- Focused on completing existing work rather than new features
- Stories are appropriately sized for single developer sessions
- Critical path clearly identified (2.1→2.2→2.3)
- Eliminates technical debt while adding essential functionality

**Scope Validation:**
- All 6 stories directly address identified architectural issues
- No "nice-to-have" features included
- Aggressive cleanup approach is appropriate for stabilization work
- Timeline realistic for experienced .NET team

### Technical Readiness

**Strong Points:**
- Technical constraints are well-defined (leverages existing .NET 10/FastEndpoints stack)
- Architecture patterns established (Clean Architecture + CQRS + DDD)
- Critical refactoring areas clearly identified with current code analysis

**Areas for Architect Investigation:**
- DynamicAuthService separation strategy (Story 2.5) - ensure clean interface boundaries
- Rate limiting middleware integration approach
- ETag fingerprint implementation optimization

### Final Validation Report

#### Critical Deficiencies
**None Identified** - This PRD effectively addresses a focused stabilization scope with clear technical requirements.

#### Recommendations
1. **Architect should review Story 2.5 approach** before implementation due to complexity
2. Consider documenting rollback procedures for the aggressive code cleanup
3. Validate rate limiting configuration doesn't conflict with existing auth pipeline

#### Final Decision
**✅ READY FOR ARCHITECT** - The PRD and epic structure are comprehensive, properly scoped for stabilization work, and provide clear technical guidance for architectural implementation.

## Next Steps

### Architect Prompt
**Architect: Please create detailed implementation architecture for Epic 2: Identity End-to-End Integration & Stabilization. Focus particularly on Story 2.5 DynamicAuthService refactoring - design clean service separation with IJwksService extraction. Use existing .NET 10/FastEndpoints/Clean Architecture patterns. PRD attached provides complete requirements and story breakdown.**

### Implementation Readiness
This PRD is ready for immediate architectural design and subsequent implementation. The focused stabilization scope, clear technical constraints, and well-defined story structure provide a solid foundation for the development phase.