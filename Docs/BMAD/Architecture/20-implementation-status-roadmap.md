# 20. Implementation Status & Roadmap

## Epic 1 (PRD) - Completed Stories ✅

**Story 1.1 - EF Core Model, Configs & Initial Migration**
- ✅ Entities: Principal, Credential, Wallet, WalletOwnership, PrincipalChainDefault
- ✅ Fluent configurations with proper constraints and indexes
- ✅ Initial migration applied
- ✅ ETag fingerprint query implemented

**Story 1.2 - JWT Validation & Rate Limiting Skeleton** 
- ✅ JwtBearerHandler + ConfigurationManager configured
- ✅ Basic JWKS caching implemented
- ✅ Placeholder endpoints created

**Story 1.3 - Domain Aggregates & Invariants**
- ✅ AxonPrincipal aggregate root with invariant enforcement
- ✅ Wallet ownership rules and verified-first defaults
- ✅ Unit tests for domain logic

**Story 1.4 - `/auth/exchange` Command Handler**
- ⚠️ **Partially Complete**: ExchangeCredentialCommand implemented but dual pattern exists
- ✅ Batch wallet operations
- ✅ Transaction handling

**Story 1.5 - `/auth/me` Query + ETag**
- ⚠️ **Partially Complete**: GetMyPrincipalQuery implemented but missing ETag optimization
- ✅ Principal snapshot retrieval

**Story 1.6 - Error Mapping, Privacy, and API Docs**
- ✅ Basic error mapping
- ✅ Privacy compliance (no contact identifiers)
- ⚠️ **Incomplete**: OpenAPI documentation needs updates

## Epic 2 (PRD-2) - Stabilization Requirements ⚡

**Story 2.1 - Clean Application Layer (Remove Dual Patterns)**
- ❌ **Pending**: Remove ExchangeTokenCommand, GetCurrentUserQuery folders
- ❌ **Pending**: Update endpoints to route directly to canonical handlers
- ❌ **Pending**: Clean unused imports and DTOs

**Story 2.2 - Complete ETag Implementation**
- ❌ **Pending**: MeEndpoint If-None-Match header reading
- ❌ **Pending**: 304 Not Modified response handling
- ❌ **Pending**: Cache-Control headers

**Story 2.3 - Add Rate Limiting**
- ❌ **Pending**: ASP.NET Core rate limiting middleware configuration
- ❌ **Pending**: 10 requests/minute per IP for /auth/exchange
- ❌ **Pending**: Rate limiting metrics and headers

**Story 2.4 - Remove Dead Code**
- ❌ **Pending**: Delete EmailHash value object completely
- ❌ **Pending**: Clean unused validation helpers and imports
- ❌ **Pending**: Remove build warnings

**Story 2.5 - [CRITICAL] Simplify DynamicAuthService**
- ❌ **Pending**: Extract IJwksService and JwksService
- ❌ **Pending**: Simplify DynamicAuthService to focus on JWT validation only
- ❌ **Pending**: Replace manual retry with Polly policies
- ❌ **Pending**: Comprehensive unit tests (>90% coverage)

**Story 2.6 - Add Basic Observability**
- ❌ **Pending**: Correlation IDs in all handlers
- ❌ **Pending**: Metrics: exchange_success, exchange_failure, etag_hits, etag_misses
- ❌ **Pending**: Structured logging for critical events

## Current Technical Debt 🔧

1. **Dual Command Patterns**: ExchangeTokenCommand coexists with ExchangeCredentialCommand
2. **Over-Engineered Services**: DynamicAuthService handles too many responsibilities
3. **Missing Infrastructure**: ETag caching, rate limiting, comprehensive observability
4. **Incomplete Repository Methods**: Several contract methods not fully implemented
5. **Dead Code**: EmailHash references and unused validation helpers
