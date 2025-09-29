# 🎯 Axon Backend Documentation - Start Here

**Welcome to Axon Backend technical documentation.** This is your mandatory entry point for both humans and AI agents.

---

## 🚀 Quick Navigation

### For New Developers
1. **First Time?** → [Getting Started](./guides/workflows/getting-started.md)
2. **Understanding the System** → [System Overview](./guides/architecture/system-overview.md)
3. **Building Features** → Choose your module:
   - [Identity Module](./modules/identity/00-MODULE-README.md) - Authentication & authorization
   - [Chat Module](./modules/chat/00-MODULE-README.md) - Messaging & conversations

### For AI Agents
- **Quick Lookup** → [Pattern Quick Reference](./guides/patterns/00-QUICK-REFERENCE.md) 🔥
- **Common Workflows** → [AI Common Workflows](./ai-context/common-workflows.md)
- **Module Map** → [Module Boundaries](./ai-context/module-boundaries-map.md)

### For Experienced Developers
- **Pattern Reference** → [Quick Reference](./guides/patterns/00-QUICK-REFERENCE.md) 🔥
- **Coding Standards** → [Coding Standards](./guides/codebase/coding-standards.md)
- **Testing Guide** → [Testing Overview](./testing/README.md)

---

## 📂 Documentation Structure

```
Docs/
├── 00-START-HERE.md              ← YOU ARE HERE
│
├── guides/                        # 🆕 ALL developer guides (consolidated)
│   ├── README.md                  # Master hub for all guides
│   ├── architecture/              # System design (high-level)
│   │   ├── system-overview.md     # Architecture vision
│   │   ├── modular-monolith.md    # Module boundaries
│   │   ├── clean-architecture.md  # Layer structure
│   │   ├── tech-stack.md          # Technology choices
│   │   └── adrs/                  # Architecture decisions
│   │
│   ├── patterns/                  # Implementation patterns
│   │   ├── 00-QUICK-REFERENCE.md  # 🔥 HOT PATH: Pattern cheatsheet
│   │   ├── cqrs.md                # Command/Query with MediatR
│   │   ├── domain-modeling.md     # DDD, Result<T>, StrongId<T>
│   │   ├── error-handling.md      # Error patterns
│   │   └── validation.md          # FluentValidation
│   │
│   ├── codebase/                  # Code organization
│   │   ├── source-tree.md         # Project structure
│   │   └── coding-standards.md    # C# conventions
│   │
│   └── workflows/                 # Developer processes
│       ├── getting-started.md     # Onboarding
│       ├── git-workflow.md        # Git conventions
│       ├── debugging-guide.md     # Troubleshooting
│       └── ide-setup/             # Tool configuration
│
├── modules/                       # Module-specific documentation
│   ├── identity/                  # Identity & Authentication
│   │   ├── 00-MODULE-README.md    # Module entry point
│   │   ├── 01-domain-model.md     # Domain design
│   │   └── ... (9 files total)
│   │
│   └── chat/                      # Chat & Messaging
│       ├── 00-MODULE-README.md    # Module entry point
│       └── ... (9 files total)
│
├── infrastructure/                # Technical implementation
│   ├── persistence/               # Database, EF Core
│   ├── caching/                   # Memory + Redis
│   ├── security/                  # Auth, secrets
│   ├── observability/             # Logging, metrics, tracing
│   └── resilience/                # Retry, circuit breakers
│
├── testing/                       # Testing practices
├── deployment/                    # Operations & environments
├── api/                          # REST API standards
├── integrations/                  # External services (Dynamic, Helius)
└── ai-context/                    # AI optimization layer
```

---

## 🎯 Common Tasks → Documentation Map

| Task | Documentation Path |
|------|-------------------|
| **Add authentication to endpoint** | `modules/identity/03-authentication.md` |
| **Create new command** | `guides/patterns/cqrs.md` + `modules/{module}/02-use-cases.md` |
| **Understand Result<T> pattern** | `guides/patterns/domain-modeling.md` |
| **Add caching to query** | `infrastructure/caching/strategy.md` |
| **Fix concurrency error** | `infrastructure/persistence/concurrency-handling.md` |
| **Write integration test** | `testing/integration-testing-guide.md` |
| **Setup local environment** | `deployment/environments/development.md` |
| **Understand module boundaries** | `guides/architecture/modular-monolith.md` |
| **Follow code style** | `guides/codebase/coding-standards.md` |
| **Handle errors properly** | `guides/patterns/error-handling.md` |

---

## 🏗️ Architecture at a Glance

**Style**: Modular Monolith with Clean Architecture + DDD + CQRS

**Key Patterns**:
- **Result<T, Error>**: Error handling without exceptions
- **StrongId<T>**: Type-safe entity identifiers
- **CQRS**: Command/Query separation via MediatR
- **Clean Architecture**: Domain → Application → Infrastructure → API

**Tech Stack**:
- .NET 10 + C# 13
- FastEndpoints + MediatR + FluentValidation
- PostgreSQL + EF Core 9
- OpenTelemetry + Serilog

**Modules**:
- **Identity**: User authentication, JWT, wallet integration (Dynamic.xyz)
- **Chat**: Real-time messaging, conversations, participants

---

## 📚 Documentation Principles

This documentation follows:
- **Progressive Disclosure**: Start with overview, drill down as needed
- **Single Source of Truth**: Each concept documented once
- **Hub-and-Spoke**: README files are navigation hubs, not content
- **20/80 Rule**: Hot-path docs separate from reference material
- **AI-Optimized**: Clear entry points, logical structure for context retrieval

---

## 🤖 For AI Agents: Context Loading Strategy

**Step 1**: Load this file (`00-START-HERE.md`)
**Step 2**: Load `guides/patterns/00-QUICK-REFERENCE.md` (🔥 HOT PATH)
**Step 3**: Identify task category (auth, messaging, caching, etc.)
**Step 4**: Load relevant guide or module hub
**Step 5**: Load specific implementation doc as needed

**Quick Reference**: Always check `guides/patterns/00-QUICK-REFERENCE.md` first for common patterns.

---

## ⚡ Next Steps

**New to Axon?**
→ Read [Getting Started](./guides/workflows/getting-started.md)
→ Read [System Overview](./guides/architecture/system-overview.md)
→ Explore [Identity Module](./modules/identity/00-MODULE-README.md)

**Building a Feature?**
→ Check [Quick Reference](./guides/patterns/00-QUICK-REFERENCE.md) 🔥
→ Review [Coding Standards](./guides/codebase/coding-standards.md)
→ Read relevant module docs

**Debugging an Issue?**
→ Check [Quick Reference](./guides/patterns/00-QUICK-REFERENCE.md)
→ Review infrastructure docs (`infrastructure/`)
→ See [Debugging Guide](./guides/workflows/debugging-guide.md)

---

**Last Updated**: 2025-01-29
**Maintained By**: Axon Engineering Team