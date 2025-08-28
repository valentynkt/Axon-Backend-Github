# Axon Backend — Folders, Ownership & Rules

**Goal:** Ship fast as a **monolith**, keep seams for a **modular monolith**, and make a later **microservice** split a file‑move—not a rewrite.
**Principles:** Clean Architecture, DDD, CQRS, *feature‑first* modules, minimum surface area.

---

## 0) Top‑Level Layout (authoritative)

```
src/
  Api/
  Shared/
    Common/
    Common.Abstractions/
  Modules/
    Chat/
      Application/
      Domain/
      Infrastructure/
tests/
```

**Intent**

* `Api/` — HTTP host (Minimal API/controllers), request/response DTOs, composition root (DI).
* `Shared/` — Tiny, technical, re‑usable primitives & contracts **without business language**.

    * `Common/` → primitives (Result, Error, ValueObject, Strong IDs, etc.).
    * `Common.Abstractions/` → technical interfaces usable across modules (e.g., `IClock`).
* `Modules/<BoundedContext>/` — One folder per bounded context, split by **layer**:

    * `Application/` → commands/queries/ports/validators; coordinates the domain.
    * `Domain/` → aggregates/entities/VOs/policies/domain events; no framework deps.
    * `Infrastructure/` → adapters implementing Application ports (LLM, persistence, HTTP).
* `tests/` — Mirrors production code (project per layer or per module, see §8).

> Add new bounded contexts under `src/Modules/` (e.g., `Portfolio/` later). Do **not** place business code in `Shared/`.

---

## 1) Allowed Dependencies (hard rules)

**Per module (e.g., `Chat`)**

```
Api  ───►  Modules.Chat.Application
Modules.Chat.Application ───► Modules.Chat.Domain
Modules.Chat.Application ───► Shared.Common, Shared.Common.Abstractions
Modules.Chat.Infrastructure ─► Modules.Chat.Application
Modules.Chat.Infrastructure ─► Modules.Chat.Domain   (allowed when needed for mapping)
```

**Prohibited**

* `Api` → `Domain` (never).
* Any cross‑module references (e.g., `Chat.Application` → `Portfolio.Domain`) — use **integration events** or an **Application‑level abstraction**.
* `Shared/*` must not depend on any `Modules/*`.

**DI composition**

* `Api` is the *only* place wiring implementations to Application ports.
* Infrastructure exposes `ServiceRegistration` (module‑scoped) consumed by `Api`.

---

## 2) Module Internals (what goes where)

### Application (CQRS)

```
src/Modules/Chat/Application/
  Abstractions/            // ports (interfaces) exposed by the app layer
    IAiClient.cs
    IConversationState.cs  // if persistence later
  Commands/
    ProcessMessage/
      ProcessMessage.cs               // request/command
      ProcessMessageHandler.cs        // MediatR handler
      ProcessMessageValidator.cs      // optional (FluentValidation)
  Queries/                 // add when needed
  Behaviors/               // add in Phase-2+ (Validation/Timeout/Observability)
  DTOs/                    // app-level DTOs (internal to module)
```

**Rules**

* Handlers orchestrate domain & ports; **no** external SDK types leak out.
* Port interfaces live here; concrete adapters live in Infrastructure.
* Keep commands/queries in **feature folders** (as above).

### Domain (DDD)

```
src/Modules/Chat/Domain/
  Aggregates/
    Conversation/
      Conversation.cs
      Message.cs
      ConversationId.cs
      Policies/
  ValueObjects/
  Services/                // domain services (pure)
  Events/                  // domain events (internal)
  Specifications/
```

**Rules**

* Pure C#; no framework/infrastructure packages.
* Business errors via `Shared.Common.Error`.
* Domain returns `Result` for business failures; reserve `throw` for invariants truly unrecoverable in current flow.

### Infrastructure (Adapters)

```
src/Modules/Chat/Infrastructure/
  Ai/
    OpenAiClient.cs             // implements IAiClient
  Persistence/                  // later: EF Core DbContext, Repositories
  Projections/                  // read models, later
  Configuration/
    ServiceRegistration.cs      // module-scoped DI
```

**Rules**

* Implements Application ports; may reference Domain types for mapping/persistence.
* No business logic; keep it glue‑code and configuration.
* External SDKs live *only* here (OpenAI/Azure SDKs, HttpClient handlers, EF).

---

## 3) Api Layer (endpoints & contracts)

```
src/Api/
  Endpoints/
    Chat/
      ProcessMessageEndpoint.cs     // maps POST /api/chat/process
  Contracts/
    Chat/
      ChatRequests.cs               // request DTO(s)
      ChatResponses.cs              // response DTO(s)
  Configuration/
    ServiceRegistration.cs          // cross-cutting host config
  Program.cs
```

**Rules**

* **DTOs** are API‑only; do **not** use domain entities in I/O.
* Map endpoint → MediatR command/query.
* Keep per‑feature endpoint classes (vertical slice feel).
* `Api` references only `Modules.*.Application` and `Shared.*`.

---

## 4) Shared Guidelines

### `Shared/Common/` (primitives only)

* `Result`, `Error`, `ValueObject`, Strongly‑typed IDs, `Maybe`, small guard helpers.
* **No** date/time providers here (they’re abstractions).
* **No** business terms or module knowledge.

### `Shared/Common.Abstractions/` (technical contracts)

* `IClock`, `IIdGenerator`, `IEventPublisher` (if truly cross‑cutting).
* If an abstraction is **module‑specific**, keep it under `Modules/<X>/Application/Abstractions`, not here.

---

## 5) Cross‑Module Communication (future‑safe today)

* Inside a module: raise **Domain Events** (internal) → handled within the same module.
* Across modules (now in‑proc, later via broker): publish **Integration Events** from the **Application layer**.

    * Create `Modules/<X>/Application/Integration/Events/<Name>Occurred.cs`.
    * Consumers in other modules handle via **Application handlers** (no domain coupling).
* If you later extract a module, you can move these event types to `Modules.<X>.Contracts` package.

---

## 6) Naming & Conventions

* **Namespaces:** `Axon.Modules.<Module>.<Layer>.<Area>`

    * `Axon.Modules.Chat.Application.Commands.ProcessMessage.ProcessMessageHandler`
    * `Axon.Modules.Chat.Domain.Aggregates.Conversation.Conversation`
* **Files:** one type per file; handler folders named after the feature (`ProcessMessage`).
* **Project names (when/if you split):**

    * `Axon.Modules.Chat.Application`, `Axon.Modules.Chat.Domain`, `Axon.Modules.Chat.Infrastructure`
    * `Axon.Shared.Common`, `Axon.Shared.Common.Abstractions`, `Axon.Api`

---

## 7) Evolution Path (no surgery required)

**Monolith (now):** one solution, modules as folders.
**Modular Monolith (when a second module appears or a module grows):**

1. Create projects: `Axon.Modules.<X>.Application/Domain/Infrastructure`.
2. Move the corresponding folders verbatim.
3. Update references per graph in §1.
4. Keep `Api` referencing only **Application** projects.
   **Microservices (if required):**

* Pull the module projects into their own repo; keep **Integration Events** as the contract; replace in‑proc dispatch with a broker.

---

## 8) Tests

```
tests/
  Api.Tests/                           // endpoint-level tests (in-process host)
  Modules.Chat.Application.Tests/      // handler/behavior tests
  Modules.Chat.Domain.Tests/           // aggregates/invariants
  Modules.Chat.Infrastructure.Tests/   // adapters (with fakes)
```

**Rules**

* Prefer **behavioral tests** (command → observable outcome).
* Builders live with the module’s test project.
* Keep test names descriptive: `ProcessMessage_ShouldReturnAssistantText_GivenValidInput`.

---

## 9) Source Control & Docs

* Every architectural decision → short **ADR** in `docs/adr/ADR-YYYYMMDD-<slug>.md` (Context, Decision, Consequences).
* Commits reference ADRs or issues: `feat(chat): implement ProcessMessage [ADR-20250729-MCP]`.

---

## 10) AI & Prompting Etiquette (for Claude/Copilot)

* Always include **Scope** (module + layer), **Done = …**, and the **target path**.

* Example:

  > *Scope:* Modules.Chat – Application.
  > *Task:* Create `Commands/ProcessMessage` command + handler that calls `IAiClient`.
  > *Paths:* `src/Modules/Chat/Application/Commands/ProcessMessage/*`.
  > *Done =* handler returns assistant text; compiles.

* Never create files outside the module’s folder without explicit instruction.

* Do not add cross‑module references; propose an **Integration Event** instead.

---

## 11) What NOT to do

* No business code in `Shared/`.
* No `Api` → `Domain` references.
* No cross‑module calls; communicate via Application events/abstractions.
* No generic base classes “for future” in `Common/`. Add only when repeated 3×.

---

## 12) Minimal Examples (paths are normative)

**Command (Application)**

```
src/Modules/Chat/Application/Commands/ProcessMessage/ProcessMessage.cs
src/Modules/Chat/Application/Commands/ProcessMessage/ProcessMessageHandler.cs
```

**Port + Adapter**

```
src/Modules/Chat/Application/Abstractions/IAiClient.cs
src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs
```

**Aggregate**

```
src/Modules/Chat/Domain/Aggregates/Conversation/Conversation.cs
```

**Endpoint**

```
src/Api/Endpoints/Chat/ProcessMessageEndpoint.cs
src/Api/Contracts/Chat/ChatRequests.cs
src/Api/Contracts/Chat/ChatResponses.cs
```

---

## 13) Versioning & Packaging (later)

* If/when you publish libraries, name them exactly as projects (e.g., `Axon.Modules.Chat.Application`).
* Integration contracts may move to `Axon.Modules.Chat.Contracts` if shared across services.

---

### Final Word

Build **per module**, **per layer**, keep **dependencies one way**, and keep `Shared/` tiny. This gives you clean MVP velocity now and the most painless road to modular monolith and—if needed—microservices.

---