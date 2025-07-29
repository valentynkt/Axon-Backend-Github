# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@Docs/ARCHITECTURE-FOLDERS.md

## Project Overview
Axon Backend - **Modular Monolith** using Clean Architecture + DDD + CQRS patterns
- **Stack**: .NET 10 (preview), MediatR, OpenAI Direct MCP
- **Goal**: Ship fast via vertical slices, maintain seams for microservice extraction
- **Philosophy**: Feature-first modules, strict dependency rules, YAGNI principle
## Start Here — Index & Guidance (Authoritative)

**Purpose.** This file is the entry point. **Do not duplicate content** that exists elsewhere — **follow these indexes** and only pull what’s needed.

### Aliases

* `@Docs/*` → `docs/*`
* `@Docs/Claude/*` → `Claude/*`  *(Claude Guidance pack)*

### Primary Indexes (open first)

* **Global docs index:** `@Docs/INDEX.md`  — map of all feature docs, ADRs, contracts, PR bodies.
* **Claude guidance index:** `@Docs/Claude/docs-and-gates-overview.md` — folders, artifacts, and gate workflow.

### Claude Guidance Pack (read as needed)

* **Architecture rules & layout:** `@Docs/Claude/ARCHITECTURE-FOLDERS.md`
* **Top‑down, slice‑first method:** `@Docs/Claude/AXON_TOP_DOWN_SLICE_FIRST.md`
* **(Optional) Workflows index:** `.claude/workflows/` — advanced orchestrations (deep slice, API evolution, boundary surgery, integration spike, hotpath hardening).

### Read Order (per task)

1. Open `@Docs/INDEX.md` → locate the feature folder and related ADR/Contract.
2. Open `@Docs/Claude/docs-and-gates-overview.md` → confirm required artifacts & current **Gate**.
3. Open the feature’s docs in `docs/features/<Module>/<Feature>/` needed for this Gate only.
4. If repo‑wide rules are needed, consult `@Docs/Claude/ARCHITECTURE-FOLDERS.md` (dependencies, boundaries) and `@Docs/Claude/AXON_TOP_DOWN_SLICE_FIRST.md` (method).

### Rules of Engagement

* **Prefer references over recreation.** Link to existing artifacts; don’t restate them.
* **Minimize context.** Read only the sections required for the current Gate.
* **Honor gates.** Don’t proceed to the next Gate until artifacts/criteria are satisfied (see Guidance overview).
* **Use indexes.** If a document isn’t linked from `@Docs/INDEX.md`, treat it as non‑authoritative until indexed.

---

## Agents & Workflows Index (Authoritative)

> Add this section to `CLAUDE.md` under the “Start Here — Index & Guidance” header.

### Sub‑Agents (project‑scoped)

* **spec‑analyst** — `./.claude/agents/spec-analyst.md`
  **Gate:** G1 → `REQUIREMENTS.md` (ACs, Non‑Goals, blocking Qs).
* **system‑designer\&planner** — `./.claude/agents/system-designer-planner.md`
  **Gate:** G1 → `ARCHITECTURE.md`, `TASK_PLAN.md` (+ ADR if boundaries/contracts change).
* **slice‑implementer** — `./.claude/agents/slice-implementer.md`
  **Gate:** G2 → minimal diffs; emits implementation summary; flags `api_surface_changed`.
* **test‑guardian** — `./.claude/agents/test-guardian.md`
  **Gate:** G2 → `TEST_REPORT.md` (delta coverage on touched files).
* **review‑coach** — `./.claude/agents/code-review-coach.md`
  **Gate:** G3 (advisory) → `REVIEW_REPORT.md` (Top‑3 micro‑refactors).
* **policy‑enforcer** — `./.claude/agents/policy-enforcer.md`
  **Gate:** G3 (blocking) → `POLICY_REPORT.md` (PASS/FAIL; severities; fixes).
* **docs‑grounder** — `./.claude/agents/docs-grounder.md`
  **Gate:** G1 (as needed) → `RESEARCH_NOTES.md`; retrieval order: `docs/references/**` → Context7 → Perplexity → FireCrawl.
* **release‑steward** — `./.claude/agents/release-steward.md`
  **Gate:** Ship → `PR_BODY.md` + `DECISION_LOG.md` update + contract sync; verifies **build/tests/health**.
* **work‑completion‑summary (ALWAYS CALL AT THE END)** — `./.claude/agents/work-completion-summary.md`
  **Gate:** post‑Ship (or session end) → emits a concise session summary (artifacts created, decisions, blockers, next best actions, links). **Always call after any workflow.**

**Default call graph & parallelism**

* **G1:** `spec‑analyst` → `system‑designer&planner` (+ `docs‑grounder` if unknowns).
* **G2 (parallel):** `slice‑implementer` **||** `test‑guardian` (start guardian after first diff).
* **G3 (parallel):** `review‑coach` **||** `policy‑enforcer`.
* **Ship:** `release‑steward` → then **always** `work‑completion‑summary`.

---

### Workflows (open and follow)

* **Deep Slice Delivery** — `./.claude/workflows/01-deep-slice-delivery.md`
  New vertical slice E2E; emphasizes small diffs, early parallel tests, and strict gates.
* **API Surface Evolution** — `./.claude/workflows/02-api-surface-evolution.md`
  Safe endpoint/DTO changes; contract tests, versioning, and contract sync at Ship.
* **Boundary Surgery Refactor** — `./.claude/workflows/03-boundary-surgery-refactor.md`
  Structural moves across modules; atomic steps, adapters/shims, ADR.
* **Hotpath Performance Hardening** — `./.claude/workflows/05-hotpath-performance-hardening.md`
  Measure→change→verify; SLO‑driven improvements with perf evidence in PR.

> **Rule:** After completing any workflow, **invoke `work‑completion‑summary`** to capture outcomes and next actions.

---

### Gate→Artifact Map (quick reference)

* **G1:** `REQUIREMENTS.md`, `ARCHITECTURE.md`, `TASK_PLAN.md` (+ `RESEARCH_NOTES.md` if used).
* **G2:** `TEST_REPORT.md` (delta coverage), implementation summary.
* **G3:** `REVIEW_REPORT.md`, `POLICY_REPORT.md` (PASS/FAIL).
* **Ship:** `PR_BODY.md`, `DECISION_LOG.md`, updated `contracts/<Module>/API_CONTRACT.md`, **build/tests/health evidence**.


## Essential Commands
```bash
# Build & Run
dotnet build                    # Build entire solution (warnings = errors)
dotnet run --project src/Api    # Run API locally
dotnet test                     # Run all tests

# Solution uses Axon.Backend.slnx format
```

## Architecture Rules (ENFORCED)
✅ **ALLOWED:**
- Api → Modules.*.Application + Shared.*
- Modules.*.Application → Modules.*.Domain + Shared.*  
- Modules.*.Infrastructure → Modules.*.Application

❌ **FORBIDDEN:**
- Api → Domain (never)
- Cross-module references
- Business code in Shared/*

## C# Coding Style & Best Practices
**MANDATORY**: Apply SOLID, KISS, YAGNI, DRY principles consistently

### Modern C# Standards
- **Records** for DTOs/Value Objects: `public record UserDto(string Name, string Email);`
- **Primary constructors** for classes when appropriate
- **File-scoped namespaces**: `namespace Axon.Modules.Chat;`
- **Target-typed new**: `List<string> items = new();`
- **Pattern matching** over traditional if/switch when cleaner
- **Nullable reference types** enabled - handle nulls explicitly
- **Minimal APIs** for endpoints with proper validation

### Clean Architecture Patterns
```csharp
// ✅ CORRECT: Command/Handler pattern
public record ProcessMessageCommand(string Message, Guid UserId) : IRequest<Result<string>>;

public sealed class ProcessMessageHandler : IRequestHandler<ProcessMessageCommand, Result<string>>
{
    private readonly IAiClient _aiClient;
    
    public ProcessMessageHandler(IAiClient aiClient) => _aiClient = aiClient;
    
    public async Task<Result<string>> Handle(ProcessMessageCommand request, CancellationToken ct)
    {
        // Clean, focused logic
        return await _aiClient.ProcessAsync(request.Message, ct);
    }
}
```

### CQRS with MediatR
- **Commands**: Modify state, return `Result<T>` or `Result`
- **Queries**: Read-only, return data DTOs
- **Handlers**: One responsibility, inject dependencies via constructor
- **Validators**: Use FluentValidation, validate at Application boundary
- **Behaviors**: Cross-cutting concerns (logging, validation, caching)

### Domain Design 

Just Example:
```csharp
// ✅ CORRECT: Value Object with validation
public readonly record struct MessageId(Guid Value)
{
    public static MessageId New() => new(Guid.NewGuid());
    public static Result<MessageId> Create(Guid value) =>
        value == Guid.Empty ? Error.Validation("MessageId cannot be empty") : new MessageId(value);
}

// ✅ CORRECT: Aggregate root
public sealed class Conversation : AggregateRoot<ConversationId>
{
    private readonly List<Message> _messages = [];
    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();
    
    public Result AddMessage(string content, UserId userId)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Error.Validation("Message content cannot be empty");
            
        _messages.Add(new Message(MessageId.New(), content, userId, DateTime.UtcNow));
        return Result.Success();
    }
}
```

### Error Handling
- **Domain**: Return `Result<T>` for business rule violations
- **Application**: Handle domain results, don't throw for business logic
- **Infrastructure**: Wrap external exceptions in Result pattern
- **API**: Map Results to appropriate HTTP responses

```csharp
// ✅ CORRECT: Result pattern usage
public async Task<Result<ConversationDto>> Handle(GetConversationQuery query, CancellationToken ct)
{
    var conversationId = ConversationId.Create(query.Id);
    if (conversationId.IsFailure)
        return conversationId.Error;
        
    var conversation = await _repository.GetByIdAsync(conversationId.Value, ct);
    return conversation is null 
        ? Error.NotFound("Conversation not found")
        : conversation.ToDto();
}
```

### Dependency Injection
- **Constructor injection** only
- **Interface segregation** - small, focused interfaces
- **Lifetime management**: Scoped for DbContext, Singleton for stateless services
- **Registration**: Use extension methods in Infrastructure layer

## Development Workflow
**IMPORTANT**: Always follow top-down, slice-first approach per `@Docs/AXON_TOP_DOWN_SLICE_FIRST.md`

1. **Planning Phase**: Use related SubAgent
2. **Implementation**: Skeleton → MVP slice → Tests
3. **Files Pattern**:
   ```
   src/Api/Endpoints/<Module>/<Feature>Endpoint.cs
   src/Api/Contracts/<Module>/<Feature>Requests.cs
   src/Modules/<Module>/Application/Commands/<Feature>/
   src/Modules/<Module>/Infrastructure/<Area>/
   ```

## Available MCP Tools
**Use these tools for enhanced development workflow:**

### Context7 (`mcp__context7-mcp`)
- **Purpose**: Documentation grounding with official library docs
- **Usage**: Research current best practices, API references, framework guidance
- **Commands**: `resolve-library-id`, `get-library-docs`

### Serena (`mcp__serena`)  
- **Purpose**: Deep codebase analysis and intelligent search
- **Usage**: Find symbols, analyze architecture, search patterns, refactor code
- **Commands**: `find_symbol`, `search_for_pattern`, `get_symbols_overview`, `replace_symbol_body`

### Desktop Commander (`mcp__desktop-commander`)
- **Purpose**: File operations, process management, system commands
- **Usage**: File editing, directory operations, running processes, build automation
- **Commands**: `read_file`, `write_file`, `edit_block`, `start_process`, `list_directory`

## Project Configuration
**Central Config** (`Directory.Build.props`):
- TFM: net10.0, Nullable: enable, ImplicitUsings: enable
- Strict analyzers: TreatWarningsAsErrors=true, AnalysisLevel=latest
- CA1716 suppressed for "Shared" namespace usage

