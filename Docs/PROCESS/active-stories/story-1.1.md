# Story 1.1: Fix 304 Not Modified Bug and Design FastEndpoints Refactoring Architecture

**Status**: 📋 Ready for Implementation
**Epic**: EPIC-001 FastEndpoints Refactoring
**Phase**: 1 of 5 (Foundation & Design)
**Priority**: Critical
**Estimated Effort**: 8 hours (1 day)
**Story Type**: Refactoring (Brutal) + Bugfix

## Story

As a **Backend Developer**,
I want **to design the target FastEndpoints REPR architecture and fix the critical 304 Not Modified bug as proof-of-concept**,
so that **I have a complete blueprint for the 6-8 day brutal refactoring that will reduce infrastructure code by 60% (773 → 300-350 lines) and establish the foundation for Days 2-7 implementation**.

## Context

This is **Phase 1 of EPIC-001** (FastEndpoints Base Endpoint Refactoring). The epic addresses severe over-engineering in the API layer: **773 verified lines of infrastructure code across a 5-layer inheritance hierarchy serving only 8 endpoints** (~97 lines per endpoint).

### Verified Line Counts (from code review):
```
BaseEndpoint.cs:                  63 lines ✅
BaseResultEndpoint.cs:           102 lines ✅
BaseMappedEndpoint.cs:            99 lines ✅
BaseIdentityQueryEndpoint.cs:    273 lines ✅ (LARGEST)
BaseIdentityCommandEndpoint.cs:   90 lines ✅
BaseChatQueryEndpoint.cs:         73 lines ✅
BaseChatCommandEndpoint.cs:       73 lines ✅
────────────────────────────────────────
TOTAL:                           773 lines ✅
```

### Critical Bug (Proof-of-Concept Fix)
**Issue**: 304 Not Modified responses are incorrectly converted to 204 NoContent, breaking HTTP caching semantics.

**Root Cause**: `BaseIdentityQueryEndpoint.cs:212` in `HandleNotModifiedResponse()` is missing `HttpContext.MarkResponseStart()`, causing FastEndpoints to auto-send 204 NoContent.

**One-Line Fix**: Add `HttpContext.MarkResponseStart();` at line 225 (before `StatusCode = 304`)

### Epic Context (6-8 Days)
**Goal**: Brutal refactoring to FastEndpoints REPR pattern with processors:
- **Code Reduction**: 773 → 300-350 lines (60%)
- **Layer Reduction**: 5 → 2-3 layers
- **Approach**: Delete 3 base classes at once, no gradual migration
- **Safety Net**: 57 E2E tests (verified, updated from 14) + git revert

### This Story's Role (Day 1):
- ✅ **Phase 1 of 5** in the epic (Days 1, 2-3.5, 4-5, 6, 7-8)
- ✅ **Zero dependencies** on other phases
- ✅ **Delivers immediate value**: Bug fix + complete architectural blueprint
- ✅ **Establishes foundation**: All decisions made, zero surprises in Phase 2-5

## Acceptance Criteria

### Bug Fix (Proof-of-Concept - 30 minutes)
1. **AC1**: 304 Not Modified bug is fixed - MeEndpoint returns proper 304 status when ETag matches (not 204 NoContent)
2. **AC2**: Fix is verified with existing E2E test `AuthMe_IfNoneMatchWithCurrentETag_ShouldReturn304` (line 88 in AuthMeE2ETests.cs)
3. **AC3**: All 57 E2E tests pass after fix (verified count, updated from 14) - no regressions
4. **AC4**: Manual verification via Swagger shows 304 response with proper headers
5. **AC5**: Clean commit: `fix: Add MarkResponseStart to fix 304 Not Modified bug`

### FastEndpoints Knowledge Acquisition (2 hours)
6. **AC6**: Pre/Post processor lifecycle documented (execution order, short-circuiting, global vs endpoint-specific)
7. **AC7**: `MarkResponseStart()` mechanism understood and documented (when to call, why it fixes 304 bug)
8. **AC8**: `SendAsync()` family methods documented vs Response property manipulation patterns
9. **AC9**: GitHub issues studied (#230, #771, #795) with real-world patterns extracted
10. **AC10**: Study notes created: `Day1-FastEndpoints-Study-Notes.md` with key patterns and code examples

### Architecture Design (1.5 hours)
11. **AC11**: Target architecture documented with clear 2-3 layer hierarchy diagram (vs current 5 layers)
12. **AC12**: BaseResultEndpoint responsibilities defined (ONLY Result<T, Error> mapping + CQRS query framework)
13. **AC13**: Processor architecture designed:
    - Global pre: LoggingPreProcessor (30 lines), TracingPreProcessor (25 lines), **ETagPreProcessor (25 lines)** ⭐ NEW
    - Global post: **ETagPostProcessor (70 lines)** ⭐ COMPLEX, CachingHeadersPostProcessor (30 lines)
    - Module: IdentityAuthProcessor (80-100 lines, preserves FakeTimeProvider for time-travel testing)
14. **AC14**: Processor registration pattern documented for Program.cs
15. **AC15**: Before/after code metrics table shows 60% reduction path (773 → 300-350 lines)
16. **AC16**: Documentation created: `Day1-Target-Architecture.md` with diagrams and specs

### Base Class Audit (2 hours)
17. **AC17**: All 7 base classes analyzed with detailed decisions:
    - BaseEndpoint (63 lines): **MERGE** into BaseResultEndpoint
    - BaseResultEndpoint (102 lines): **SIMPLIFY** to ~120-150 lines (absorbs BaseEndpoint + query framework)
    - BaseMappedEndpoint (99 lines): **DELETE** entirely (inline Mapster mapping)
    - BaseIdentityQueryEndpoint (273 lines): **SIMPLIFY** to ~80-100 lines (NOT delete - pragmatic approach)
    - BaseIdentityCommandEndpoint (90 lines): **DELETE** entirely
    - BaseChatQueryEndpoint (73 lines): **DELETE** entirely (inline pagination)
    - BaseChatCommandEndpoint (73 lines): **DELETE** entirely (keep streaming in endpoint)
18. **AC18**: Each method tagged (KEEP/MOVE/DELETE) with justification
19. **AC19**: Total line reduction calculated: 773 → ~320-350 lines infrastructure
20. **AC20**: Audit document created: `Day1-Base-Class-Audit.md` with detailed table

### Processor Specifications (2 hours)
21. **AC21**: All 6 processors specified in detail (added ETagPreProcessor from review):
    1. LoggingPreProcessor (30 lines) - Global Pre
    2. TracingPreProcessor (25 lines) - Global Pre
    3. **ETagPreProcessor (25 lines) - Global Pre** ⭐ CRITICAL (extract client ETag)
    4. **ETagPostProcessor (70 lines) - Global Post** ⭐ COMPLEX (304 handling + MarkResponseStart)
    5. CachingHeadersPostProcessor (30 lines) - Global Post
    6. IdentityAuthProcessor (80-100 lines) - Module Pre (preserves FakeTimeProvider)
22. **AC22**: Each processor has: file path, responsibilities, interface, dependencies, test plan, key code examples
23. **AC23**: Total processor lines: ~300-350 (validates 60% reduction)
24. **AC24**: ETag Pre+Post combination pattern documented (client ETag extraction → comparison)
25. **AC25**: FakeTimeProvider preservation documented (enables 12 time-based E2E tests)
26. **AC26**: Processor specs created: `Day1-Processor-Specifications.md`

### Completion Validation
27. **AC27**: All 4 documentation files created and complete
28. **AC28**: All decisions documented, zero unknowns for Phase 2
29. **AC29**: Ready for Phase 2 (Days 2-3.5): Processor implementation can start immediately

## Tasks / Subtasks

### Morning Session (4 hours) - Bug Fix + FastEndpoints Study

- [ ] **Task 1.1: Fix 304 Not Modified Bug (Proof-of-Concept)** (30 minutes) - AC: #1-5
  - [ ] **Subtask 1.1.1**: Open `src/Api/Modules/BaseIdentityQueryEndpoint.cs`, navigate to line 212 (`HandleNotModifiedResponse` method)
  - [ ] **Subtask 1.1.2**: Add `HttpContext.MarkResponseStart();` at line 225 (before `StatusCode = 304`)
  - [ ] **Subtask 1.1.3**: Remove Pattern B workaround: Delete `Response = Activator.CreateInstance<TResponse>()` (lines 233-240)
  - [ ] **Subtask 1.1.4**: Run E2E tests: `dotnet test --filter "Category=E2E"`
  - [ ] **Subtask 1.1.5**: Verify 57/57 tests pass (updated from 14), specifically `AuthMe_IfNoneMatchWithCurrentETag_ShouldReturn304` (line 88)
  - [ ] **Subtask 1.1.6**: Manual test via Swagger: GET /api/v1/auth/me with If-None-Match header
  - [ ] **Subtask 1.1.7**: Commit: `fix: Add MarkResponseStart to fix 304 Not Modified bug`
  - [ ] **Subtask 1.1.8**: Push to feature branch `epic-001/phase-1-foundation`

- [ ] **Task 1.2: Study FastEndpoints Pre/Post Processors** (2 hours) - AC: #6-10
  - [ ] **Subtask 1.2.1**: Read FastEndpoints docs: Pre/Post Processors (https://fast-endpoints.com/docs/pre-post-processors)
  - [ ] **Subtask 1.2.2**: Read FastEndpoints docs: Response Lifecycle & Configuration Settings
  - [ ] **Subtask 1.2.3**: Study GitHub Issue #230 (Response lifecycle - **CRITICAL**, directly explains 304 bug)
  - [ ] **Subtask 1.2.4**: Study GitHub Issue #771 (Flexible Error Response Control)
  - [ ] **Subtask 1.2.5**: Study GitHub Issue #795 (ETag Header with Response Type)
  - [ ] **Subtask 1.2.6**: Document processor lifecycle:
    - Execution order (Pre → Endpoint → Post)
    - Short-circuiting (return early from Pre, skip endpoint)
    - Global vs endpoint-specific processors
    - Pre+Post combination patterns (ETag example)
  - [ ] **Subtask 1.2.7**: Document `MarkResponseStart()` mechanism:
    - Why it's needed (prevents FastEndpoints auto-204)
    - When to call (before manually writing to response)
    - How it relates to SendAsync() methods
  - [ ] **Subtask 1.2.8**: Document Response property patterns:
    - **Pattern A** (LEGITIMATE): `Response = problemDetails` after WriteAsJsonAsync (BaseResultEndpoint:91)
    - **Pattern B** (WORKAROUND): `Response = Activator.CreateInstance<TResponse>()` (should use MarkResponseStart instead)
  - [ ] **Subtask 1.2.9**: Document `SendAsync()` family methods (SendOkAsync, SendNoContentAsync, SendAsync with status code)
  - [ ] **Subtask 1.2.10**: Create `Day1-FastEndpoints-Study-Notes.md` with:
    - Key patterns and code examples
    - ETag Pre+Post combination pattern
    - GitHub issues learnings

- [ ] **Task 1.3: Design 2-Layer Target Architecture** (1.5 hours) - AC: #11-16
  - [ ] **Subtask 1.3.1**: Create architecture diagram (ASCII or mermaid) showing:
    - **Current**: 5 layers (BaseEndpoint → BaseResultEndpoint → BaseMappedEndpoint → BaseIdentity*/BaseChat* → Concrete)
    - **Target**: 2-3 layers (FastEndpoints → BaseResultEndpoint → Concrete + Processors)
  - [ ] **Subtask 1.3.2**: Define BaseResultEndpoint final responsibilities:
    - Result<T, Error> → HTTP response mapping
    - CQRS query execution framework (absorbed from BaseMappedEndpoint)
    - Methods: ExecuteAsync, HandleAsync, SendAsync usage
    - Target size: ~120-150 lines (increased from 102 to absorb BaseEndpoint + query framework)
  - [ ] **Subtask 1.3.3**: List what to REMOVE from BaseResultEndpoint:
    - Pattern B Response property workarounds (keep Pattern A - legitimate)
    - All logging (→ LoggingPreProcessor)
    - All auth checks (→ IdentityAuthProcessor)
    - All ETag logic (→ ETag Pre+Post Processors)
  - [ ] **Subtask 1.3.4**: Design processor architecture (6 processors total):
    - **Global pre**: LoggingPreProcessor (30 lines), TracingPreProcessor (25 lines), **ETagPreProcessor (25 lines)** ⭐ NEW
    - **Global post**: **ETagPostProcessor (70 lines)** ⭐ COMPLEX (304 + MarkResponseStart), CachingHeadersPostProcessor (30 lines)
    - **Module**: IdentityAuthProcessor (80-100 lines) - Dynamic vs Axon tokens, FakeTimeProvider support
  - [ ] **Subtask 1.3.5**: Document processor registration pattern in Program.cs (order matters)
  - [ ] **Subtask 1.3.6**: Create before/after code metrics table:
    - Before: 773 lines across 7 files
    - After: ~320-350 lines (BaseResultEndpoint ~120-150 + Processors ~200)
    - Reduction: 60%
  - [ ] **Subtask 1.3.7**: Document BaseMappedEndpoint deletion strategy (inline Mapster in endpoints)
  - [ ] **Subtask 1.3.8**: Create `Day1-Target-Architecture.md` with all diagrams, specs, and metrics

### Afternoon Session (4 hours) - Base Class Audit + Processor Specs

- [ ] **Task 1.4: Audit All 7 Base Classes** (2 hours) - AC: #17-20
  - [ ] **Subtask 1.4.1**: Analyze `src/BuildingBlocks/Web/Endpoints/Base/BaseEndpoint.cs` (63 lines ✅)
    - Tag each method: MOVE_TO_PROCESSOR (LogRequestReceived, LogRequestCompleted, LogRequestFailed)
    - **Decision**: **MERGE** into BaseResultEndpoint, then move logging to LoggingPreProcessor
    - Justification: Thin abstraction, better as processor
  - [ ] **Subtask 1.4.2**: Analyze `src/BuildingBlocks/Web/Endpoints/Base/BaseResultEndpoint.cs` (102 lines ✅)
    - Tag: **KEEP** (Result pattern mapping, ExecuteAsync, HandleAsync)
    - Tag: **KEEP Pattern A** (line 91 - legitimate: `Response = problemDetails` after WriteAsJsonAsync)
    - Tag: **REMOVE** (logging → LoggingPreProcessor)
    - Tag: **ADD** (query execution framework from BaseMappedEndpoint)
    - **Decision**: **SIMPLIFY** to ~120-150 lines (absorbs BaseEndpoint + adds query framework)
    - Justification: Core value provider, will grow slightly to absorb responsibilities
  - [ ] **Subtask 1.4.3**: Analyze `src/BuildingBlocks/Web/Endpoints/Base/BaseMappedEndpoint.cs` (99 lines ✅)
    - Tag: **DELETE** entire file (mapping can be inline in endpoints)
    - **Decision**: **DELETE** - Inline Mapster mapping in endpoints
    - Justification: 99 lines for 8 endpoints is overkill, reduces clarity
    - Migration: Replace with `request.Adapt<TQuery>()` directly (3-5 lines per endpoint)
  - [ ] **Subtask 1.4.4**: Analyze `src/Api/Modules/BaseIdentityQueryEndpoint.cs` (273 lines ✅) ⚠️ LARGEST - **REVISED DECISION**
    - Tag: **MOVE** lines 64-81 (manual token expiration checks → IdentityAuthProcessor) ⭐ Preserves FakeTimeProvider
    - Tag: **MOVE** lines 166-171, 194-206 (ETag extraction/injection → ETag Pre+Post Processors)
    - Tag: **MOVE** lines 212-243 (304 Not Modified logic → ETagPostProcessor)
    - Tag: **MOVE** lines 83, 91, 108-112 (logging → LoggingPreProcessor)
    - Tag: **KEEP** lines 150-187 (query execution & mapping integration - valuable CQRS framework)
    - **Decision**: **SIMPLIFY** to ~80-100 lines (NOT delete - pragmatic approach)
    - Justification: Provides valuable query execution framework for CQRS pattern
  - [ ] **Subtask 1.4.5**: Analyze `src/Api/Modules/BaseIdentityCommandEndpoint.cs` (90 lines ✅)
    - Similar to BaseIdentityQueryEndpoint but minimal value
    - **Decision**: **DELETE** entire file after extracting auth to processor
    - Justification: Minimal value, logic can be inline in command endpoints
  - [ ] **Subtask 1.4.6**: Analyze `src/Api/Modules/BaseChatQueryEndpoint.cs` (73 lines ✅)
    - Pagination logic: Domain-specific, keep inline in concrete endpoints
    - **Decision**: **DELETE** base class, endpoints inherit from BaseResultEndpoint
    - Justification: Pagination is simple, better inline (domain-specific logic)
  - [ ] **Subtask 1.4.7**: Analyze `src/Api/Modules/BaseChatCommandEndpoint.cs` (73 lines ✅)
    - Streaming support (SSE): Complex, endpoint-specific
    - **Decision**: **DELETE** base class, keep streaming logic in ChatTurnEndpoint
    - Justification: Streaming is complex, only 1 endpoint needs it (ChatTurnEndpoint)
  - [ ] **Subtask 1.4.8**: Create `Day1-Base-Class-Audit.md` with detailed table:
    - Columns: File | Lines | Methods | Decision | Justification | Target Lines
    - Summary:
      - **DELETE**: 3 files (BaseMapped, BaseChat*, BaseIdentityCommand) = 245 lines
      - **MERGE**: BaseEndpoint → BaseResultEndpoint
      - **SIMPLIFY**: BaseIdentityQueryEndpoint (273 → ~80-100)
      - **SIMPLIFY**: BaseResultEndpoint (102 → ~120-150, absorbs BaseEndpoint + query framework)
    - Total reduction: 773 → ~320-350 lines (60%)

- [ ] **Task 1.5: Design Processor Responsibilities** (2 hours) - AC: #21-26
  - [ ] **Subtask 1.5.1**: Specify **LoggingPreProcessor** (Global Pre-Processor, 30 lines)
    - File: `src/Api/Processors/LoggingPreProcessor.cs`
    - Responsibilities: Log incoming request (method, path, userId, correlationId)
    - Interface: `IPreProcessor<TRequest>`
    - Dependencies: ILogger, Activity
    - Extract from: BaseEndpoint.LogRequestReceived (line 24-30)
    - Test plan: Unit test with NSubstitute ILogger
  - [ ] **Subtask 1.5.2**: Specify **TracingPreProcessor** (Global Pre-Processor, 25 lines)
    - File: `src/Api/Processors/TracingPreProcessor.cs`
    - Responsibilities: Create OpenTelemetry activity, enrich trace context
    - Interface: `IPreProcessor<TRequest>`
    - Dependencies: ActivitySource, HttpContext
    - Test plan: Unit test activity creation
  - [ ] **Subtask 1.5.3**: Specify **ETagPreProcessor** (Global Pre-Processor, 25 lines) ⭐ NEW - CRITICAL
    - File: `src/Api/Processors/ETagPreProcessor.cs`
    - Responsibilities:
      - Extract client ETag from If-None-Match header
      - Store in HttpContext.Items["ClientETag"] for post-processor comparison
      - Enable early 304 detection (performance optimization)
    - Interface: `IPreProcessor<TRequest>`
    - Dependencies: HttpContext
    - Test plan: Unit test + E2E test for ETag extraction
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
  - [ ] **Subtask 1.5.4**: Specify **ETagPostProcessor** (Global Post-Processor, 70 lines) ⭐ COMPLEX
    - File: `src/Api/Processors/ETagPostProcessor.cs`
    - Estimated Lines: 70 (more complex than original 50-line estimate)
    - Responsibilities:
      - Extract ETag from response entity (IHaveETag interface)
      - Compare with client ETag from HttpContext.Items["ClientETag"]
      - If match: Call `MarkResponseStart()`, send 304 with empty body
      - If no match: Inject ETag header into response
      - ✅ FIXES 304 BUG globally
    - Interface: `IPostProcessor<TRequest, TResponse>`
    - Extract from: BaseIdentityQueryEndpoint lines 166-171, 194-206, 212-243
    - Test plan: Unit test + E2E test `AuthMe_IfNoneMatchWithCurrentETag_ShouldReturn304`
    - **Key Code** (combines Pre+Post pattern):
      ```csharp
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
  - [ ] **Subtask 1.5.5**: Specify **CachingHeadersPostProcessor** (Global Post-Processor, 30 lines)
    - File: `src/Api/Processors/CachingHeadersPostProcessor.cs`
    - Responsibilities: Inject Cache-Control, Vary headers
    - Interface: `IPostProcessor<TRequest, TResponse>`
    - Extract from: BaseIdentityQueryEndpoint line 202
    - Test plan: Unit test header injection
  - [ ] **Subtask 1.5.6**: Specify **IdentityAuthProcessor** (Module-Specific Pre-Processor, 80-100 lines) ⭐ PRESERVES FakeTimeProvider
    - File: `src/Api/Modules/Identity/Processors/IdentityAuthProcessor.cs`
    - Responsibilities:
      - Differentiate Dynamic vs Axon tokens
      - Validate token expiration (FakeTimeProvider support for tests)
      - Enrich HttpContext.User with token metadata
      - Short-circuit with 401 if invalid
    - Interface: `IPreProcessor<TRequest>`
    - Dependencies: ITimeProvider, HttpContext.User
    - Extract from: BaseIdentityQueryEndpoint lines 64-81 (manual JWT expiration checks)
    - Test plan: Unit test + E2E test with FakeTimeProvider
    - **Key Code Example**:
      ```csharp
      public async Task PreProcessAsync(TRequest req, HttpContext ctx, CancellationToken ct)
      {
          // Extract exp claim
          var expClaim = ctx.User.FindFirst("exp")?.Value;
          if (!string.IsNullOrEmpty(expClaim) && long.TryParse(expClaim, out var exp))
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
  - [ ] **Subtask 1.5.7**: Create `Day1-Processor-Specifications.md` with:
    - **All 6 processors** specified (added ETagPreProcessor from review):
      1. LoggingPreProcessor (30 lines) - Global Pre
      2. TracingPreProcessor (25 lines) - Global Pre
      3. **ETagPreProcessor (25 lines) - Global Pre** ⭐ NEW
      4. **ETagPostProcessor (70 lines) - Global Post** ⭐ COMPLEX
      5. CachingHeadersPostProcessor (30 lines) - Global Post
      6. IdentityAuthProcessor (80-100 lines) - Module Pre
    - Each processor: file path, responsibilities, interface, dependencies, test plan, key code examples
    - Total estimated lines: ~300-350
    - ETag Pre+Post combination pattern explained (client extraction → server comparison)
    - FakeTimeProvider preservation documented (enables 12 time-based E2E tests)
    - Registration pattern in Program.cs (order matters: Pre → Endpoint → Post)
    - Validation: 773 → 300-350 lines (60% reduction achieved)

### End-of-Day Deliverables Check

- [ ] **Task 1.6: Validate Day 1 Completion** (30 minutes) - AC: #27-29
  - [ ] **Subtask 1.6.1**: Verify bug fix committed and pushed to `epic-001/phase-1-foundation`
  - [ ] **Subtask 1.6.2**: Verify all 4 documentation files created:
    - `Day1-FastEndpoints-Study-Notes.md` ✅
    - `Day1-Target-Architecture.md` ✅
    - `Day1-Base-Class-Audit.md` ✅
    - `Day1-Processor-Specifications.md` ✅
  - [ ] **Subtask 1.6.3**: Review documentation for completeness against ALL 29 ACs:
    - Bug Fix: AC1-5 (304 fixed, 57 tests pass, committed)
    - Knowledge: AC6-10 (FastEndpoints study notes complete)
    - Architecture: AC11-16 (Target architecture designed)
    - Audit: AC17-20 (All 7 base classes analyzed)
    - Processors: AC21-26 (6 processors specified in detail)
    - Completion: AC27-29 (All deliverables ready)
  - [ ] **Subtask 1.6.4**: Confirm readiness for Phase 2 (Days 2-3.5):
    - All decisions documented ✅
    - Zero unknowns for processor implementation ✅
    - ETag Pre+Post pattern validated ✅
    - FakeTimeProvider preservation confirmed ✅
    - BaseIdentityQueryEndpoint simplification strategy clear ✅

## Dev Notes

### Current Base Class Hierarchy (5 Layers - Anti-Pattern)

```
FastEndpoints.Endpoint<TRequest, TResponse>                      [Framework]
│
├── BaseEndpoint<TRequest, TResponse>                            [64 lines]
│   └─ Logging: LogRequestReceived, LogRequestCompleted, LogRequestFailed
│   └─ File: src/BuildingBlocks/Web/Endpoints/Base/BaseEndpoint.cs
│
└── BaseResultEndpoint<TRequest, TResponse>                      [103 lines]
    └─ Result<T, Error> → HTTP response mapping
    └─ Response property manipulation workarounds (lines 91, 122-129)
    └─ File: src/BuildingBlocks/Web/Endpoints/Base/BaseResultEndpoint.cs
    │
    └── BaseMappedEndpoint<TRequest, TResponse>                  [100 lines]
        └─ Auto-mapping: MapRequest, MapResponse, MapExecuteMap
        └─ File: src/BuildingBlocks/Web/Endpoints/Base/BaseMappedEndpoint.cs
        │
        ├── BaseIdentityQueryEndpoint<TRequest, TResponse, TQuery, TDomainResult>  [274 lines] ⚠️
        │   └─ Manual JWT expiration checks (lines 64-81)
        │   └─ ETag extraction (lines 166-171)
        │   └─ ETag injection (lines 194-206)
        │   └─ 304 Not Modified logic (lines 212-243) 🐛 BUG HERE
        │   └─ Dynamic vs Axon token logic
        │   └─ Structured logging
        │   └─ File: src/Api/Modules/BaseIdentityQueryEndpoint.cs
        │   │
        │   └── MeEndpoint (21 lines) - GET /api/v1/auth/me
        │       └─ File: src/Api/Endpoints/V1/Auth/Queries/MeEndpoint.cs
        │
        ├── BaseIdentityCommandEndpoint<...>                     [~150 lines]
        │   └─ File: src/Api/Modules/BaseIdentityCommandEndpoint.cs
        │   └─ Commands: Challenge, VerifySignature, Exchange, Refresh
        │
        ├── BaseChatQueryEndpoint<...>                           [74 lines]
        │   └─ File: src/Api/Modules/BaseChatQueryEndpoint.cs
        │   └─ Queries: GetConversations, GetConversationMessages
        │
        └── BaseChatCommandEndpoint<...>                         [~100 lines]
            └─ File: src/Api/Modules/BaseChatCommandEndpoint.cs
            └─ Commands: ChatTurn (streaming)
```

**Total Infrastructure**: 773+ lines serving 8 endpoints
**Infrastructure per Endpoint**: ~97 lines
**Maintainability**: Poor (logic scattered across 5 files, 7 base classes)

### Target Architecture (2-3 Layers - REPR Pattern)

```
FastEndpoints.Endpoint<TRequest, TResponse>                      [Framework]
│
└── BaseResultEndpoint<TRequest, TResponse>                      [~80-100 lines, SIMPLIFIED]
    └─ ONLY Result<T, Error> → HTTP response mapping
    └─ Methods: ExecuteAsync, HandleAsync, SendSuccessAsync, SendFailureAsync
    └─ Uses: SendAsync() methods (no Response property manipulation)
    └─ File: src/BuildingBlocks/Web/Endpoints/Base/BaseResultEndpoint.cs
    │
    └── 8 Concrete Endpoints [30-50 lines each]
        ├─ MeEndpoint - GET /api/v1/auth/me
        ├─ ChallengeEndpoint - POST /api/v1/auth/challenge
        ├─ VerifySignatureEndpoint - POST /api/v1/auth/verify-signature
        ├─ ExchangeEndpoint - POST /api/v1/auth/exchange
        ├─ RefreshEndpoint - POST /api/v1/auth/refresh
        ├─ GetConversationsEndpoint - GET /api/v1/chat/conversations
        ├─ GetConversationMessagesEndpoint - GET /api/v1/chat/conversations/{id}/messages
        └─ ChatTurnEndpoint - POST /api/v1/chat/conversations/{id}/turn

+ Processors (Composition over Inheritance) [~300-350 lines total]
  ├─ Global Pre-Processors:
  │  ├─ LoggingPreProcessor (30 lines)
  │  └─ TracingPreProcessor (25 lines)
  │
  ├─ Global Post-Processors:
  │  ├─ ETagPostProcessor (50 lines) ← Fixes 304 bug globally
  │  └─ CachingHeadersPostProcessor (30 lines)
  │
  └─ Module-Specific Processors:
     └─ IdentityAuthProcessor (80-100 lines) ← Dynamic vs Axon token logic
```

**Total Infrastructure**: ~300-350 lines
**Code Reduction**: 60% (773 → 300-350)
**Layers**: 2-3 (vs 5)
**Maintainability**: Excellent (concentrated logic, clear separation)

### The 304 Not Modified Bug - Technical Details

**Location**: `src/Api/Modules/BaseIdentityQueryEndpoint.cs:212-243`

**Current Broken Code** (lines 212-243):
```csharp
private Task HandleNotModifiedResponse(string etag, CancellationToken _)
{
    Logger.LogDebug("Returning 304 Not Modified for ETag: {ETag} (traceId={TraceId})",
        etag, HttpContext.TraceIdentifier);

    // Set headers
    HttpContext.Response.Headers.ETag = $"\"{etag}\"";
    HttpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
    HttpContext.Response.ContentLength = 0;

    // Set status code
    HttpContext.Response.StatusCode = StatusCodes.Status304NotModified;  // Line 225

    // CRITICAL BUG: Missing HttpContext.MarkResponseStart()
    // Without it, FastEndpoints thinks no response was sent and auto-sends 204 NoContent

    // WORKAROUND: Set Response property to non-null (lines 227-240)
    // This workaround is INEFFECTIVE because FastEndpoints checks MarkResponseStart flag
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
- Without `HttpContext.MarkResponseStart()`, FastEndpoints assumes endpoint didn't send response
- Auto-fallback logic sends 204 NoContent (per FastEndpoints conventions)
- Response property manipulation is a red herring - FastEndpoints doesn't check this

**One-Line Fix** (add at line 225):
```csharp
HttpContext.Response.StatusCode = StatusCodes.Status304NotModified;
HttpContext.MarkResponseStart(); // ✅ Tell FastEndpoints we're handling response
```

**Better Fix** (use SendAsync):
```csharp
private Task HandleNotModifiedResponse(string etag, CancellationToken ct)
{
    Logger.LogDebug("Returning 304 Not Modified for ETag: {ETag}", etag);
    HttpContext.Response.Headers.ETag = $"\"{etag}\"";
    HttpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";

    return SendAsync(new EmptyResponse(), 304, ct); // ✅ FastEndpoints handles everything
}
```

**Best Fix** (move to ETagPostProcessor - Phase 2):
```csharp
// In ETagPostProcessor.PostProcessAsync()
if (etag != null && Request.Headers.IfNoneMatch == $"\"{etag}\"")
{
    HttpContext.MarkResponseStart();
    await SendAsync(new EmptyResponse(), 304, ct);
    return; // Short-circuit
}
```

**Reference**: [FastEndpoints GitHub Issue #230](https://github.com/FastEndpoints/FastEndpoints/issues/230) - Identical issue reported and resolved

### Test Coverage for 304 Bug

**Existing E2E Test** (passes after fix):
- File: `tests/Modules/Identity/E2E/AuthMeE2ETests.cs`
- Test: `AuthMe_WithETagHeader_ShouldReturnETagForCaching` (line 66-84)
- Validates: ETag header presence, Cache-Control headers
- **Gap**: Does NOT test 304 Not Modified response (only tests first request)

**Additional Test Needed** (Phase 4):
```csharp
[Test]
public async Task AuthMe_WithMatchingETag_ShouldReturn304NotModified()
{
    // Arrange: Get initial response with ETag
    var (axonToken, _) = await SetupPrincipalWithWallets(validJwt);
    SetAuthorizationHeader(axonToken);
    var initialResponse = await HttpClient.GetAsync("/api/v1/auth/me");
    var etag = initialResponse.Headers.ETag.Tag;

    // Act: Send request with If-None-Match header
    HttpClient.DefaultRequestHeaders.Add("If-None-Match", etag);
    var cachedResponse = await HttpClient.GetAsync("/api/v1/auth/me");

    // Assert: Should return 304 Not Modified
    cachedResponse.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    cachedResponse.Headers.ETag.Tag.ShouldBe(etag);
    (await cachedResponse.Content.ReadAsStringAsync()).ShouldBeEmpty();
}
```

### Project Structure Notes

**Base Endpoint Files** (BuildingBlocks):
- `src/BuildingBlocks/Web/Endpoints/Base/BaseEndpoint.cs` (64 lines)
- `src/BuildingBlocks/Web/Endpoints/Base/BaseResultEndpoint.cs` (103 lines)
- `src/BuildingBlocks/Web/Endpoints/Base/BaseMappedEndpoint.cs` (100 lines)

**Module-Specific Base Endpoints** (Api layer):
- `src/Api/Modules/BaseIdentityQueryEndpoint.cs` (274 lines) ⚠️ LARGEST
- `src/Api/Modules/BaseIdentityCommandEndpoint.cs` (~150 lines)
- `src/Api/Modules/BaseChatQueryEndpoint.cs` (74 lines)
- `src/Api/Modules/BaseChatCommandEndpoint.cs` (~100 lines)

**Concrete Endpoints**:
- Identity: `src/Api/Endpoints/V1/Auth/` (5 endpoints)
  - MeEndpoint.cs (GET /auth/me) - 97 lines, only endpoint with ETag support
  - Challenge, VerifySignature, Exchange, Refresh
- Chat: `src/Api/Endpoints/V1/Chat/` (3 endpoints)
  - GetConversationsEndpoint.cs
  - GetConversationMessagesEndpoint.cs
  - ChatTurnEndpoint.cs (streaming, most complex)

**Test Files**:
- E2E: `tests/Modules/Identity/E2E/AuthMeE2ETests.cs` (14 tests)
- Test Base: `tests/Modules/Identity/E2E/Infrastructure/E2ETestBase.cs`
- Validator: `tests/Modules/Identity/E2E/Infrastructure/AuthMeResponseValidator.cs`

**Documentation**:
- FastEndpoints: `Docs/Libraries/FastEndpoints/IMPLEMENTATION_GUIDE.md`
- Patterns: `Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md`

### Architectural Constraints from Documentation

**From Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md**:
- Must use Result<T, Error> pattern for all operations
- Errors map to HTTP status codes automatically (Error.NotFound → 404, etc.)
- Must use StrongId<T> for type-safe identifiers

**From Docs/Libraries/FastEndpoints/IMPLEMENTATION_GUIDE.md**:
- REPR pattern: Request-Endpoint-Response (vertical slices)
- Pre/Post processors for cross-cutting concerns
- SendAsync() methods preferred over Response property
- Built-in FluentValidation integration

**From CLAUDE.md (Project Instructions)**:
- Modern C#: File-scoped namespaces, records, target-typed new
- Quality gates: 90%+ test coverage, no warnings
- Always prefer editing existing files to creating new ones

### FastEndpoints Study Resources

**Documentation URLs**:
- Pre/Post Processors: https://fast-endpoints.com/docs/pre-post-processors
- Configuration Settings: https://fast-endpoints.com/docs/configuration-settings
- Response Lifecycle: https://fast-endpoints.com/docs/endpoint-lifecycle

**GitHub Issues (Directly Relevant)**:
- Issue #230: Response lifecycle and MarkResponseStart (CRITICAL - directly explains our bug)
- Issue #771: Flexible Error Response Control
- Issue #795: ETag Header with Response Type

### References

**Epic Document**:
- [Source: Docs/PROCESS/research/epic-fastendpoints-refactoring.md]
- Section: Phase 1 (Story 1.1) - Lines 533-879
- Section: Technical Context - Lines 150-505

**Code Files Analyzed**:
- [Source: src/Api/Modules/BaseIdentityQueryEndpoint.cs] - 274 lines, bug at line 212-243
- [Source: src/BuildingBlocks/Web/Endpoints/Base/BaseResultEndpoint.cs] - 103 lines
- [Source: src/BuildingBlocks/Web/Endpoints/Base/BaseMappedEndpoint.cs] - 100 lines
- [Source: src/BuildingBlocks/Web/Endpoints/Base/BaseEndpoint.cs] - 64 lines
- [Source: src/Api/Endpoints/V1/Auth/Queries/MeEndpoint.cs] - 97 lines

**Test Files**:
- [Source: tests/Modules/Identity/E2E/AuthMeE2ETests.cs] - Lines 66-84 (ETag test)

**Documentation**:
- [Source: Docs/Libraries/FastEndpoints/IMPLEMENTATION_GUIDE.md]
- [Source: Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md]

## Change Log

| Date       | Version | Description                                     | Author |
| ---------- | ------- | ----------------------------------------------- | ------ |
| 2025-10-02 | 0.1     | Initial draft with deep technical analysis     | Valik  |

## Dev Agent Record

### Context Reference

Story Context will be generated after story approval via `*story-context` workflow.

### Agent Model Used

Claude Sonnet 4.5 (claude-sonnet-4-5-20250929)

### Completion Notes List

- [ ] Bug fix verified with E2E tests
- [ ] All 4 documentation files created
- [ ] Architecture design reviewed and approved
- [ ] Ready for Phase 2 implementation (Days 2-3)

### File List

**Files to Modify (Bug Fix)**:
- `src/Api/Modules/BaseIdentityQueryEndpoint.cs` (Line 225 - add MarkResponseStart)

**Files to Create (Documentation)**:
- `Day1-FastEndpoints-Study-Notes.md`
- `Day1-Target-Architecture.md`
- `Day1-Base-Class-Audit.md`
- `Day1-Processor-Specifications.md`

**Files to Analyze** (Read-only for audit):
- `src/BuildingBlocks/Web/Endpoints/Base/BaseEndpoint.cs`
- `src/BuildingBlocks/Web/Endpoints/Base/BaseResultEndpoint.cs`
- `src/BuildingBlocks/Web/Endpoints/Base/BaseMappedEndpoint.cs`
- `src/Api/Modules/BaseIdentityCommandEndpoint.cs`
- `src/Api/Modules/BaseChatQueryEndpoint.cs`
- `src/Api/Modules/BaseChatCommandEndpoint.cs`

**Test Files** (Verify passing):
- `tests/Modules/Identity/E2E/AuthMeE2ETests.cs`
