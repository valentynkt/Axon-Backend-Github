# Epic: FastEndpoints Base Endpoint Refactoring

**Epic ID**: EPIC-001
**Status**: 🎯 Proposed
**Priority**: High
**Created**: 2025-10-02
**Last Updated**: 2025-10-02
**Owner**: Valik
**Timeline**: 5-7 days intensive work

---

## Executive Summary

### The Problem

The Axon Backend API layer has accumulated **773 lines of infrastructure code** across a **5-layer inheritance hierarchy** of base endpoint classes, yet only serves **8 concrete endpoints**. This results in an infrastructure-to-endpoint ratio of **~97 lines per endpoint**, indicating severe over-engineering.

**Critical Bug**: 304 Not Modified responses are incorrectly converted to 204 NoContent, breaking HTTP caching semantics and causing client-side caching failures.

**Concrete Issues**:
- **BaseEndpoint.cs** (64 lines) - Base logging and Result pattern
- **BaseResultEndpoint.cs** (103 lines) - Result→Response mapping
- **BaseMappedEndpoint.cs** (100 lines) - Auto-mapping layer
- **BaseIdentityQueryEndpoint.cs** (274 lines) - ETag, auth, logging, manual token validation
- **BaseIdentityCommandEndpoint.cs** (~150 lines) - Command-specific Identity logic
- **BaseChatQueryEndpoint.cs** (~120 lines) - Chat query abstractions
- **BaseChatCommandEndpoint.cs** (74 lines) - Chat command abstractions

**Total**: 773+ lines serving 8 endpoints (GetConversations, GetConversationMessages, ChatTurn, Challenge, VerifySignature, Exchange, Refresh, Me)

### Root Cause Analysis

**FastEndpoints is NOT the problem** - our implementation is. The research identified specific misuses:

1. **Missing `HttpContext.MarkResponseStart()`** (Line 212 in BaseIdentityQueryEndpoint)
   - FastEndpoints checks this flag to prevent double response writes
   - Without it, FastEndpoints auto-converts 304 to 204
   - **Fix**: One-line addition

2. **Response Property Manipulation Workarounds**
   - Code: `Response = Activator.CreateInstance<TResponse>()` to prevent auto-204
   - Indicates fundamental misunderstanding of FastEndpoints response lifecycle
   - Should use `SendAsync()` methods or `MarkResponseStart()` instead

3. **Manual Token Expiration Checks** (Lines 64-81 in BaseIdentityQueryEndpoint)
   - Manually validating JWT expiration for test environment (FakeTimeProvider)
   - Comments indicate this is a workaround for authentication middleware
   - Reimplementing what middleware should handle

4. **5-Layer Deep Inheritance Hierarchy**
   - Violates composition over inheritance principle
   - Creates tight coupling and wide impact radius for changes
   - Makes debugging difficult (logic scattered across 5 files)

5. **Unused FastEndpoints Features**
   - **Pre-Processors**: Could handle authentication, ETag extraction, logging
   - **Post-Processors**: Could handle ETag injection, response caching headers
   - **Response Interceptors**: Could handle 304 logic globally
   - **SendAsync() methods**: Bypassed with Response property manipulation
   - **Built-in ProblemDetails**: Partially reimplemented

### Decision: Refactor FastEndpoints Implementation

**✅ APPROVED**: Keep FastEndpoints 7.0.1, execute brutal refactoring

**Rationale from Decision Matrix** (Section 4 of Research Report):
- **Score**: 9.0/10 (highest among 4 options evaluated)
- **Time Efficiency**: 5-7 days vs 10-15 days for framework migration
- **Maintainability**: Same target state as alternatives
- **Feature Completeness**: Leverages existing FastEndpoints features
- **Risk**: Lowest (same framework, comprehensive test coverage)

**Alternatives Rejected**:
- Migrate to Minimal APIs: 6.6/10 score, 2x time investment, lose FastEndpoints features
- Migrate to Carter: 6.8/10 score, similar migration cost, smaller ecosystem
- Keep Current: 6.9/10 score, technical debt compounds exponentially

### Expected Impact

**Code Reduction**: 60% (773 → 300-350 lines)
- Delete 4 base classes completely
- Reduce BaseResultEndpoint from 103 to <100 lines
- Move cross-cutting concerns to processors (~200 lines total)

**Bug Resolution**:
- Fix 304 Not Modified bug (immediate)
- Remove all Response property manipulation workarounds
- Remove manual token expiration checks

**Architecture Improvement**:
- 5 layers → 2-3 layers (BaseEndpoint → [Optional BaseResultEndpoint] → Concrete Endpoint)
- Inheritance → Composition (pre/post processors)
- Framework fighting → Framework alignment (REPR pattern)

**Developer Experience**:
- New endpoint implementation: Cut from 30min to 10min (estimate based on real-world reports)
- Learning curve: Reduced (2-3 layers vs 5 layers to understand)
- Debugging: Improved (concentrated logic vs scattered across 5 files)
- Onboarding: Easier ("Learn FastEndpoints" vs "Learn our 5-layer custom abstraction")

**Performance**: Neutral to Positive
- Pre/post processors are optimized by FastEndpoints
- Research shows <2% difference between FastEndpoints and Minimal APIs
- May improve due to removing unnecessary abstraction layers

### Timeline & Approach

**Duration**: 5-7 days intensive work

**Approach**: Brutal but Safe
- **Brutal**: Delete all 4 unnecessary base classes at once, no gradual migration
- **Safe**: Comprehensive testing at every step (14 E2E tests + unit tests)
- **Solo Dev Advantage**: No coordination overhead, can move fast
- **Safety Net**: Git revert + strong test coverage

**Phases**:
1. **Day 1**: Fix bug + design architecture
2. **Days 2-3**: Build new foundation (processors + simplified base)
3. **Days 4-5**: Migrate all 8 endpoints
4. **Days 6-7**: Polish + documentation

**Risk Level**: Low-Medium (mitigated by testing)

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
**Test Coverage**: 14 E2E tests (AuthMe tests + Chat tests)

### Current Architecture (Anti-Pattern) - Detailed Breakdown

#### File Locations and Responsibilities

```
src/Api/Modules/
├── BaseEndpoint.cs (64 lines)
│   └─ Responsibilities: Base logging, IMediator injection, Result pattern foundation
│   └─ Issues: Minimal value, could be absorbed into BaseResultEndpoint
│
├── BaseResultEndpoint.cs (103 lines)
│   └─ Responsibilities: Result<T, Error> → HTTP response mapping, ProblemDetails
│   └─ Issues: Response property manipulation (`Response = Activator.CreateInstance<TResponse>()`)
│   └─ File: src/Api/Modules/BaseResultEndpoint.cs
│
└── BaseMappedEndpoint.cs (100 lines)
    └─ Responsibilities: Auto-mapping between command/query and request/response DTOs
    └─ Issues: Unnecessary abstraction, mapping can be inline in endpoints
    └─ File: src/Api/Modules/BaseMappedEndpoint.cs
    │
    ├── Identity/BaseIdentityQueryEndpoint.cs (274 lines) ⚠️ LARGEST FILE
    │   ├─ Responsibilities:
    │   │   • ETag extraction from request headers
    │   │   • ETag injection into response headers
    │   │   • 304 Not Modified response logic
    │   │   • Manual JWT token expiration checks (Lines 64-81)
    │   │   • Dynamic vs Axon token differentiation
    │   │   • Structured logging
    │   │   • Response mapping
    │   ├─ Issues:
    │   │   • Missing `HttpContext.MarkResponseStart()` at Line 212 (causes 304→204 bug)
    │   │   • Manual token validation bypasses middleware
    │   │   • ETag logic should be in processor
    │   │   • Logging should be in processor
    │   └─ File: src/Api/Modules/Identity/BaseIdentityQueryEndpoint.cs
    │
    ├── Identity/BaseIdentityCommandEndpoint.cs (~150 lines)
    │   └─ Responsibilities: Command-specific Identity logic, similar issues
    │   └─ File: src/Api/Modules/Identity/BaseIdentityCommandEndpoint.cs
    │
    ├── Chat/BaseChatQueryEndpoint.cs (~120 lines)
    │   └─ Responsibilities: Chat query abstractions, pagination
    │   └─ File: src/Api/Modules/Chat/BaseChatQueryEndpoint.cs
    │
    └── Chat/BaseChatCommandEndpoint.cs (74 lines)
        └─ Responsibilities: Chat command abstractions, streaming support
        └─ File: src/Api/Modules/Chat/BaseChatCommandEndpoint.cs
```

**Total Infrastructure**: 773+ lines across 7 files
**Infrastructure per Endpoint**: ~97 lines

#### The 8 Concrete Endpoints

**Identity Module** (5 endpoints):
1. `ChallengeEndpoint` - POST /api/v1/auth/challenge (Dynamic Web3 auth initiation)
2. `VerifySignatureEndpoint` - POST /api/v1/auth/verify-signature (Web3 signature verification)
3. `ExchangeEndpoint` - POST /api/v1/auth/exchange (Exchange Dynamic token for Axon JWT)
4. `RefreshEndpoint` - POST /api/v1/auth/refresh (JWT refresh)
5. `MeEndpoint` - GET /api/v1/auth/me (Current user info, ETag support)

**Chat Module** (3 endpoints):
6. `GetConversationsEndpoint` - GET /api/v1/chat/conversations (List conversations)
7. `GetConversationMessagesEndpoint` - GET /api/v1/chat/conversations/{id}/messages (Message history)
8. `ChatTurnEndpoint` - POST /api/v1/chat/conversations/{id}/turn (Streaming chat, most complex)

### Target Architecture (REPR Pattern) - Detailed Design

#### Simplified Hierarchy

```
FastEndpoints.Endpoint<TRequest, TResponse>  [FastEndpoints Framework]
│
└── BaseResultEndpoint<TRequest, TResponse>  [~80-100 lines, KEEP & SIMPLIFY]
    ├─ Responsibilities: ONLY Result<T, Error> → HTTP response mapping
    ├─ Methods:
    │   • ExecuteAsync(TRequest, CancellationToken) → Result<TResponse, Error>
    │   • HandleAsync() → Calls ExecuteAsync + maps result to SendAsync()
    │   • SendSuccessAsync(TResponse) → Uses SendAsync(response, 200, ct)
    │   • SendFailureAsync(Error) → Maps to ProblemDetails, uses SendAsync()
    └─ Removes: All logging, auth, ETag, mapping logic
    │
    └── 8 Concrete Endpoints [30-50 lines each]
        ├─ MeEndpoint
        ├─ ChallengeEndpoint
        ├─ VerifySignatureEndpoint
        ├─ ExchangeEndpoint
        ├─ RefreshEndpoint
        ├─ GetConversationsEndpoint
        ├─ GetConversationMessagesEndpoint
        └─ ChatTurnEndpoint
```

#### Processors (Composition over Inheritance)

**Global Pre-Processors** (~100 lines total):
```
1. LoggingPreProcessor (30 lines)
   • Logs incoming request (method, path, userId)
   • Uses ILogger<T> and Activity for correlation

2. TracingPreProcessor (25 lines)
   • OpenTelemetry activity creation
   • Enriches trace context

3. AuthenticationPreProcessor (45 lines)
   • Optional (only for endpoints requiring auth)
   • Basic auth validation (already done by middleware)
   • Could be skipped if relying fully on [Authorize]
```

**Global Post-Processors** (~100 lines total):
```
1. ETagPostProcessor (50 lines)
   • Extract ETag from response entity (IHaveETag interface)
   • Inject ETag header into response
   • Handle If-None-Match comparison
   • Call HttpContext.MarkResponseStart() when sending 304
   • Uses SendAsync(EmptyResponse, 304, ct) for Not Modified

2. CachingHeadersPostProcessor (30 lines)
   • Inject Cache-Control headers
   • Inject Vary headers
   • Based on endpoint configuration

3. ProblemDetailsPostProcessor (20 lines)
   • Optional, if BaseResultEndpoint doesn't handle
   • Global error formatting
```

**Module-Specific Processors** (~100-150 lines total):
```
1. IdentityAuthProcessor (80-100 lines) [Pre-Processor]
   • Applied only to Identity endpoints
   • Handles Dynamic vs Axon token differentiation
   • Validates token expiration (FakeTimeProvider support for tests)
   • Enriches HttpContext.User with token metadata
   • Could be split into DynamicTokenProcessor + AxonTokenProcessor

2. ChatRateLimitProcessor (20-30 lines) [Pre-Processor, future]
   • Applied only to Chat endpoints
   • Rate limiting logic specific to chat
```

**Total Processor Code**: ~300-350 lines (vs 773 lines current)

#### Deleted Files (4 base classes)
```
✂️ DELETE: src/Api/Modules/BaseMappedEndpoint.cs (100 lines)
✂️ DELETE: src/Api/Modules/Identity/BaseIdentityQueryEndpoint.cs (274 lines)
✂️ DELETE: src/Api/Modules/Identity/BaseIdentityCommandEndpoint.cs (~150 lines)
✂️ DELETE: src/Api/Modules/Chat/BaseChatQueryEndpoint.cs (~120 lines)
✂️ DELETE: src/Api/Modules/Chat/BaseChatCommandEndpoint.cs (74 lines)

Total Deleted: ~718 lines
```

#### Modified Files (2 base classes)
```
📝 SIMPLIFY: src/Api/Modules/BaseEndpoint.cs
   • Merge into BaseResultEndpoint or delete entirely
   • Decision pending design phase (Day 1)

📝 SIMPLIFY: src/Api/Modules/BaseResultEndpoint.cs (103 → ~80-100 lines)
   • Keep: Result<T, Error> mapping only
   • Remove: Response property manipulation workaround
   • Add: Proper use of SendAsync() methods
   • Add: XML documentation for proper usage
```

### Key Technical Issues - Detailed Analysis

#### Issue 1: 304 Not Modified Bug (CRITICAL)

**Location**: `src/Api/Modules/Identity/BaseIdentityQueryEndpoint.cs:212`

**Current Code** (broken):
```csharp
private async Task HandleNotModifiedResponse(CancellationToken ct)
{
    HttpContext.Response.StatusCode = 304; // ❌ FastEndpoints sees no MarkResponseStart
    // Missing: HttpContext.MarkResponseStart();
    await HttpContext.Response.CompleteAsync(); // Response sent
}
// Result: FastEndpoints thinks no response was sent, auto-sends 204 NoContent
```

**Root Cause**:
- FastEndpoints tracks response state via `HttpContext.HasResponseStarted()` flag
- Without `MarkResponseStart()`, FastEndpoints assumes endpoint didn't send response
- Auto-fallback logic sends 204 NoContent (per FastEndpoints conventions)

**Fix** (one line):
```csharp
private async Task HandleNotModifiedResponse(CancellationToken ct)
{
    HttpContext.MarkResponseStart(); // ✅ Tell FastEndpoints we're handling response
    HttpContext.Response.StatusCode = 304;
    await HttpContext.Response.CompleteAsync();
}
```

**Better Fix** (use SendAsync):
```csharp
private async Task HandleNotModifiedResponse(CancellationToken ct)
{
    await SendAsync(new EmptyResponse(), 304, ct); // ✅ FastEndpoints handles everything
}
```

**Best Fix** (move to processor):
```csharp
// In ETagPostProcessor.PostProcessAsync()
if (etag != null && Request.Headers.IfNoneMatch == etag)
{
    await SendAsync(new EmptyResponse(), 304, ct);
    return; // Short-circuit, don't continue to endpoint
}
```

**Reference**: [FastEndpoints GitHub Issue #230](https://github.com/FastEndpoints/FastEndpoints/issues/230) - Identical issue reported and resolved

#### Issue 2: Response Property Manipulation (ANTI-PATTERN)

**Location**: Multiple base classes

**Current Code** (workaround):
```csharp
protected override async Task HandleAsync(TRequest req, CancellationToken ct)
{
    Response = Activator.CreateInstance<TResponse>(); // ❌ Hack to prevent auto-204

    var result = await ExecuteAsync(req, ct);

    if (result.IsSuccess)
        Response = result.Value; // Replace dummy with real response
}
```

**Why This Exists**:
- Developers thought setting Response property prevents FastEndpoints auto-sending 204
- Actually, FastEndpoints checks `HttpContext.HasResponseStarted()` flag, not Response property
- Workaround is ineffective and indicates framework misunderstanding

**Proper Pattern**:
```csharp
protected override async Task HandleAsync(TRequest req, CancellationToken ct)
{
    var result = await ExecuteAsync(req, ct);

    if (result.IsSuccess)
        await SendAsync(result.Value, 200, ct); // ✅ Explicit send
    else
        await SendAsync(MapToProblemDetails(result.Error), result.Error.StatusCode, ct);
}
```

#### Issue 3: Manual Token Expiration Checks (REIMPLEMENTATION)

**Location**: `src/Api/Modules/Identity/BaseIdentityQueryEndpoint.cs:64-81`

**Current Code**:
```csharp
// Lines 64-81: Manual JWT expiration check
var expClaim = User.FindFirst("exp");
if (expClaim != null && long.TryParse(expClaim.Value, out var exp))
{
    var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp);
    var now = _timeProvider.GetUtcNow(); // FakeTimeProvider for tests

    if (now >= expirationTime)
    {
        await SendUnauthorizedAsync(ct);
        return;
    }
}
```

**Why This Exists** (per code comments):
- Required for test environment using `FakeTimeProvider`
- JWT middleware doesn't use injected `TimeProvider`, uses `DateTime.UtcNow`
- Workaround manually validates expiration with injectable time

**Proper Solution**:
1. **Short-term**: Move to `IdentityAuthProcessor` (consolidate workaround)
2. **Long-term**: Configure JWT middleware to use injected `TimeProvider`
   ```csharp
   // In Program.cs JWT configuration
   options.UseTimeProvider = true; // If available in .NET 10
   ```
3. **Alternative**: Custom `ISystemClock` implementation for tests

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
- No changes to API contracts (internal refactor)

---

## Scope

### In Scope
- ✅ Fix 304 Not Modified bug (one-line fix)
- ✅ Delete 4 unnecessary base classes (BaseMappedEndpoint, BaseIdentity*, BaseChat*)
- ✅ Simplify BaseResultEndpoint to <100 lines (Result pattern only)
- ✅ Create pre/post processors for cross-cutting concerns
- ✅ Migrate all 8 endpoints to new pattern
- ✅ Remove all Response property manipulation workarounds
- ✅ Remove manual token expiration checks
- ✅ Update engineering documentation
- ✅ Comprehensive testing (14 E2E tests must pass)

### Out of Scope
- ❌ Adding new endpoints (focus on refactoring existing)
- ❌ Changing API contracts (internal refactor only)
- ❌ Framework migration (staying with FastEndpoints 7.0.1)
- ❌ Backward compatibility (brutal approach, solo dev)
- ❌ Gradual migration (all at once)

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
- **File**: `src/Api/Modules/Identity/BaseIdentityQueryEndpoint.cs`
- **Line**: 212 (in `HandleNotModifiedResponse` method)
- **Change**:
  ```csharp
  // BEFORE (broken)
  private async Task HandleNotModifiedResponse(CancellationToken ct)
  {
      HttpContext.Response.StatusCode = 304;
      await HttpContext.Response.CompleteAsync();
  }

  // AFTER (fixed)
  private async Task HandleNotModifiedResponse(CancellationToken ct)
  {
      HttpContext.MarkResponseStart(); // ✅ ONE LINE FIX
      HttpContext.Response.StatusCode = 304;
      await HttpContext.Response.CompleteAsync();
  }
  ```
- **Actions**:
  1. Open `BaseIdentityQueryEndpoint.cs`
  2. Navigate to line 212 (`HandleNotModifiedResponse` method)
  3. Add `HttpContext.MarkResponseStart();` before `StatusCode = 304`
  4. Save file
  5. Run all E2E tests: `dotnet test --filter "Category=E2E"`
  6. Verify 14/14 tests pass
  7. Manually test MeEndpoint with ETag via Swagger
  8. Commit: `fix: Add MarkResponseStart to fix 304 Not Modified bug`
- **Acceptance Criteria**:
  - ✅ All 14 E2E tests passing
  - ✅ MeEndpoint returns 304 when ETag matches (manual verification)
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
     - Execution order (before endpoint HandleAsync)
     - Access to request, HttpContext, cancellation token
     - Short-circuiting (return early, skip endpoint)
     - Global vs endpoint-specific processors
     - Registration patterns in Program.cs
  2. **Post-Processors**:
     - Execution order (after endpoint HandleAsync)
     - Access to response, HttpContext, cancellation token
     - Response modification capabilities
     - Error handling in processors
  3. **Response Interceptors**:
     - Global response manipulation
     - 304 handling examples
  4. **MarkResponseStart()**:
     - When to call
     - Why it's needed
     - Relationship to SendAsync() methods
- **Deliverable**: Notes document (`Day1-FastEndpoints-Study-Notes.md`)
  - Key patterns identified
  - Code examples for our use cases
  - Gotchas and edge cases
- **Acceptance Criteria**:
  - ✅ Understand pre/post processor lifecycle
  - ✅ Identify 3-5 code examples relevant to our refactoring
  - ✅ Document GitHub issues related to our bugs
  - ✅ Clear mental model of processor execution order

**Task 1.1.3: Design 2-Layer Target Architecture** (1.5 hours)
- **Objective**: Visual diagram + written specification of target state
- **Actions**:
  1. Create architecture diagram (ASCII or draw.io)
  2. Define hierarchy:
     - Layer 1: FastEndpoints.Endpoint<TRequest, TResponse>
     - Layer 2: BaseResultEndpoint<TRequest, TResponse>
     - Layer 3: Concrete endpoints (8 total)
  3. Define BaseResultEndpoint responsibilities:
     - ONLY Result<T, Error> → HTTP response mapping
     - Methods: ExecuteAsync, HandleAsync, SendSuccessAsync, SendFailureAsync
     - Remove: All logging, auth, ETag, mapping
     - Use: SendAsync() methods (no Response property manipulation)
  4. Define processor architecture:
     - Global pre-processors: Logging, Tracing
     - Global post-processors: ETag, CachingHeaders
     - Module processors: IdentityAuth (Dynamic vs Axon tokens)
  5. Document processor registration pattern in Program.cs
  6. Create before/after comparison diagram
- **Deliverable**: `Day1-Target-Architecture.md` with diagrams
- **Acceptance Criteria**:
  - ✅ Clear visual diagram of 2-layer hierarchy
  - ✅ Processor responsibilities defined
  - ✅ Before/after comparison shows 60% code reduction path
  - ✅ Registration pattern documented

##### Afternoon (4 hours)

**Task 1.1.4: Audit All 7 Base Classes** (2 hours)
- **Objective**: Methodical analysis of every method in every base class
- **Process**: For each base class, analyze each method and tag decision

**Base Class 1: BaseEndpoint.cs** (64 lines)
- Read entire file line by line
- Tag each method/property:
  - **KEEP**: Essential, moves to BaseResultEndpoint
  - **MOVE_TO_PROCESSOR**: Cross-cutting concern
  - **DELETE**: Unnecessary, redundant, or reimplemented by FastEndpoints
- Document: Constructor, IMediator usage, Result pattern foundation
- Decision: Merge into BaseResultEndpoint or delete entirely?

**Base Class 2: BaseResultEndpoint.cs** (103 lines)
- Current size: 103 lines
- Target size: ~80-100 lines
- Tag each method:
  - ExecuteAsync: **KEEP** (core abstraction)
  - HandleAsync: **KEEP** but **SIMPLIFY** (remove Response manipulation)
  - Result mapping: **KEEP** (core value)
  - Error handling: **KEEP** but use SendAsync()
  - Logging: **MOVE_TO_PROCESSOR**

**Base Class 3: BaseMappedEndpoint.cs** (100 lines)
- Auto-mapping logic
- Decision: **DELETE** entirely (mapping can be inline in endpoints)
- Justification: 100 lines for 8 endpoints is overkill, inline mapping is clearer

**Base Class 4: BaseIdentityQueryEndpoint.cs** (274 lines) ⚠️ LARGEST
- ETag extraction: **MOVE_TO_PROCESSOR** (ETagPostProcessor)
- ETag injection: **MOVE_TO_PROCESSOR** (ETagPostProcessor)
- 304 logic: **MOVE_TO_PROCESSOR** (ETagPostProcessor)
- Manual token checks: **MOVE_TO_PROCESSOR** (IdentityAuthProcessor)
- Dynamic vs Axon logic: **MOVE_TO_PROCESSOR** (IdentityAuthProcessor)
- Logging: **MOVE_TO_PROCESSOR** (LoggingPreProcessor)
- Response mapping: **DELETE** (inherited from BaseMappedEndpoint)
- Decision: **DELETE** entire file

**Base Class 5: BaseIdentityCommandEndpoint.cs** (~150 lines)
- Similar analysis to BaseIdentityQueryEndpoint
- Decision: **DELETE** entire file

**Base Class 6: BaseChatQueryEndpoint.cs** (~120 lines)
- Pagination logic: **KEEP** in endpoint (domain-specific)
- Other logic: **MOVE_TO_PROCESSOR** or **DELETE**
- Decision: **DELETE** base class, inline pagination in endpoints

**Base Class 7: BaseChatCommandEndpoint.cs** (74 lines)
- Streaming support: **KEEP** in endpoint (domain-specific)
- Other logic: **MOVE_TO_PROCESSOR** or **DELETE**
- Decision: **DELETE** base class, keep streaming logic in ChatTurnEndpoint

**Deliverable**: `Day1-Base-Class-Audit.md`
- Table format: File | Line Count | Methods | Decision | Justification
- Summary: 4 files to DELETE (718 lines), 1-2 files to SIMPLIFY

**Acceptance Criteria**:
- ✅ All 7 base classes analyzed
- ✅ Every method tagged (KEEP/MOVE/DELETE)
- ✅ Deletion decisions justified
- ✅ Total line reduction calculated (773 → ~300-350)

**Task 1.1.5: Design Processor Responsibilities** (2 hours)
- **Objective**: Detailed spec for each processor to be built in Phase 2

**Processor 1: LoggingPreProcessor** (Global Pre-Processor)
- **File**: `src/Api/Processors/LoggingPreProcessor.cs`
- **Estimated Lines**: 30
- **Responsibilities**:
  - Log incoming request (method, path, userId, correlationId)
  - Use ILogger<T> and Activity for structured logging
  - Extract userId from HttpContext.User
  - Add to Activity tags
- **Interface**: `IPreProcessor<TRequest>`
- **Apply To**: All endpoints (global registration)
- **Dependencies**: ILogger, Activity
- **Test Plan**: Unit test with NSubstitute ILogger

**Processor 2: TracingPreProcessor** (Global Pre-Processor)
- **File**: `src/Api/Processors/TracingPreProcessor.cs`
- **Estimated Lines**: 25
- **Responsibilities**:
  - Create OpenTelemetry activity
  - Enrich trace context (endpoint name, userId, requestId)
  - Set activity tags
- **Interface**: `IPreProcessor<TRequest>`
- **Apply To**: All endpoints (global registration)
- **Dependencies**: ActivitySource, HttpContext
- **Test Plan**: Unit test activity creation

**Processor 3: ETagPostProcessor** (Global Post-Processor)
- **File**: `src/Api/Processors/ETagPostProcessor.cs`
- **Estimated Lines**: 50
- **Responsibilities**:
  - Extract ETag from response entity (IHaveETag interface)
  - Inject ETag header into response
  - Compare with If-None-Match header
  - If match: Call MarkResponseStart(), SendAsync(EmptyResponse, 304, ct)
  - Short-circuit endpoint execution if 304
- **Interface**: `IPostProcessor<TRequest, TResponse>`
- **Apply To**: Endpoints with ETag support (MeEndpoint)
- **Dependencies**: HttpContext, Response entity
- **Test Plan**: Unit test + E2E test for 304 responses
- **Key Code**:
  ```csharp
  public async Task PostProcessAsync(TRequest req, TResponse res, HttpContext ctx, CancellationToken ct)
  {
      if (res is IHaveETag etagEntity && !string.IsNullOrEmpty(etagEntity.ETag))
      {
          ctx.Response.Headers.ETag = etagEntity.ETag;

          if (ctx.Request.Headers.IfNoneMatch == etagEntity.ETag)
          {
              ctx.MarkResponseStart();
              await SendAsync(new EmptyResponse(), 304, ct);
          }
      }
  }
  ```

**Processor 4: CachingHeadersPostProcessor** (Global Post-Processor)
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

**Processor 5: IdentityAuthProcessor** (Module-Specific Pre-Processor)
- **File**: `src/Api/Modules/Identity/Processors/IdentityAuthProcessor.cs`
- **Estimated Lines**: 80-100
- **Responsibilities**:
  - Differentiate Dynamic vs Axon tokens
  - Validate token expiration (FakeTimeProvider support for tests)
  - Enrich HttpContext.User with token metadata
  - Short-circuit with 401 if invalid
- **Interface**: `IPreProcessor<TRequest>`
- **Apply To**: Identity endpoints only
- **Dependencies**: ITimeProvider, HttpContext.User
- **Test Plan**: Unit test + E2E test with FakeTimeProvider
- **Key Code** (extract from BaseIdentityQueryEndpoint.cs:64-81):
  ```csharp
  public async Task PreProcessAsync(TRequest req, HttpContext ctx, CancellationToken ct)
  {
      // Extract exp claim
      var expClaim = ctx.User.FindFirst("exp");
      if (expClaim != null && long.TryParse(expClaim.Value, out var exp))
      {
          var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp);
          var now = _timeProvider.GetUtcNow();

          if (now >= expirationTime)
          {
              await SendUnauthorizedAsync(ct);
              return; // Short-circuit
          }
      }

      // Differentiate Dynamic vs Axon token
      var tokenType = ctx.User.FindFirst("token_type")?.Value;
      ctx.Items["TokenType"] = tokenType; // Enrich for endpoint
  }
  ```

**Processor 6: ChatRateLimitProcessor** (Module-Specific Pre-Processor, FUTURE)
- **File**: `src/Api/Modules/Chat/Processors/ChatRateLimitProcessor.cs`
- **Estimated Lines**: 20-30
- **Status**: Out of scope for this epic, placeholder for future
- **Responsibilities**: Rate limiting for chat endpoints

**Deliverable**: `Day1-Processor-Specifications.md`
- Each processor: Name, file path, responsibilities, interface, dependencies, test plan, key code
- Total estimated lines: ~300-350
- Registration pattern in Program.cs

**Acceptance Criteria**:
- ✅ All 5 processors specified in detail
- ✅ Interfaces identified
- ✅ Dependencies documented
- ✅ Test plans outlined
- ✅ Code reduction validated (773 → 300-350 lines)

#### Success Criteria (Day 1)

**Code Changes**:
- ✅ 304 bug fixed (one line addition)
- ✅ All 14 E2E tests passing
- ✅ Clean commit pushed

**Documentation**:
- ✅ `Day1-FastEndpoints-Study-Notes.md` created
- ✅ `Day1-Target-Architecture.md` created (with diagrams)
- ✅ `Day1-Base-Class-Audit.md` created (detailed table)
- ✅ `Day1-Processor-Specifications.md` created (5 processors specified)

**Knowledge**:
- ✅ Deep understanding of FastEndpoints processors
- ✅ Clear mental model of target architecture
- ✅ Decisions documented for all 7 base classes
- ✅ Ready to implement processors in Phase 2

**Risks Mitigated**:
- ✅ Critical bug fixed immediately (304 responses work)
- ✅ Architecture validated before implementation
- ✅ No surprises in Phase 2 (all decisions made)

#### Notes from Research Report (Section 8)

From the Implementation Roadmap (page 796-833 of research report):

**Morning Session** (4 hours):
- 30 min: Fix 304 bug, run E2E tests, commit
- 2 hours: Study FastEndpoints (pre/post processors, response lifecycle, GitHub issues)
- 1.5 hours: Design target architecture (diagrams + specs)

**Afternoon Session** (4 hours):
- 2 hours: Audit all 7 base classes (line-by-line analysis, tagging)
- 2 hours: Design processor responsibilities (5 processors, detailed specs)

**Total**: 8 hours intensive work

**Deliverable**: Ready for Phase 2 implementation with zero unknowns

---

### Phase 2: Build New Foundation (Days 2-3) - Story 1.2
**Story**: Create Processors and Simplify BaseResultEndpoint

**Tasks**:
- Create LoggingProcessor (pre-processor)
- Create TracingProcessor (pre-processor, OpenTelemetry)
- Create ETagProcessor (post-processor)
- Create CachingHeadersProcessor (post-processor)
- Create IdentityAuthProcessor (module-specific, Dynamic vs Axon tokens)
- Simplify BaseResultEndpoint to ~100 lines (Result pattern only)
- Remove Response property manipulation
- Use SendAsync() methods properly
- Delete unnecessary base classes (4 files)
- Register processors in Program.cs
- Unit test all processors

**Acceptance Criteria**:
- ✅ All processors implemented with unit tests
- ✅ BaseResultEndpoint reduced to <100 lines
- ✅ Old base classes deleted (4 files)
- ✅ No Response property workarounds
- ✅ Processors registered and working
- ✅ Unit tests passing

**Estimated Effort**: 2 days

---

### Phase 3: Migrate All Endpoints (Days 4-5) - Story 1.3
**Story**: Migrate All 8 Endpoints to New Pattern

**Endpoints to Migrate**:
1. **Identity Module** (Day 4):
   - ChallengeEndpoint → BaseResultEndpoint
   - VerifySignatureEndpoint → BaseResultEndpoint
   - ExchangeEndpoint → BaseResultEndpoint
   - RefreshEndpoint → BaseResultEndpoint
   - MeEndpoint → BaseResultEndpoint (remove ETag logic, now in processor)

2. **Chat Module** (Day 5):
   - GetConversationsEndpoint → BaseResultEndpoint
   - GetConversationMessagesEndpoint → BaseResultEndpoint
   - ChatTurnEndpoint → BaseResultEndpoint (streaming, most complex)

**Migration Pattern Per Endpoint**:
- Change inheritance to BaseResultEndpoint only
- Remove manual auth checks (now in IdentityAuthProcessor)
- Remove ETag logic (now in ETagProcessor)
- Remove logging (now in LoggingProcessor)
- Use SendAsync() methods
- Remove unnecessary overrides
- Test immediately after migration

**Acceptance Criteria**:
- ✅ All 8 endpoints inherit from BaseResultEndpoint only
- ✅ All 14 E2E tests passing
- ✅ All unit tests passing
- ✅ Zero workarounds remaining
- ✅ Manual token checks removed
- ✅ ETag logic removed from endpoints
- ✅ Streaming still works (ChatTurnEndpoint)

**Estimated Effort**: 2 days

---

### Phase 4: Polish & Document (Days 6-7) - Story 1.4
**Story**: Code Quality, Testing, and Documentation

**Tasks**:
- Remove all dead code
- Update XML comments
- Run .NET analyzer and fix warnings
- Performance verification (baseline comparison)
- Comprehensive test run (unit + E2E + integration)
- Manual testing via Swagger (all endpoints, ETag, 304, auth, errors)
- Update `Docs/ENGINEERING/guides/patterns/endpoint-patterns.md`
- Add processor documentation
- Create "How to Create New Endpoint" guide
- Update architecture diagrams
- Update ADR-004 from "Proposed" to "Accepted"
- Document actual vs expected results

**Acceptance Criteria**:
- ✅ Code analysis clean (no warnings)
- ✅ All tests passing (unit + E2E + integration)
- ✅ Test coverage >90%
- ✅ Performance verified (same or better than baseline)
- ✅ Manual testing complete (all scenarios verified)
- ✅ Engineering documentation updated
- ✅ ADR-004 accepted and published
- ✅ Ready for production

**Estimated Effort**: 2 days

---

## Acceptance Criteria (Epic-Level)

### Functional
- ✅ All 8 endpoints work correctly
- ✅ 304 Not Modified responses work (ETag support)
- ✅ Authentication works (Dynamic + Axon tokens)
- ✅ Validation works (FluentValidation)
- ✅ Error handling works (ProblemDetails RFC 7807)
- ✅ Streaming works (ChatTurnEndpoint)
- ✅ Rate limiting works
- ✅ Swagger/OpenAPI generation works

### Technical
- ✅ Infrastructure code reduced from 773 to ~300-350 lines (60% reduction)
- ✅ Inheritance hierarchy reduced from 5 to 2-3 layers
- ✅ Zero Response property manipulation workarounds
- ✅ Zero manual token expiration checks
- ✅ BaseResultEndpoint <100 lines
- ✅ Pre/post processors implemented and working
- ✅ All 14 E2E tests passing
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
- **Testing Infrastructure**: 14 E2E tests provide safety net (AVAILABLE)
- **FastEndpoints 7.0.1**: Current framework (INSTALLED)
- **MediatR**: Command/query handling (INSTALLED)
- **FluentValidation**: Request validation (INSTALLED)
- **Result Pattern**: Error handling (IMPLEMENTED)

### External
- **FastEndpoints Documentation**: Pre/post processors, response lifecycle (AVAILABLE)
- **FastEndpoints Community**: Discord, GitHub issues (AVAILABLE)

### Blockers
- ❌ None identified

---

## Risks and Mitigation

| Risk | Likelihood | Impact | Mitigation | Contingency |
|------|------------|--------|------------|-------------|
| Breaking Production | Medium | High | 14 E2E tests + manual testing | git revert + rollback deploy |
| Performance Regression | Low | Medium | Benchmark on Day 6, processors optimized by FastEndpoints | Profile hot paths, adjust processor ordering |
| Unknown FastEndpoints Edge Cases | Low | Medium | Study docs Day 1, review GitHub issues | Ask Discord community, worst case: one-off workaround |
| Timeline Slippage | Low | Low | Buffer built in (5-7 days estimate) | Can ship after Day 5 if docs delayed |
| Regression in Tests | Medium | High | Test after each phase, test each endpoint after migration | Fix immediately, don't batch |

**Overall Risk Level**: Low-Medium (mitigated by comprehensive testing and git revert safety)

---

## Success Metrics

### Quantitative
- **Code Reduction**: 60% (773 → 300-350 lines) ✅ TARGET
- **Layers Reduction**: 5 → 2-3 layers ✅ TARGET
- **Test Pass Rate**: 100% (14/14 E2E + all unit tests) ✅ TARGET
- **Test Coverage**: >90% ✅ TARGET
- **Performance**: Same or better than baseline ✅ TARGET
- **Time to Implement New Endpoint**: Cut in half (no base class archaeology) ✅ TARGET

### Qualitative
- **Debuggability**: Excellent (logic concentrated in fewer files) ✅ TARGET
- **Maintainability**: High (composition over inheritance) ✅ TARGET
- **Developer Experience**: Low learning curve (2-3 layers vs 5) ✅ TARGET
- **Code Quality**: Zero workarounds, clean architecture ✅ TARGET
- **Framework Alignment**: Using FastEndpoints properly (REPR + processors) ✅ TARGET

---

## Technical Decisions

### Decided
- **Framework**: Stay with FastEndpoints 7.0.1 (not migrating)
- **Approach**: Brutal refactoring (no gradual migration, no backward compatibility)
- **Pattern**: REPR (Request-Endpoint-Response) + Vertical Slices
- **Cross-Cutting Concerns**: Pre/post processors (not inheritance)
- **Response Handling**: Use SendAsync() methods (not Response property)
- **Authentication**: Processor-based (not manual checks)
- **Testing**: Comprehensive (14 E2E + unit tests after each phase)

### Architecture Decision Record
- **ADR-004**: Refactor FastEndpoints Base Endpoint Abstractions
- **Status**: Proposed → Will move to Accepted after implementation
- **Location**: Section 9 of technical-research-2025-10-01.md

---

## Documentation

### Source Documentation
- **Technical Research**: `Docs/PROCESS/research/technical-research-2025-10-01.md` (comprehensive 1348-line analysis)
- **ADR**: Section 9 of technical research (will be extracted)

### Documentation to Update
- `Docs/ENGINEERING/guides/patterns/endpoint-patterns.md` - New REPR pattern
- `Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md` - Processor usage
- `Docs/ENGINEERING/guides/codebase/coding-standards.md` - Endpoint standards
- ADR-004 (create and accept)
- Architecture diagrams

### Documentation to Create
- "How to Create New Endpoint" guide
- Processor documentation
- Migration notes (before/after comparison)

---

## Related Work

### Research
- **Performance Comparison**: FastEndpoints vs Minimal APIs vs Carter (Section 4)
- **Real-World Evidence**: Production experiences, known issues (Section 6)
- **Architecture Patterns**: REPR + Vertical Slices (Section 7)

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

### Key Insight from Research
**FastEndpoints is NOT the problem - our implementation is.** The framework is well-designed for Clean Architecture + CQRS + Vertical Slices. We've been fighting it instead of using it properly.

### Why Now?
- **Optimal Timing**: 8 endpoints is perfect refactoring size (at 50+ it becomes painful)
- **Good Safety Net**: 14 E2E tests provide confidence
- **Technical Debt Window**: Pay it off now before compound interest
- **Production Early**: Can afford refactoring now, harder later

### Implementation Philosophy
**Brutal but Safe**: Delete everything unnecessary, migrate all at once, but test comprehensively at every step. Git revert is our safety net.

---

## Story List

1. **Story 1.1**: Fix 304 Bug and Design Target Architecture (Day 1)
2. **Story 1.2**: Create Processors and Simplify BaseResultEndpoint (Days 2-3)
3. **Story 1.3**: Migrate All 8 Endpoints to New Pattern (Days 4-5)
4. **Story 1.4**: Code Quality, Testing, and Documentation (Days 6-7)

**Total**: 4 stories over 5-7 days

---

## Changelog

| Date | Change | Author |
|------|--------|--------|
| 2025-10-02 | Epic created from technical research report | Valik |

---

**Epic Status**: 🎯 Ready for Implementation
**Next Step**: Create Story 1.1 (Fix 304 Bug and Design Target Architecture)
