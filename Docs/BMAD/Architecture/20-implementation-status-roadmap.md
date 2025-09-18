# 20. Implementation Status & Roadmap

## Epic 3 (Wallet-First Resolution) - ✅ Complete

**Story 3.1 - Wallet-First Principal Resolution**
- ✅ **COMPLETE**: `ResolveOrCreatePrincipalWalletFirst` method fully implemented in `ExchangeCredentialHandler` (lines 276-381)
- ✅ **Repository Infrastructure**: All required methods implemented:
  - `FindVerifiedSigningOwnersAsync` - batch wallet ownership lookup
  - `IsCredentialTakenAsync` - credential conflict detection
  - `FindByCredentialAsync` - fallback credential lookup
- ✅ **4-Step Resolution Process**: Wallet-first → credential fallback → add credential → create new principal

**Story 3.2 - Cross-Credential Identity Linking**
- ✅ **COMPLETE**: Logic implemented to add new credentials to existing principals (lines 328-370)
- ✅ **Conflict Detection**: `IsCredentialTakenAsync` used with proper 409 error mapping (lines 351-367)
- ✅ **Idempotency**: Duplicate credential handling (lines 332-336)

**Story 3.3 - Enhanced Testing for Wallet-First Flow**
- ✅ **COMPLETE**: Comprehensive test coverage including:
  - `Should_ResolveSamePrincipal_When_WalletMatches` - wallet resolution tests
  - `Should_AddCredential_When_WalletMatches` - cross-credential linking tests
  - `Should_ReturnConflictError_When_WalletOwnedByAnotherPrincipal` - conflict handling tests
  - `Should_FallbackToCredentialLookup_When_NoWalletOwners` - fallback logic tests
  - `Should_CreateNewPrincipal_When_NoWalletOwnersAndNoCredential` - new principal creation tests

## Epic 4 (Smart Caching) - Critical Gaps Identified 🚨

**🚨 CRITICAL FINDING**: Epic 4 was missing cache warming implementation - complete performance failure without it.

**Story 4.1 - Rename AxonId to AxonUserId (1 point)**
- ❌ **PENDING**: StronglyTypedId exists but needs brutal refactoring (~26 files affected)
- ❌ **Mass Rename Required**: AxonId → AxonUserId across Identity and domain layers
- ✅ **No Migration**: Database column names remain unchanged (`principal.id`, etc.)
- ✅ **Build System Ready**: Existing patterns support type name changes

**Story 4.2 - Enhanced ICurrentUserService (2 points)**
- ❌ **BRUTAL INTERFACE ENHANCEMENT REQUIRED**: Missing async methods `GetAxonUserIdAsync()`, `TryGetAxonUserId()`
- ❌ **Interface Gap**: No AxonUserId resolution support
- ✅ **Infrastructure**: `IMemoryCache` already registered in both Identity and Chat modules
- ❌ **Implementation Gap**: `HttpContextUserService` has NO caching, NO async methods

**Story 4.3 - Smart Caching Implementation (3 points)**
- ❌ **CRITICAL GAP**: ExchangeCredentialHandler has NO cache warming implementation
- ❌ **HttpContextUserService Gap**: NO caching, NO async methods, NO AxonUserId support
- ❌ **Missing Dependencies**: IMemoryCache and IHttpContextAccessor not injected in ExchangeCredentialHandler
- ✅ **Repository Available**: `FindByCredentialAsync` pattern exists (NO new method needed)
- ❌ **Progressive Cache**: HttpContext.Items → IMemoryCache → Database NOT implemented

**Story 4.4 - Chat Module Integration (1 point)**
- ❌ **BRUTAL COMPLETE REWRITE REQUIRED**: BaseChatCommandHandler.GetAuthenticatedUserId() must be replaced
- ❌ **DELETE DefaultCurrentUserService**: Stub service still exists and needs complete removal
- ❌ **CONVERT ALL Handlers**: All Chat handlers need async AxonUserId pattern
- ❌ **DI Registration**: Chat module still registers DefaultCurrentUserService stub
- ❌ **Domain Model**: Conversation aggregate still uses UserId instead of AxonUserId

## Epic 4 Validation & Testing Status

**Infrastructure Verification (✅ Complete):**
- Memory cache registration confirmed in both modules via `ServiceRegistration.cs`
- Existing cache usage patterns validated (JWKS, replay guard, MCP config)
- `HttpContext.Items` access confirmed in current `HttpContextUserService`
- Repository dependency injection patterns established

**Performance Baseline (⏳ Pending):**
- Current identity resolution latency measurement (~50ms estimated)
- Cache hit ratio monitoring implementation
- Memory usage impact assessment
- Request-scoped vs cross-request performance comparison

**Integration Testing Requirements (⏳ Pending):**
- End-to-end user journey: Exchange → Chat commands using same AxonUserId
- Cache consistency across HTTP requests
- Graceful degradation when cache unavailable
- Backward compatibility with existing Chat module patterns
