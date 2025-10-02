# Epic: FastEndpoints Base Endpoint Refactoring - FINAL

**Epic ID**: EPIC-001
**Status**: ✅ APPROVED - Ready for Implementation
**Priority**: High
**Created**: 2025-10-02
**Last Updated**: 2025-10-02
**Owner**: Valik
**Timeline**: 6-8 days intensive work
**Review Status**: Merged from technical research + code review findings

---

## Executive Summary

### The Problem

The Axon Backend API layer has accumulated **773 lines of infrastructure code** (verified) across a **5-layer inheritance hierarchy** of base endpoint classes, yet only serves **8 concrete endpoints**. This results in an infrastructure-to-endpoint ratio of **~97 lines per endpoint**, indicating severe over-engineering.

**Critical Bug**: 304 Not Modified responses are missing `HttpContext.MarkResponseStart()`, causing FastEndpoints to auto-convert them to 204 NoContent, breaking HTTP caching semantics.

**Verified Infrastructure**:
```
BaseEndpoint.cs:                  63 lines
BaseResultEndpoint.cs:           102 lines
BaseMappedEndpoint.cs:            99 lines
BaseIdentityQueryEndpoint.cs:    273 lines
BaseIdentityCommandEndpoint.cs:   90 lines
BaseChatQueryEndpoint.cs:         73 lines
BaseChatCommandEndpoint.cs:       73 lines
────────────────────────────────────────
TOTAL:                           773 lines ✅
```

**Serving**: 8 endpoints (GetConversations, GetConversationMessages, ChatTurn, Challenge, VerifySignature, Exchange, Refresh, Me)

### Root Cause Analysis

**FastEndpoints is NOT the problem** - our implementation is. Specific issues identified:

1. **Missing `HttpContext.MarkResponseStart()`** (Line 212 in BaseIdentityQueryEndpoint)
   - FastEndpoints checks this flag to prevent double response writes
   - Without it, FastEndpoints auto-converts 304 to 204
   - **Fix**: One-line addition

2. **Response Property Manipulation** (Two Patterns)
   - Pattern A (BaseResultEndpoint.cs:91): `Response = (TResponse)(object)problemDetails` - **LEGITIMATE** (tells FastEndpoints response is handled)
   - Pattern B (BaseIdentityQueryEndpoint.cs:233): `Response = Activator.CreateInstance<TResponse>()` - **WORKAROUND** (should use MarkResponseStart())

3. **Manual Token Expiration Checks** (Lines 64-81 in BaseIdentityQueryEndpoint)
   - **Purpose**: Support FakeTimeProvider for time-travel testing (enables deterministic E2E tests)
   - **Reason**: JWT middleware doesn't respect injected TimeProvider (.NET limitation)
   - **Assessment**: Feature, not bug - enables 12 time-based E2E tests

4. **5-Layer Deep Inheritance Hierarchy**
   - Violates composition over inheritance principle
   - Creates tight coupling and wide impact radius for changes
   - Makes debugging difficult (logic scattered across 5 files)

5. **Unused FastEndpoints Features**
   - Pre-Processors: Could handle authentication, logging
   - Post-Processors: Could handle ETag injection, caching headers
   - Response Interceptors: Could handle 304 logic globally
   - SendAsync() methods: Bypassed with Response property manipulation
   - Built-in ProblemDetails: Partially reimplemented

### Decision: Refactor FastEndpoints Implementation

**✅ APPROVED**: Keep FastEndpoints 7.0.1, execute brutal refactoring

**Rationale**:
- **Score**: 9.0/10 (highest among 4 options evaluated)
- **Time Efficiency**: 6-8 days vs 10-15 days for framework migration
- **Maintainability**: Same target state as alternatives
- **Feature Completeness**: Leverages existing FastEndpoints features
- **Risk**: Lowest (same framework, 57 E2E tests provide comprehensive safety net)

**Alternatives Rejected**:
- Migrate to Minimal APIs: 6.6/10 score, 2x time investment, lose FastEndpoints features
- Migrate to Carter: 6.8/10 score, similar migration cost, smaller ecosystem
- Keep Current: 6.9/10 score, technical debt compounds exponentially

### Expected Impact

**Code Reduction**: 60% (773 → 300-350 lines)
- Delete 3 base classes completely (BaseMappedEndpoint, BaseChatQueryEndpoint, BaseChatCommandEndpoint)
- Simplify BaseIdentityQueryEndpoint from 273 to ~80-100 lines (not delete - retain query execution framework)
- Simplify BaseResultEndpoint from 102 to ~120-150 lines
- Move cross-cutting concerns to processors (~200 lines total)

**Bug Resolution**:
- Fix 304 Not Modified bug (immediate)
- Remove Pattern B Response property manipulation workaround
- Preserve Pattern A (legitimate FastEndpoints usage)
- Preserve manual token expiration checks (enables time-travel testing)

**Architecture Improvement**:
- 5 layers → 2-3 layers (FastEndpoints → BaseResultEndpoint → Concrete Endpoint)
- Inheritance → Composition (pre/post processors)
- Framework fighting → Framework alignment (REPR pattern)

**Developer Experience**:
- New endpoint implementation: Cut from 30min to 10min
- Learning curve: Reduced (2-3 layers vs 5 layers to understand)
- Debugging: Improved (concentrated logic vs scattered across 5 files)
- Onboarding: Easier ("Learn FastEndpoints" vs "Learn our 5-layer custom abstraction")

**Performance**: Neutral to Positive
- Pre/post processors are optimized by FastEndpoints
- Research shows <2% difference between FastEndpoints and Minimal APIs
- May improve due to removing unnecessary abstraction layers

### Timeline & Approach

**Duration**: 6-8 days intensive work (revised from 5-7 days)

**Approach**: Brutal but Safe
- **Brutal**: Delete 3 unnecessary base classes at once, no gradual migration
- **Safe**: Comprehensive testing at every step (57 E2E tests + unit tests)
- **Solo Dev Advantage**: No coordination overhead, can move fast
- **Safety Net**: Git revert + strong test coverage

**Phases**:
1. **Day 1**: Fix bug + design architecture
2. **Days 2-3.5**: Build new foundation (processors + simplified bases)
3. **Days 4-5**: Migrate all 8 endpoints
4. **Days 6-7**: Polish + documentation
5. **Buffer**: +0.5-1 day for ETag processor complexity

**Risk Level**: Low-Medium (mitigated by 57 E2E tests)

---

## Business Value

### Problems Solved
1. **Bug Resolution**: Fix 304 Not Modified responses being converted to 204 NoContent
2. **Technical Debt**: Eliminate 60% of infrastructure code that provides minimal value
3. **Maintainability**: Reduce cognitive load from 5 layers to 2-3 layers
4. **Developer Velocity**: Cut new endpoint implementation time in half
5. **Code Quality**: Remove workarounds and framework-fighting patterns

### Benefits
- **Immediate**: Bug fix (304 responses work correctly)
- **Short-term**: Easier to add new endpoints (8 existing → many more planned)
- **Long-term**: Maintainable codebase, easier onboarding, reduced technical debt compounding

### Risk of Not Doing
- Technical debt compounds (at 50+ endpoints, refactoring becomes painful)
- Workarounds proliferate
- Developer productivity decreases
- Onboarding becomes harder
- Framework benefits remain locked

---

## Technical Context

### System Overview

**Architecture**: Modular Monolith with Clean Architecture + DDD + CQRS + Vertical Slices
**Framework**: .NET 10 Preview + FastEndpoints 7.0.1
**Current State**: 8 API endpoints across 2 modules (Identity, Chat)
**Test Coverage**: 57 E2E tests across 5 test suites (AuthMe, Idempotency, TokenValidation, WalletSignature, ChatTurn)

**Test Breakdown**:
```
AuthMeE2ETests.cs:           14 tests (ETag, 304, caching)
IdempotencyE2ETests.cs:       8 tests (command idempotency)
TokenValidationE2ETests.cs:  12 tests (JWT expiration, FakeTimeProvider)
WalletSignatureE2ETests.cs:   9 tests (Web3 signature verification)
ChatTurnE2ETests.cs:         14 tests (SSE streaming)
────────────────────────────────────────
TOTAL:                       57 E2E tests ✅
```

### Current Architecture (Anti-Pattern)

```
FastEndpoints.Endpoint<TRequest, TResponse>  [FastEndpoints Framework]
│
└── BaseEndpoint<TRequest, TResponse> (63 lines)
    ├─ Logging helpers (LogRequestReceived, LogRequestCompleted, etc.)
    │
    └── BaseResultEndpoint<TRequest, TResponse> (102 lines)
        ├─ Result<T, Error> → HTTP response mapping
        ├─ ProblemDetails error handling
        │
        └── BaseMappedEndpoint<TRequest, TResponse> (99 lines)
            ├─ Mapster mapping integration
            ├─ MapRequest<T>, MapResponse<T>, MapExecuteMap<T>
            │
            ├── BaseIdentityQueryEndpoint (273 lines) ⚠️ LARGEST
            │   ├─ ETag extraction & injection
            │   ├─ 304 Not Modified logic (BUGGY)
            │   ├─ Manual JWT token expiration checks
            │   ├─ Dynamic vs Axon token differentiation
            │   ├─ Structured logging
            │   └─ Response mapping
            │
            ├── BaseIdentityCommandEndpoint (90 lines)
            │   └─ Command-specific Identity logic
            │
            ├── BaseChatQueryEndpoint (73 lines)
            │   └─ Chat query abstractions, pagination
            │
            └── BaseChatCommandEndpoint (73 lines)
                └─ Chat command abstractions, streaming support
                    │
                    └── 8 Concrete Endpoints (10-30 lines each)
```

**Total Infrastructure**: 773 lines across 7 files
**Infrastructure per Endpoint**: ~97 lines

### Target Architecture (REPR Pattern) - REFINED

```
FastEndpoints.Endpoint<TRequest, TResponse>  [FastEndpoints Framework]
│
└── BaseResultEndpoint<TRequest, TResponse> (~120-150 lines, SIMPLIFIED)
    ├─ Responsibilities:
    │   • Result<T, Error> → HTTP response mapping
    │   • ProblemDetails integration
    │   • Query execution framework (CQRS integration)
    │   • SendAsync() usage (no Response property manipulation)
    │
    └── 8 Concrete Endpoints (30-50 lines each)
        ├─ MeEndpoint
        ├─ ChallengeEndpoint
        ├─ VerifySignatureEndpoint
        ├─ ExchangeEndpoint
        ├─ RefreshEndpoint
        ├─ GetConversationsEndpoint
        ├─ GetConversationMessagesEndpoint
        └─ ChatTurnEndpoint (streaming, most complex)
```

**Processors (Composition over Inheritance)**:

**Global Pre-Processors** (~80 lines total):
```
1. LoggingPreProcessor (30 lines)
   • Logs incoming request (method, path, userId)
   • Uses ILogger<T> and Activity for correlation

2. TracingPreProcessor (25 lines)
   • OpenTelemetry activity creation
   • Enriches trace context

3. ETagPreProcessor (25 lines) ⭐ NEW - CRITICAL
   • Extract client ETag from If-None-Match header
   • Store in HttpContext.Items for post-processor comparison
   • Enables early 304 detection (performance optimization)
```

**Global Post-Processors** (~120 lines total):
```
1. ETagPostProcessor (70 lines) ⭐ REVISED - More complex than original plan
   • Extract ETag from response entity (IHaveETag interface)
   • Compare with client ETag from HttpContext.Items
   • If match: Call MarkResponseStart(), send 304 with empty body
   • If no match: Inject ETag header into response
   • ✅ FIXES 304 BUG

2. CachingHeadersPostProcessor (30 lines)
   • Inject Cache-Control headers
   • Inject Vary headers
   • Based on endpoint configuration

3. ProblemDetailsPostProcessor (20 lines, OPTIONAL)
   • Global error formatting
   • May be redundant with BaseResultEndpoint
```

**Module-Specific Processors** (~100 lines total):
```
1. IdentityAuthProcessor (80-100 lines) [Pre-Processor]
   • Applied only to Identity endpoints
   • Handles Dynamic vs Axon token differentiation
   • Validates token expiration (FakeTimeProvider support for tests) ⭐
   • Enriches HttpContext.User with token metadata
   • ✅ PRESERVES time-travel testing capability

2. ChatRateLimitProcessor (20-30 lines) [Pre-Processor, FUTURE]
   • Applied only to Chat endpoints
   • Rate limiting logic specific to chat
```

**Total Processor Code**: ~300-350 lines (vs 773 lines current)

**Deleted Files** (3 base classes):
```
✂️ DELETE: BaseMappedEndpoint.cs (99 lines)
✂️ DELETE: BaseChatQueryEndpoint.cs (73 lines)
✂️ DELETE: BaseChatCommandEndpoint.cs (73 lines)

Total Deleted: ~245 lines
```

**Modified Files** (4 base classes):
```
📝 MERGE: BaseEndpoint.cs → BaseResultEndpoint.cs
   • Logging methods absorbed into BaseResultEndpoint

📝 SIMPLIFY: BaseResultEndpoint.cs (102 → ~120-150 lines)
   • Keep: Result<T, Error> mapping only
   • Remove: Logging (move to processor)
   • Add: Query execution framework (from BaseMappedEndpoint)
   • Add: Proper use of SendAsync() methods
   • Add: XML documentation for proper usage

📝 SIMPLIFY: BaseIdentityQueryEndpoint.cs (273 → ~80-100 lines) ⚠️ REVISED
   • ❌ DON'T DELETE entirely (original plan)
   • ✅ SIMPLIFY instead (pragmatic approach)
   • Extract: ETag logic → ETag Pre/Post Processors
   • Extract: Auth validation → IdentityAuthProcessor
   • Extract: Logging → LoggingPreProcessor
   • Keep: Query execution & mapping integration (~80-100 lines)
   • Justification: Provides valuable CQRS integration framework

📝 DELETE: BaseIdentityCommandEndpoint.cs (90 lines)
   • Minimal value, logic can be inline in endpoints
```

### Key Technical Issues - Detailed Analysis

#### Issue 1: 304 Not Modified Bug (CRITICAL) ✅ VERIFIED

**Location**: `src/Api/Modules/BaseIdentityQueryEndpoint.cs:212`

**Current Code** (broken):
```csharp
private Task HandleNotModifiedResponse(string etag, CancellationToken _)
{
    Logger.LogDebug("Returning 304 Not Modified for ETag: {ETag}", etag);

    HttpContext.Response.Headers.ETag = $"\"{etag}\"";
    HttpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
    HttpContext.Response.ContentLength = 0;
    HttpContext.Response.StatusCode = StatusCodes.Status304NotModified;

    // ❌ MISSING: HttpContext.MarkResponseStart();

    // Workaround (Pattern B - should be removed)
    try
    {
        Response = Activator.CreateInstance<TResponse>();
    }
    catch
    {
        Response = (TResponse)(object)new object();
    }

    return Task.CompletedTask;
}
```

**Root Cause**:
- FastEndpoints tracks response state via `HttpContext.HasResponseStarted()` flag
- Without `MarkResponseStart()`, FastEndpoints assumes endpoint didn't send response
- Auto-fallback logic sends 204 NoContent (per FastEndpoints conventions)

**Fix (one line)**:
```csharp
private Task HandleNotModifiedResponse(string etag, CancellationToken _)
{
    HttpContext.MarkResponseStart(); // ✅ Tell FastEndpoints we're handling response
    HttpContext.Response.StatusCode = 304;
    HttpContext.Response.Headers.ETag = $"\"{etag}\"";
    HttpContext.Response.ContentLength = 0;
    await HttpContext.Response.CompleteAsync();
}
```

**Better Fix** (use SendAsync):
```csharp
private async Task HandleNotModifiedResponse(string etag, CancellationToken ct)
{
    await SendAsync(new EmptyResponse(), 304, ct); // ✅ FastEndpoints handles everything
    HttpContext.Response.Headers.ETag = $"\"{etag}\"";
}
```

**Best Fix** (move to ETag Post-Processor) ⭐ FINAL APPROACH:
```csharp
// In ETagPostProcessor.PostProcessAsync()
public async Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> ctx, CancellationToken ct)
{
    if (ctx.Response is not IHaveETag etagEntity || string.IsNullOrEmpty(etagEntity.ETag))
        return;

    var serverETag = etagEntity.ETag;
    var clientETag = ctx.HttpContext.Items["ClientETag"]?.ToString();

    if (!string.IsNullOrEmpty(clientETag) && clientETag == serverETag)
    {
        // Client has current version - return 304
        ctx.HttpContext.MarkResponseStart(); // ✅ FIX THE BUG
        ctx.HttpContext.Response.StatusCode = 304;
        ctx.HttpContext.Response.Headers.ETag = $"\"{serverETag}\"";
        ctx.HttpContext.Response.ContentLength = 0;
        await ctx.HttpContext.Response.WriteAsync("", ct);
    }
    else
    {
        // ETags don't match - send full response
        ctx.HttpContext.Response.Headers.ETag = $"\"{serverETag}\"";
        ctx.HttpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
    }
}
```

**Test Coverage**:
- Test: `AuthMe_IfNoneMatchWithCurrentETag_ShouldReturn304` (AuthMeE2ETests.cs:88)
- Validator: `AuthMeResponseValidator.ValidateNotModifiedResponse`

**Reference**: [FastEndpoints GitHub Issue #230](https://github.com/FastEndpoints/FastEndpoints/issues/230)

#### Issue 2: Response Property Patterns (TWO DISTINCT PATTERNS)

**Pattern A (LEGITIMATE)**: BaseResultEndpoint.cs:91
```csharp
private async Task SendProblemDetailsAsync(Error error, CancellationToken ct)
{
    var problemDetails = error.ToProblemDetails(...);
    HttpContext.Response.StatusCode = problemDetails.Status ?? 500;
    HttpContext.Response.ContentType = "application/problem+json";

    // ✅ LEGITIMATE: Tell FastEndpoints response is handled
    Response = (TResponse)(object)problemDetails;

    await HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: ct);
}
```

**Assessment**: This is **CORRECT FastEndpoints usage**. After writing to response stream manually, setting Response property tells FastEndpoints the response is handled.

**Pattern B (WORKAROUND)**: BaseIdentityQueryEndpoint.cs:233
```csharp
private Task HandleNotModifiedResponse(string etag, CancellationToken _)
{
    HttpContext.Response.StatusCode = 304;

    // ❌ WORKAROUND: Should use MarkResponseStart() instead
    Response = Activator.CreateInstance<TResponse>();

    return Task.CompletedTask;
}
```

**Assessment**: This is a **workaround for missing MarkResponseStart()**. Should be removed when moving to processor.

**Action Plan**:
- ✅ **KEEP Pattern A** (legitimate, documented in FastEndpoints best practices)
- ❌ **REMOVE Pattern B** (replaced by MarkResponseStart() in processor)

#### Issue 3: Manual Token Expiration Checks (FEATURE, NOT BUG) ⭐

**Location**: `src/Api/Modules/BaseIdentityQueryEndpoint.cs:64-81`

**Current Code**:
```csharp
// Additional validation: Check token expiration manually using application TimeProvider
// This is necessary because JWT middleware may not honor FakeTimeProvider in test environments
// In production, this provides defense-in-depth alongside middleware validation
var expClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
if (!string.IsNullOrEmpty(expClaim) && long.TryParse(expClaim, out var expUnix))
{
    var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix);
    var now = HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();

    if (expDate < now)
    {
        Logger.LogWarning("Token expired: exp={ExpDate}, now={Now}", expDate, now);
        await HttpContext.SendProblemDetailsAsync(
            Error.Unauthorized("Token has expired", "AUTH.TOKEN_EXPIRED"), ct);
        return;
    }
}
```

**Why This Exists**:
1. ✅ **Time-Travel Testing**: FakeTimeProvider enables deterministic E2E tests
2. ✅ **.NET Limitation**: JWT middleware doesn't use injected TimeProvider (uses DateTime.UtcNow)
3. ✅ **Defense-in-Depth**: Provides redundant validation in production

**Test Coverage Enabled** (12 tests in TokenValidationE2ETests.cs):
- `AuthMe_WithExpiredToken_ShouldReturn401`
- `AuthMe_TokenExpiryAt5PMToday_ShouldWorkBeforeExpiry`
- `AuthMe_TokenExpiryAt5PMToday_ShouldFailAfterExpiry`
- ... 9 more time-based tests

**Assessment**: This is a **FEATURE that enables critical testing**, not a workaround to remove.

**Action Plan**:
- ✅ **PRESERVE** this logic (move to IdentityAuthProcessor)
- ✅ **DOCUMENT** clearly that this enables time-travel testing
- ⚠️ **FUTURE**: Consider custom ISystemClock implementation (alternative approach)

**Proper Solution** (move to processor):
```csharp
// In IdentityAuthProcessor.PreProcessAsync()
public class IdentityAuthProcessor : IPreProcessor<EmptyRequest>
{
    private readonly TimeProvider _timeProvider;

    public async Task PreProcessAsync(IPreProcessorContext<EmptyRequest> ctx, CancellationToken ct)
    {
        // Extract exp claim
        var expClaim = ctx.HttpContext.User.FindFirst("exp");
        if (expClaim != null && long.TryParse(expClaim.Value, out var exp))
        {
            var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp);
            var now = _timeProvider.GetUtcNow();

            if (now >= expirationTime)
            {
                await ctx.HttpContext.SendUnauthorizedAsync(ct);
                return; // Short-circuit
            }
        }

        // Differentiate Dynamic vs Axon token
        var tokenType = ctx.HttpContext.User.FindFirst("token_type")?.Value;
        ctx.HttpContext.Items["TokenType"] = tokenType;
    }
}
```

#### Issue 4: Deep Inheritance Hierarchy (MAINTENANCE NIGHTMARE)

**Problem**: 5 layers create cognitive overhead and tight coupling

**Example Developer Flow** (current):
```
Developer wants to add logging to MeEndpoint:
1. Open MeEndpoint.cs - no logging code here
2. Check BaseIdentityQueryEndpoint.cs - some logging, but not what I need
3. Check BaseMappedEndpoint.cs - no logging
4. Check BaseResultEndpoint.cs - some logging
5. Check BaseEndpoint.cs - ah, there's the ILogger
6. Modify BaseEndpoint.cs - but wait, this affects all 8 endpoints!
7. Create override in MeEndpoint? Or new method in BaseIdentityQueryEndpoint?
8. Spend 30 minutes understanding where to put code
```

**Target Developer Flow**:
```
Developer wants to add logging to MeEndpoint:
1. Open LoggingPreProcessor.cs - all logging logic here
2. Add endpoint-specific logging if needed
3. Or add logging directly in MeEndpoint for one-off scenarios
4. Change isolated, no impact on other endpoints
```

#### Issue 5: Unused FastEndpoints Features

**Features We're Not Using** (but should):

1. **Pre/Post Processors**: Cross-cutting concerns (auth, logging, ETag)
2. **Response Interceptors**: Global 304 handling
3. **SendAsync() family**: SendOkAsync, SendNoContentAsync, SendCreatedAtAsync
4. **Global Configuration**: Most base class logic could be global processors
5. **Built-in ProblemDetails**: FastEndpoints has native support (RFC 7807)
6. **Source Generators**: Boilerplate reduction (FastEndpoints.Generator package)
7. **Testing Utilities**: Factory methods for cleaner tests

**Impact**: Reimplementing what framework provides, fighting instead of leveraging

### Technology Stack

**Fixed (Cannot Change)**:
- .NET 10 Preview
- FastEndpoints 7.0.1
- MediatR (CQRS command/query handling)
- FluentValidation (request validation)
- Result Pattern (CSharpFunctionalExtensions)
- PostgreSQL + EF Core 9
- OpenTelemetry (distributed tracing)

**Refactoring Scope**:
- API layer only (endpoint base classes + processors)
- No changes to application/domain layers
- No changes to API contracts (internal refactor only)

---

## Scope

### In Scope
- ✅ Fix 304 Not Modified bug (`MarkResponseStart()` addition)
- ✅ Delete 3 unnecessary base classes (BaseMappedEndpoint, BaseChat*)
- ✅ Simplify BaseIdentityQueryEndpoint to ~80-100 lines (not delete)
- ✅ Simplify BaseResultEndpoint to ~120-150 lines
- ✅ Create pre/post processors for cross-cutting concerns
- ✅ Migrate all 8 endpoints to new pattern
- ✅ Remove Pattern B Response property workaround (keep Pattern A)
- ✅ Preserve manual token expiration checks (move to processor)
- ✅ Update engineering documentation
- ✅ Comprehensive testing (57 E2E tests must pass)

### Out of Scope
- ❌ Adding new endpoints (focus on refactoring existing)
- ❌ Changing API contracts (internal refactor only)
- ❌ Framework migration (staying with FastEndpoints 7.0.1)
- ❌ Backward compatibility (brutal approach, solo dev)
- ❌ Gradual migration (all at once)
- ❌ Custom ISystemClock implementation (future enhancement)

---

## Epic Breakdown

### Phase 1: Fix Bug + Design (Day 1) - Story 1.1

**Story ID**: STORY-1.1
**Story Title**: Fix 304 Not Modified Bug and Design Target Architecture
**Priority**: Critical (blocks other phases)
**Estimated Effort**: 8 hours (1 day)

#### Story Overview

The first step in the refactoring epic is to immediately fix the critical 304 Not Modified bug and design the target architecture. This phase has zero dependencies on later phases and delivers immediate value (bug fix) while establishing the blueprint for the remaining work.

**Key Deliverables**:
1. 304 bug fixed and verified
2. Complete FastEndpoints knowledge acquisition (pre/post processors)
3. Target architecture designed (2-layer hierarchy + processors)
4. Base class audit complete (decisions documented)
5. Processor responsibilities defined

#### Detailed Task Breakdown

##### Morning (4 hours)

**Task 1.1.1: Fix 304 Not Modified Bug** (30 minutes)
- **File**: `src/Api/Modules/BaseIdentityQueryEndpoint.cs`
- **Method**: `HandleNotModifiedResponse` (Line 212)
- **Change**:
  ```csharp
  // BEFORE (broken)
  private Task HandleNotModifiedResponse(string etag, CancellationToken _)
  {
      // ... header setup ...
      HttpContext.Response.StatusCode = StatusCodes.Status304NotModified;

      // ❌ MISSING: HttpContext.MarkResponseStart();

      Response = Activator.CreateInstance<TResponse>(); // Workaround
      return Task.CompletedTask;
  }

  // AFTER (fixed)
  private Task HandleNotModifiedResponse(string etag, CancellationToken _)
  {
      HttpContext.MarkResponseStart(); // ✅ ONE LINE FIX
      HttpContext.Response.StatusCode = StatusCodes.Status304NotModified;
      HttpContext.Response.Headers.ETag = $"\"{etag}\"";
      HttpContext.Response.ContentLength = 0;
      // Remove Response property workaround (no longer needed)
      return Task.CompletedTask;
  }
  ```

- **Actions**:
  1. Open `BaseIdentityQueryEndpoint.cs`
  2. Navigate to line 212 (`HandleNotModifiedResponse` method)
  3. Add `HttpContext.MarkResponseStart();` as first line in method
  4. Remove `Response = Activator.CreateInstance<TResponse>()` workaround
  5. Save file
  6. Run all E2E tests: `dotnet test --filter "Category=E2E"`
  7. Verify 57/57 tests pass (updated from 14)
  8. Manually test MeEndpoint with ETag via Swagger
  9. Commit: `fix: Add MarkResponseStart to fix 304 Not Modified bug`

- **Acceptance Criteria**:
  - ✅ All 57 E2E tests passing
  - ✅ MeEndpoint returns 304 when ETag matches (manual verification)
  - ✅ Test `AuthMe_IfNoneMatchWithCurrentETag_ShouldReturn304` passes
  - ✅ Clean commit pushed to feature branch

**Task 1.1.2: Study FastEndpoints Pre/Post Processors** (2 hours)
- **Objective**: Deep understanding of FastEndpoints processor architecture

- **Resources**:
  - FastEndpoints Docs: https://fast-endpoints.com/docs/pre-post-processors
  - FastEndpoints Docs: https://fast-endpoints.com/docs/configuration-settings
  - GitHub Issue #230: https://github.com/FastEndpoints/FastEndpoints/issues/230
  - GitHub Issue #771: Flexible Error Response Control
  - GitHub Issue #795: ETag Header with Response Type

- **Study Areas**:
  1. **Pre-Processors**:
     - Execution order (BEFORE endpoint HandleAsync)
     - Access to request, HttpContext, cancellation token
     - Short-circuiting (return early, skip endpoint)
     - Global vs endpoint-specific processors
     - Registration patterns in Program.cs
     - **Use Case**: Extract client ETag for comparison

  2. **Post-Processors**:
     - Execution order (AFTER endpoint HandleAsync, BEFORE serialization)
     - Access to response, HttpContext, cancellation token
     - Response modification capabilities
     - Error handling in processors
     - **Use Case**: Inject ETag, handle 304 responses

  3. **Response Interceptors**:
     - Global response manipulation
     - 304 handling examples
     - When to use vs post-processors

  4. **MarkResponseStart()**:
     - When to call (before manually writing to response)
     - Why it's needed (prevents FastEndpoints auto-204)
     - Relationship to SendAsync() methods

- **Deliverable**: Notes document (`Day1-FastEndpoints-Study-Notes.md`)
  - Key patterns identified
  - Code examples for our use cases (ETag Pre+Post, Auth Pre, Logging Pre)
  - Gotchas and edge cases
  - Pre-processor + Post-processor combination patterns

- **Acceptance Criteria**:
  - ✅ Understand pre/post processor lifecycle
  - ✅ Identify 3-5 code examples relevant to our refactoring
  - ✅ Document GitHub issues related to our bugs
  - ✅ Clear mental model of processor execution order
  - ✅ Understand when to use Pre vs Post processors

**Task 1.1.3: Design 2-Layer Target Architecture** (1.5 hours)
- **Objective**: Visual diagram + written specification of target state

- **Actions**:
  1. Create architecture diagram (ASCII or draw.io)
  2. Define hierarchy:
     - Layer 1: FastEndpoints.Endpoint<TRequest, TResponse>
     - Layer 2: BaseResultEndpoint<TRequest, TResponse> (~120-150 lines)
     - Layer 3: Concrete endpoints (8 total, 30-50 lines each)
  3. Define BaseResultEndpoint responsibilities:
     - Result<T, Error> → HTTP response mapping
     - CQRS query execution framework
     - Methods: ExecuteAsync, HandleAsync, SendAsync usage
     - Remove: All logging, auth, ETag
     - Keep: Query execution pattern from BaseMappedEndpoint
  4. Define processor architecture:
     - Global pre-processors: Logging (30 lines), Tracing (25 lines), ETagPre (25 lines)
     - Global post-processors: ETagPost (70 lines), CachingHeaders (30 lines)
     - Module processors: IdentityAuth (80-100 lines)
  5. Document processor registration pattern in Program.cs
  6. Create before/after comparison diagram
  7. Document BaseMappedEndpoint deletion strategy (inline mapping in endpoints)

- **Deliverable**: `Day1-Target-Architecture.md` with diagrams

- **Acceptance Criteria**:
  - ✅ Clear visual diagram of 2-layer hierarchy
  - ✅ Processor responsibilities defined (Pre + Post for ETag)
  - ✅ Before/after comparison shows 60% code reduction path
  - ✅ Registration pattern documented
  - ✅ BaseMappedEndpoint migration strategy (inline Mapster)

##### Afternoon (4 hours)

**Task 1.1.4: Audit All 7 Base Classes** (2 hours)
- **Objective**: Methodical analysis of every method in every base class
- **Process**: For each base class, analyze each method and tag decision

**Base Class 1: BaseEndpoint.cs** (63 lines)
- Read entire file line by line
- Tag each method/property:
  - **MOVE_TO_PROCESSOR**: LogRequestReceived, LogRequestCompleted, LogRequestFailed, LogRequestCancelled
  - **DELETE**: Entire file (merge into BaseResultEndpoint)
- Decision: **DELETE** - Merge logging into LoggingPreProcessor

**Base Class 2: BaseResultEndpoint.cs** (102 lines)
- Current size: 102 lines
- Target size: ~120-150 lines (will increase slightly to absorb BaseEndpoint + query framework)
- Tag each method:
  - ExecuteAsync: **KEEP** (core abstraction)
  - HandleAsync: **KEEP** but **SIMPLIFY** (remove Pattern B Response manipulation, keep Pattern A)
  - Result mapping: **KEEP** (core value)
  - Error handling: **KEEP** (Pattern A is legitimate)
  - Logging: **MOVE_TO_PROCESSOR**
  - **ADD**: Query execution framework from BaseMappedEndpoint
- Decision: **SIMPLIFY and EXPAND**

**Base Class 3: BaseMappedEndpoint.cs** (99 lines)
- Auto-mapping logic (MapRequest, MapResponse, MapExecuteMap)
- Decision: **DELETE** entirely
- Justification: 99 lines for 8 endpoints is overkill, inline mapping is clearer
- Migration: Replace with `request.Adapt<TQuery>()` directly in endpoints (3-5 lines)

**Base Class 4: BaseIdentityQueryEndpoint.cs** (273 lines) ⚠️ REVISED
- Current size: 273 lines
- Target size: ~80-100 lines
- Tag each method/section:
  - Lines 49-82 (Auth validation): **MOVE_TO_PROCESSOR** (IdentityAuthProcessor)
  - Lines 83-147 (ETag handling): **MOVE_TO_PROCESSOR** (ETag Pre+Post Processors)
  - Lines 150-187 (Query execution & mapping): **KEEP** (endpoint-specific CQRS integration)
  - Lines 189-207 (Success response): **PARTIALLY MOVE** (headers to processor)
  - Lines 209-243 (304 handling): **MOVE_TO_PROCESSOR** (ETagPostProcessor)
  - Abstract methods: **KEEP** (configuration hooks)
- Decision: **SIMPLIFY** (not DELETE)
- Justification: Provides valuable query execution framework for CQRS pattern

**Base Class 5: BaseIdentityCommandEndpoint.cs** (90 lines)
- Similar to BaseIdentityQueryEndpoint but for commands
- Decision: **DELETE** entirely
- Justification: Minimal value, can inline in command endpoints

**Base Class 6: BaseChatQueryEndpoint.cs** (73 lines)
- Pagination logic
- Decision: **DELETE** base class
- Justification: Inline pagination in endpoints (domain-specific)

**Base Class 7: BaseChatCommandEndpoint.cs** (73 lines)
- Streaming support (SSE)
- Decision: **DELETE** base class
- Justification: Keep streaming logic in ChatTurnEndpoint directly (complex, endpoint-specific)

**Deliverable**: `Day1-Base-Class-Audit.md`
- Table format: File | Line Count | Methods | Decision | Justification
- Summary:
  - **DELETE**: 3 files (BaseMappedEndpoint, BaseChatQueryEndpoint, BaseChatCommandEndpoint) = 245 lines
  - **MERGE**: BaseEndpoint → BaseResultEndpoint
  - **SIMPLIFY**: BaseIdentityQueryEndpoint (273 → ~80-100 lines)
  - **SIMPLIFY**: BaseResultEndpoint (102 → ~120-150 lines, absorbs BaseEndpoint + query framework)
  - **DELETE**: BaseIdentityCommandEndpoint = 90 lines
- Total line reduction: 773 → ~320-350 lines infrastructure

**Acceptance Criteria**:
- ✅ All 7 base classes analyzed
- ✅ Every method tagged (KEEP/MOVE/DELETE)
- ✅ Deletion vs simplification decisions justified
- ✅ Total line reduction calculated (773 → ~320-350)

**Task 1.1.5: Design Processor Responsibilities** (2 hours)
- **Objective**: Detailed spec for each processor to be built in Phase 2

**Processor 1: LoggingPreProcessor** (Global Pre-Processor, 30 lines)
- **File**: `src/Api/Processors/LoggingPreProcessor.cs`
- **Responsibilities**:
  - Log incoming request (method, path, userId, correlationId)
  - Use ILogger<T> and Activity for structured logging
  - Extract userId from HttpContext.User
  - Add to Activity tags
- **Interface**: `IPreProcessor<TRequest>`
- **Apply To**: All endpoints (global registration)
- **Dependencies**: ILogger, Activity
- **Test Plan**: Unit test with NSubstitute ILogger

**Processor 2: TracingPreProcessor** (Global Pre-Processor, 25 lines)
- **File**: `src/Api/Processors/TracingPreProcessor.cs`
- **Responsibilities**:
  - Create OpenTelemetry activity
  - Enrich trace context (endpoint name, userId, requestId)
  - Set activity tags
- **Interface**: `IPreProcessor<TRequest>`
- **Apply To**: All endpoints (global registration)
- **Dependencies**: ActivitySource, HttpContext
- **Test Plan**: Unit test activity creation

**Processor 3: ETagPreProcessor** (Global Pre-Processor, 25 lines) ⭐ CRITICAL
- **File**: `src/Api/Processors/ETagPreProcessor.cs`
- **Estimated Lines**: 25
- **Responsibilities**:
  - Extract client ETag from If-None-Match header
  - Store in HttpContext.Items["ClientETag"] for post-processor
  - Early detection setup (performance optimization)
- **Interface**: `IPreProcessor<TRequest>`
- **Apply To**: Endpoints with ETag support
- **Dependencies**: HttpContext
- **Test Plan**: Unit test + E2E test for ETag extraction
- **Key Code**:
  ```csharp
  public async Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct)
  {
      if (ctx.HttpContext.Request.Headers.TryGetValue("If-None-Match", out var clientETag))
      {
          // Store unquoted ETag for comparison
          ctx.HttpContext.Items["ClientETag"] = clientETag.ToString().Trim('"');
      }
  }
  ```

**Processor 4: ETagPostProcessor** (Global Post-Processor, 70 lines) ⭐ CRITICAL
- **File**: `src/Api/Processors/ETagPostProcessor.cs`
- **Estimated Lines**: 70 (more complex than original estimate)
- **Responsibilities**:
  - Extract ETag from response entity (IHaveETag interface)
  - Compare with client ETag from HttpContext.Items["ClientETag"]
  - If match: Call MarkResponseStart(), send 304 with empty body
  - If no match: Inject ETag header into response
  - Short-circuit serialization if 304
- **Interface**: `IPostProcessor<TRequest, TResponse>`
- **Apply To**: Endpoints with ETag support (MeEndpoint)
- **Dependencies**: HttpContext, Response entity
- **Test Plan**: Unit test + E2E test for 304 responses
- **Key Code**:
  ```csharp
  public async Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> ctx, CancellationToken ct)
  {
      // Only process if response implements IHaveETag
      if (ctx.Response is not IHaveETag etagEntity || string.IsNullOrEmpty(etagEntity.ETag))
          return;

      var serverETag = etagEntity.ETag;
      var clientETag = ctx.HttpContext.Items["ClientETag"]?.ToString();

      // Check if ETags match (client has current version)
      if (!string.IsNullOrEmpty(clientETag) && clientETag == serverETag)
      {
          // Client has current version - return 304
          ctx.HttpContext.MarkResponseStart(); // ✅ FIX THE BUG
          ctx.HttpContext.Response.StatusCode = 304;
          ctx.HttpContext.Response.Headers.ETag = $"\"{serverETag}\"";
          ctx.HttpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
          ctx.HttpContext.Response.ContentLength = 0;

          // Clear response body
          await ctx.HttpContext.Response.WriteAsync("", ct);
      }
      else
      {
          // ETags don't match - send full response with new ETag
          ctx.HttpContext.Response.Headers.ETag = $"\"{serverETag}\"";
          ctx.HttpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
      }
  }
  ```

**Processor 5: CachingHeadersPostProcessor** (Global Post-Processor, 30 lines)
- **File**: `src/Api/Processors/CachingHeadersPostProcessor.cs`
- **Estimated Lines**: 30
- **Responsibilities**:
  - Inject Cache-Control headers (based on endpoint config)
  - Inject Vary headers (Vary: Accept, Authorization)
  - Support endpoint-level caching policies
- **Interface**: `IPostProcessor<TRequest, TResponse>`
- **Apply To**: Endpoints with caching configured
- **Dependencies**: HttpContext, endpoint metadata
- **Test Plan**: Unit test header injection

**Processor 6: IdentityAuthProcessor** (Module-Specific Pre-Processor, 80-100 lines)
- **File**: `src/Api/Modules/Identity/Processors/IdentityAuthProcessor.cs`
- **Estimated Lines**: 80-100
- **Responsibilities**:
  - Validate token expiration (FakeTimeProvider support for tests) ⭐
  - Differentiate Dynamic vs Axon tokens
  - Enrich HttpContext.User with token metadata
  - Short-circuit with 401 if invalid
- **Interface**: `IPreProcessor<TRequest>`
- **Apply To**: Identity endpoints only
- **Dependencies**: TimeProvider (injected), HttpContext.User
- **Test Plan**: Unit test + E2E test with FakeTimeProvider (12 time-based tests)
- **Key Code** (extract from BaseIdentityQueryEndpoint.cs:64-81):
  ```csharp
  public async Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct)
  {
      // Extract exp claim
      var expClaim = ctx.HttpContext.User.FindFirst("exp");
      if (expClaim != null && long.TryParse(expClaim.Value, out var exp))
      {
          var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp);
          var now = _timeProvider.GetUtcNow(); // ✅ PRESERVES time-travel testing

          if (now >= expirationTime)
          {
              _logger.LogWarning("Token expired: exp={ExpDate}, now={Now}", expirationTime, now);
              await ctx.HttpContext.SendUnauthorizedAsync(ct);
              return; // Short-circuit
          }
      }

      // Differentiate Dynamic vs Axon token
      var tokenType = ctx.HttpContext.User.FindFirst("token_type")?.Value;
      ctx.HttpContext.Items["TokenType"] = tokenType; // Enrich for endpoint
  }
  ```

**Deliverable**: `Day1-Processor-Specifications.md`
- Each processor: Name, file path, responsibilities, interface, dependencies, test plan, key code
- Total estimated lines: ~300-350
- Registration pattern in Program.cs
- ETag Pre+Post combination explained

**Acceptance Criteria**:
- ✅ All 6 processors specified in detail (added ETagPre)
- ✅ Interfaces identified
- ✅ Dependencies documented
- ✅ Test plans outlined (57 E2E tests + new processor unit tests)
- ✅ Code reduction validated (773 → 320-350 lines)
- ✅ FakeTimeProvider preservation documented

#### Success Criteria (Day 1)

**Code Changes**:
- ✅ 304 bug fixed (MarkResponseStart() added)
- ✅ All 57 E2E tests passing (updated from 14)
- ✅ Clean commit pushed

**Documentation**:
- ✅ `Day1-FastEndpoints-Study-Notes.md` created
- ✅ `Day1-Target-Architecture.md` created (with diagrams)
- ✅ `Day1-Base-Class-Audit.md` created (detailed table with revised decisions)
- ✅ `Day1-Processor-Specifications.md` created (6 processors specified, including ETagPre)

**Knowledge**:
- ✅ Deep understanding of FastEndpoints processors (Pre + Post patterns)
- ✅ Clear mental model of target architecture
- ✅ Decisions documented for all 7 base classes (revised: 3 DELETE, 2 SIMPLIFY, 2 MERGE)
- ✅ Ready to implement processors in Phase 2

**Risks Mitigated**:
- ✅ Critical bug fixed immediately (304 responses work)
- ✅ Architecture validated before implementation
- ✅ No surprises in Phase 2 (all decisions made)
- ✅ FakeTimeProvider requirement understood and documented

---

### Phase 2: Build New Foundation (Days 2-3.5) - Story 1.2

**Story**: Create Processors and Simplify Base Endpoints
**Estimated Effort**: 2.5 days (increased from 2 days for ETag complexity)

**Day 2 Morning (4 hours): Global Pre-Processors**

**Tasks**:
1. **Create LoggingPreProcessor** (2 hours)
   - Implement `IPreProcessor<TRequest>`
   - Extract logging logic from BaseEndpoint
   - Structured logging with tracing context
   - Unit tests

2. **Create TracingPreProcessor** (1 hour)
   - Implement `IPreProcessor<TRequest>`
   - OpenTelemetry activity creation
   - Unit tests

3. **Create ETagPreProcessor** (1 hour) ⭐
   - Implement `IPreProcessor<TRequest>`
   - Extract client ETag from If-None-Match header
   - Store in HttpContext.Items
   - Unit tests

**Day 2 Afternoon (4 hours): Global Post-Processors**

**Tasks**:
4. **Create ETagPostProcessor** (3 hours) ⭐ COMPLEX
   - Implement `IPostProcessor<TRequest, TResponse>`
   - Compare client ETag with server ETag
   - Handle 304 Not Modified responses
   - Call `MarkResponseStart()` for 304
   - Unit tests + E2E validation
   - **Critical**: Test with `AuthMe_IfNoneMatchWithCurrentETag_ShouldReturn304`

5. **Create CachingHeadersPostProcessor** (1 hour)
   - Implement `IPostProcessor<TRequest, TResponse>`
   - Inject Cache-Control and Vary headers
   - Unit tests

**Day 3 Morning (4 hours): Module Processors + Registration**

**Tasks**:
6. **Create IdentityAuthProcessor** (3 hours)
   - Implement `IPreProcessor<TRequest>`
   - Extract token validation from BaseIdentityQueryEndpoint
   - Preserve FakeTimeProvider support (critical for 12 time-based tests)
   - Handle Dynamic vs Axon token logic
   - Unit tests + E2E validation

7. **Register All Processors in Program.cs** (1 hour)
   - Global processors (Logging, Tracing, ETagPre, ETagPost, CachingHeaders)
   - Module processors (IdentityAuth for Identity endpoints only)
   - Configure processor order
   - Test registration

**Day 3 Afternoon (4 hours): Simplify Base Classes**

**Tasks**:
8. **Merge BaseEndpoint → BaseResultEndpoint** (1 hour)
   - Copy logging methods to BaseResultEndpoint (will be deprecated later)
   - Delete BaseEndpoint.cs
   - Update BaseResultEndpoint inheritance to `Endpoint<TRequest, TResponse>`

9. **Simplify BaseResultEndpoint** (2 hours)
   - Current: 102 lines
   - Target: ~120-150 lines
   - Add query execution framework from BaseMappedEndpoint
   - Keep Pattern A Response property usage (legitimate)
   - Remove logging (now in processor)
   - Add XML documentation
   - Unit tests

10. **Delete BaseMappedEndpoint** (1 hour)
    - Remove file
    - Document inline mapping strategy for endpoints
    - Update all endpoint base class references

**Acceptance Criteria**:
- ✅ All 6 processors implemented with unit tests
- ✅ BaseResultEndpoint simplified to ~120-150 lines
- ✅ BaseEndpoint merged into BaseResultEndpoint
- ✅ BaseMappedEndpoint deleted
- ✅ No Response property Pattern B workarounds
- ✅ Processors registered and working
- ✅ All 57 E2E tests still passing
- ✅ Unit tests for all processors passing

---

### Phase 3: Migrate All Endpoints (Days 4-5) - Story 1.3

**Story**: Migrate All 8 Endpoints to New Pattern
**Estimated Effort**: 2 days

**Day 4 (8 hours) - Identity Endpoints**

**Migration Pattern Per Endpoint**:
1. Change inheritance (remove multiple base classes)
2. Add inline Mapster mapping (replace BaseMappedEndpoint)
3. Remove manual auth checks (now in IdentityAuthProcessor)
4. Remove ETag logic (now in ETag Pre+Post Processors)
5. Remove logging (now in LoggingPreProcessor)
6. Use SendAsync() methods
7. Remove unnecessary overrides
8. Test immediately after migration

**Identity Endpoints** (5 endpoints):

1. **ChallengeEndpoint** (1 hour)
   - Before: Inherits from BaseIdentityCommandEndpoint
   - After: Inherits from BaseResultEndpoint directly
   - Add: `var command = request.Adapt<ChallengeCommand>()`
   - Remove: Auth logic, logging
   - Test: `dotnet test --filter "FullyQualifiedName~WalletSignature"`

2. **VerifySignatureEndpoint** (1 hour)
   - Same pattern as ChallengeEndpoint
   - Test: `dotnet test --filter "FullyQualifiedName~WalletSignature"`

3. **ExchangeEndpoint** (1 hour)
   - Same pattern, returns JWT
   - Test: `dotnet test --filter "FullyQualifiedName~AuthMe"`

4. **RefreshEndpoint** (1 hour)
   - Same pattern, returns JWT
   - Test: `dotnet test --filter "FullyQualifiedName~TokenValidation"`

5. **MeEndpoint** (2 hours) ⚠️ COMPLEX
   - Before: Inherits from BaseIdentityQueryEndpoint (273 lines of base logic)
   - After: Inherits from BaseResultEndpoint (~30-40 lines total)
   - Remove: ALL ETag logic (now in ETag Pre+Post Processors)
   - Remove: Manual auth checks (now in IdentityAuthProcessor)
   - Remove: Logging (now in LoggingPreProcessor)
   - Keep: Query execution via MediatR
   - Add: `var query = request.Adapt<GetMyPrincipalQuery>()`
   - Test: ALL AuthMe E2E tests (14 tests)
   - **Critical**: Verify `AuthMe_IfNoneMatchWithCurrentETag_ShouldReturn304` passes

**Run All Identity E2E Tests** (2 hours)
- `dotnet test --filter "Category=E2E&FullyQualifiedName~Identity"`
- Fix any failures immediately
- 43 Identity E2E tests must pass (AuthMe 14, Idempotency 8, TokenValidation 12, WalletSignature 9)

**Day 5 (8 hours) - Chat Endpoints**

**Chat Endpoints** (3 endpoints):

6. **GetConversationsEndpoint** (2 hours)
   - Before: Inherits from BaseChatQueryEndpoint (73 lines)
   - After: Inherits from BaseResultEndpoint
   - Add: Inline pagination logic (from BaseChatQueryEndpoint)
   - Add: `var query = request.Adapt<GetConversationsQuery>()`
   - Remove: Base class abstractions
   - Test: Chat E2E tests

7. **GetConversationMessagesEndpoint** (2 hours)
   - Same pattern as GetConversationsEndpoint
   - Pagination logic
   - Test: Chat E2E tests

8. **ChatTurnEndpoint** (3 hours) ⚠️ MOST COMPLEX
   - Before: Inherits from BaseChatCommandEndpoint (73 lines, streaming support)
   - After: Inherits from BaseResultEndpoint
   - Keep: SSE streaming logic (endpoint-specific, complex)
   - Add: `var command = request.Adapt<ChatTurnCommand>()`
   - Remove: Base class abstractions
   - Test: ALL ChatTurn E2E tests (14 tests)
   - **Critical**: Verify streaming still works

**Run All E2E Tests** (1 hour)
- `dotnet test --filter "Category=E2E"`
- Full test suite (57 tests)
- Fix any remaining failures

**Acceptance Criteria**:
- ✅ All 8 endpoints inherit from BaseResultEndpoint only
- ✅ All 57 E2E tests passing
- ✅ All unit tests passing
- ✅ Zero workarounds remaining (Pattern B removed)
- ✅ Manual token checks removed from endpoints (moved to processor)
- ✅ ETag logic removed from endpoints (moved to processors)
- ✅ Streaming still works (ChatTurnEndpoint)
- ✅ Pagination preserved (Chat query endpoints)

---

### Phase 4: Cleanup & Final Simplification (Day 6) - Story 1.4

**Story**: Delete Remaining Base Classes and Finalize
**Estimated Effort**: 1 day

**Tasks**:

1. **Delete BaseIdentityQueryEndpoint** (2 hours)
   - Verify all Identity query endpoints migrated
   - Delete file: `src/Api/Modules/BaseIdentityQueryEndpoint.cs`
   - Commit: `refactor: Delete BaseIdentityQueryEndpoint after migration`

2. **Delete BaseIdentityCommandEndpoint** (1 hour)
   - Verify all Identity command endpoints migrated
   - Delete file: `src/Api/Modules/BaseIdentityCommandEndpoint.cs`
   - Commit: `refactor: Delete BaseIdentityCommandEndpoint after migration`

3. **Delete BaseChatQueryEndpoint** (1 hour)
   - Verify Chat query endpoints migrated (pagination inlined)
   - Delete file: `src/Api/Modules/BaseChatQueryEndpoint.cs`
   - Commit: `refactor: Delete BaseChatQueryEndpoint after migration`

4. **Delete BaseChatCommandEndpoint** (1 hour)
   - Verify ChatTurnEndpoint migrated (streaming preserved)
   - Delete file: `src/Api/Modules/BaseChatCommandEndpoint.cs`
   - Commit: `refactor: Delete BaseChatCommandEndpoint after migration`

5. **Remove Deprecated Logging Methods from BaseResultEndpoint** (1 hour)
   - Logging now in LoggingPreProcessor
   - Remove: LogRequestReceived, LogRequestCompleted, LogRequestFailed, LogRequestCancelled
   - Reduce BaseResultEndpoint to final size (~120-150 lines)

6. **Code Cleanup** (2 hours)
   - Remove all dead code
   - Remove unused using statements
   - Update XML comments
   - Remove TODOs related to refactoring
   - Run .NET analyzer: `dotnet build --configuration Release`
   - Fix all warnings

**Acceptance Criteria**:
- ✅ 4 base class files deleted (BaseIdentity*, BaseChat*)
- ✅ BaseResultEndpoint at final size (~120-150 lines)
- ✅ No compiler warnings
- ✅ All 57 E2E tests still passing
- ✅ Code analysis clean

---

### Phase 5: Polish & Document (Day 7-8) - Story 1.5

**Story**: Testing, Performance, and Documentation
**Estimated Effort**: 1-2 days

**Day 7 Morning (4 hours) - Testing**

**Tasks**:
1. **Comprehensive Test Run** (2 hours)
   - All unit tests: `dotnet test --filter "Category=Unit"`
   - All E2E tests: `dotnet test --filter "Category=E2E"`
   - All integration tests: `dotnet test`
   - Verify coverage >90%: `dotnet test --collect:"XPlat Code Coverage"`

2. **Manual Testing** (2 hours)
   - Test each endpoint manually via Swagger
   - Verify ETag headers (MeEndpoint)
   - Verify 304 responses (MeEndpoint with If-None-Match)
   - Verify error responses (ProblemDetails RFC 7807)
   - Test authentication flow (Challenge, VerifySignature, Exchange, Me)
   - Test streaming (ChatTurnEndpoint SSE)

**Day 7 Afternoon (4 hours) - Performance**

**Tasks**:
3. **Performance Baseline** (2 hours)
   - Benchmark each endpoint (before/after)
   - Metrics to measure:
     - Response time (p50, p95, p99)
     - Memory allocation
     - Throughput (requests/second)
   - Document results
   - Verify: Same or better than baseline

4. **Performance Verification** (2 hours)
   - Run load tests (100 concurrent users)
   - Monitor: Response times, error rates, memory usage
   - Compare with baseline
   - Document: "Performance maintained" or "Performance improved by X%"

**Day 8 (8 hours) - Documentation**

**Tasks**:
5. **Update Engineering Documentation** (4 hours)
   - `Docs/ENGINEERING/guides/patterns/endpoint-patterns.md` - New REPR pattern
   - `Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md` - Processor usage
   - `Docs/ENGINEERING/guides/codebase/coding-standards.md` - Endpoint standards
   - Add: "How to Create New Endpoint" guide
   - Add: Processor documentation
   - Add: Migration notes (before/after comparison)

6. **Update Architecture Documentation** (2 hours)
   - Update architecture diagrams (5 layers → 2-3 layers)
   - Document processor architecture
   - Update system overview

7. **Create ADR-004** (2 hours)
   - Extract from technical research (Section 9)
   - Status: "Proposed" → "Accepted"
   - Add implementation notes
   - Document actual results:
     - Line count: 773 → ~320-350 (actual)
     - Test coverage: 57 E2E tests (all passing)
     - Performance: [actual results]
     - Timeline: [actual days]

**Acceptance Criteria**:
- ✅ All tests passing (unit + E2E + integration)
- ✅ Test coverage >90%
- ✅ Performance verified (same or better than baseline)
- ✅ Manual testing complete (all scenarios verified)
- ✅ Engineering documentation updated
- ✅ Architecture documentation updated
- ✅ ADR-004 accepted and published
- ✅ Ready for production

---

## Acceptance Criteria (Epic-Level)

### Functional
- ✅ All 8 endpoints work correctly
- ✅ 304 Not Modified responses work (ETag support)
- ✅ Authentication works (Dynamic + Axon tokens)
- ✅ Time-travel testing works (FakeTimeProvider support in 12 tests)
- ✅ Validation works (FluentValidation)
- ✅ Error handling works (ProblemDetails RFC 7807)
- ✅ Streaming works (ChatTurnEndpoint SSE)
- ✅ Pagination works (Chat query endpoints)
- ✅ Swagger/OpenAPI generation works

### Technical
- ✅ Infrastructure code reduced from 773 to ~320-350 lines (60% reduction)
- ✅ Inheritance hierarchy reduced from 5 to 2-3 layers
- ✅ Pattern B Response property workaround removed
- ✅ Pattern A Response property usage preserved (legitimate)
- ✅ Manual token expiration checks moved to processor (preserved for testing)
- ✅ BaseResultEndpoint ~120-150 lines
- ✅ 6 pre/post processors implemented and working
- ✅ All 57 E2E tests passing
- ✅ All unit tests passing
- ✅ Test coverage >90%
- ✅ No compiler warnings
- ✅ Performance maintained or improved

### Quality
- ✅ Code is debuggable (logic concentrated, not scattered)
- ✅ Code is maintainable (clear separation of concerns)
- ✅ Code follows REPR pattern (vertical slices)
- ✅ Documentation updated
- ✅ ADR published

---

## Dependencies

### Internal
- **Testing Infrastructure**: 57 E2E tests provide safety net (AVAILABLE)
- **FastEndpoints 7.0.1**: Current framework (INSTALLED)
- **MediatR**: Command/query handling (INSTALLED)
- **FluentValidation**: Request validation (INSTALLED)
- **Result Pattern**: Error handling (IMPLEMENTED)
- **FakeTimeProvider**: Time-travel testing (IMPLEMENTED)

### External
- **FastEndpoints Documentation**: Pre/post processors, response lifecycle (AVAILABLE)
- **FastEndpoints Community**: Discord, GitHub issues (AVAILABLE)

### Blockers
- ❌ None identified

---

## Risks and Mitigation

| Risk | Likelihood | Impact | Mitigation | Contingency |
|------|------------|--------|------------|-------------|
| Breaking Production | Medium | High | 57 E2E tests + manual testing | git revert + rollback deploy |
| Performance Regression | Low | Medium | Benchmark on Day 7, processors optimized by FastEndpoints | Profile hot paths, adjust processor ordering |
| FakeTimeProvider Broken | Low | High | 12 time-based tests validate, preserve in processor | Add custom ISystemClock if needed |
| Streaming Broken | Medium | High | 14 ChatTurn E2E tests, manual testing | Keep streaming logic inline, test thoroughly |
| Unknown FastEndpoints Edge Cases | Low | Medium | Study docs Day 1, review GitHub issues | Ask Discord community, worst case: one-off workaround |
| Timeline Slippage | Medium | Low | Buffer built in (6-8 days estimate, +0.5-1 day for ETag) | Can ship after Day 6 if docs delayed |
| Regression in Tests | Medium | High | Test after each phase, test each endpoint after migration | Fix immediately, don't batch |

**Overall Risk Level**: Low-Medium (mitigated by 57 E2E tests and git revert safety)

---

## Success Metrics

### Quantitative
- **Code Reduction**: 60% (773 → ~320-350 lines) ✅ TARGET
- **Layers Reduction**: 5 → 2-3 layers ✅ TARGET
- **Test Pass Rate**: 100% (57/57 E2E + all unit tests) ✅ TARGET
- **Test Coverage**: >90% ✅ TARGET
- **Performance**: Same or better than baseline ✅ TARGET
- **Time to Implement New Endpoint**: Cut in half (no base class archaeology) ✅ TARGET

### Qualitative
- **Debuggability**: Excellent (logic concentrated in fewer files) ✅ TARGET
- **Maintainability**: High (composition over inheritance) ✅ TARGET
- **Developer Experience**: Low learning curve (2-3 layers vs 5) ✅ TARGET
- **Code Quality**: Zero workarounds (Pattern B removed, Pattern A legitimate) ✅ TARGET
- **Framework Alignment**: Using FastEndpoints properly (REPR + processors) ✅ TARGET

---

## Technical Decisions

### Decided
- **Framework**: Stay with FastEndpoints 7.0.1 (not migrating)
- **Approach**: Brutal refactoring (no gradual migration, no backward compatibility)
- **Pattern**: REPR (Request-Endpoint-Response) + Vertical Slices
- **Cross-Cutting Concerns**: Pre/post processors (not inheritance)
- **Response Handling**: Use SendAsync() methods (remove Pattern B, keep Pattern A)
- **Authentication**: Processor-based (preserve FakeTimeProvider for testing)
- **Testing**: Comprehensive (57 E2E + unit tests after each phase)
- **BaseIdentityQueryEndpoint**: SIMPLIFY (not DELETE) - pragmatic approach
- **BaseMappedEndpoint**: DELETE - inline Mapster mapping in endpoints
- **ETag Strategy**: Pre-processor + Post-processor combination
- **Timeline**: 6-8 days (realistic with buffer for complexity)

### Architecture Decision Record
- **ADR-004**: Refactor FastEndpoints Base Endpoint Abstractions
- **Status**: Proposed → Will move to Accepted after implementation
- **Source**: Technical Research (Docs/PROCESS/research/technical-research-2025-10-01.md)
- **Review**: Code Review Findings (Docs/PROCESS/research/epic-review-findings-2025-10-02.md)

---

## Documentation

### Source Documentation
- **Technical Research**: `Docs/PROCESS/research/technical-research-2025-10-01.md` (1348 lines, comprehensive analysis)
- **Epic Review**: `Docs/PROCESS/research/epic-review-findings-2025-10-02.md` (verification + refinements)
- **ADR**: Section 9 of technical research (will be extracted)

### Documentation to Update
- `Docs/ENGINEERING/guides/patterns/endpoint-patterns.md` - New REPR pattern
- `Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md` - Processor usage
- `Docs/ENGINEERING/guides/codebase/coding-standards.md` - Endpoint standards
- ADR-004 (create and accept)
- Architecture diagrams

### Documentation to Create
- "How to Create New Endpoint" guide (with processor awareness)
- Processor documentation (ETag Pre+Post combination)
- Migration notes (before/after comparison)
- FakeTimeProvider testing guide (time-travel testing pattern)

---

## Related Work

### Research
- **Performance Comparison**: FastEndpoints vs Minimal APIs vs Carter (Section 4 of research)
- **Real-World Evidence**: Production experiences, known issues (Section 6 of research)
- **Architecture Patterns**: REPR + Vertical Slices (Section 7 of research)

### References
- FastEndpoints Documentation: https://fast-endpoints.com/docs
- GitHub Issue #230: Response lifecycle (directly relevant to 304 bug)
- REPR Pattern: https://www.infoworld.com/article/2336445/how-to-use-the-repr-design-pattern-in-asp-net-core.html
- Vertical Slice Architecture: https://www.milanjovanovic.tech/blog/vertical-slice-architecture

---

## Notes

### Solo Dev Advantages
- No coordination overhead
- Can execute brutally and fast
- No gradual migration needed
- No backward compatibility needed
- Can test aggressively

### Key Insights
**FastEndpoints is NOT the problem - our implementation is.** The framework is well-designed for Clean Architecture + CQRS + Vertical Slices. We've been fighting it instead of using it properly.

**FakeTimeProvider is a feature**, not a workaround. It enables deterministic time-based testing (12 E2E tests depend on it).

**Two Response Property Patterns**: Pattern A (ProblemDetails) is legitimate FastEndpoints usage. Pattern B (Activator.CreateInstance) is a workaround to remove.

**ETag requires Pre+Post combination**: Original plan only included post-processor. Pre-processor needed to extract client ETag early.

### Why Now?
- **Optimal Timing**: 8 endpoints is perfect refactoring size (at 50+ it becomes painful)
- **Good Safety Net**: 57 E2E tests provide strong confidence (4x better than originally thought)
- **Technical Debt Window**: Pay it off now before compound interest
- **Production Early**: Can afford refactoring now, harder later

### Implementation Philosophy
**Brutal but Safe**: Delete everything unnecessary, migrate all at once, but test comprehensively at every step. 57 E2E tests + git revert is our safety net.

---

## Story List

1. **Story 1.1**: Fix 304 Bug and Design Target Architecture (Day 1)
2. **Story 1.2**: Create Processors and Simplify Base Endpoints (Days 2-3.5)
3. **Story 1.3**: Migrate All 8 Endpoints to New Pattern (Days 4-5)
4. **Story 1.4**: Delete Remaining Base Classes and Cleanup (Day 6)
5. **Story 1.5**: Testing, Performance, and Documentation (Days 7-8)

**Total**: 5 stories over 6-8 days

---

## Changelog

| Date | Change | Author |
|------|--------|--------|
| 2025-10-02 | Initial epic created from technical research | Valik |
| 2025-10-02 | Merged with code review findings, refined architecture | Valik |
| 2025-10-02 | Updated test count (14 → 57), added ETagPre, revised timeline | Valik |

---

## Appendices

### Appendix A: Verified Line Counts

```bash
BaseEndpoint.cs:                  63 lines ✅
BaseResultEndpoint.cs:           102 lines ✅
BaseMappedEndpoint.cs:            99 lines ✅
BaseIdentityQueryEndpoint.cs:    273 lines ✅
BaseIdentityCommandEndpoint.cs:   90 lines ✅
BaseChatQueryEndpoint.cs:         73 lines ✅
BaseChatCommandEndpoint.cs:       73 lines ✅
────────────────────────────────────────
TOTAL:                           773 lines ✅
```

### Appendix B: Complete Test Inventory

```
Identity Module E2E Tests:
├── AuthMeE2ETests.cs          14 tests (ETag, 304, caching)
│   └── AuthMe_IfNoneMatchWithCurrentETag_ShouldReturn304 ⭐ CRITICAL
├── IdempotencyE2ETests.cs      8 tests (command idempotency)
├── TokenValidationE2ETests.cs 12 tests (JWT expiration, FakeTimeProvider) ⭐
└── WalletSignatureE2ETests.cs  9 tests (Web3 signature verification)

Chat Module E2E Tests:
└── ChatTurnE2ETests.cs        14 tests (SSE streaming) ⭐

TOTAL: 57 E2E tests ✅
```

### Appendix C: Critical Code Locations

**304 Bug Location**:
- File: `src/Api/Modules/BaseIdentityQueryEndpoint.cs`
- Method: `HandleNotModifiedResponse` (Line 212)
- Missing: `HttpContext.MarkResponseStart()`

**Response Property Patterns**:
- Pattern A (Legitimate): `BaseResultEndpoint.cs:91` ✅ KEEP
- Pattern B (Workaround): `BaseIdentityQueryEndpoint.cs:233` ❌ REMOVE

**Manual Token Validation** (Time-Travel Testing):
- File: `src/Api/Modules/BaseIdentityQueryEndpoint.cs`
- Lines: 64-81
- Purpose: FakeTimeProvider support ✅ PRESERVE
- Move To: IdentityAuthProcessor

**304 Test**:
- File: `tests/Modules/Identity/E2E/AuthMeE2ETests.cs`
- Test: `AuthMe_IfNoneMatchWithCurrentETag_ShouldReturn304` (Line 88)
- Validator: `AuthMeResponseValidator.ValidateNotModifiedResponse`

### Appendix D: Key Refinements from Review

1. **Test Count**: Updated from 14 to 57 E2E tests
2. **ETag Strategy**: Added ETagPreProcessor (original plan only had post-processor)
3. **BaseIdentityQueryEndpoint**: Changed from DELETE to SIMPLIFY
4. **Response Property**: Distinguished Pattern A (legitimate) from Pattern B (workaround)
5. **FakeTimeProvider**: Documented as feature enabling time-travel testing
6. **Timeline**: Adjusted from 5-7 days to 6-8 days
7. **BaseResultEndpoint Size**: Increased from ~80-100 to ~120-150 lines (absorbs query framework)

---

**Epic Status**: ✅ APPROVED - Ready for Implementation
**Next Step**: Begin Day 1 (Fix 304 Bug + Design Target Architecture)
**Review Date**: 2025-10-02
**Implementation Start**: TBD

---

_This epic was created by merging comprehensive technical research (1348 lines) with code review findings, and incorporates all best practices, verified line counts, actual test coverage (57 E2E tests), and refined architectural decisions. Ready for immediate execution._
