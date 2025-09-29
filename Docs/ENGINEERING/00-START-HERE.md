# 🎯 Axon Backend Documentation - Start Here

**Welcome to Axon Backend technical documentation.** This is your mandatory entry point for both humans and AI agents.

---

## 🚀 Quick Navigation

### For New Developers
1. **First Time?** → [Getting Started](./guides/workflows/getting-started.md)
2. **Understanding the System** → [System Overview](./guides/architecture/system-overview.md)
3. **Building Features** → Choose your module:
   - [Identity Module](./modules/identity/00-INDEX.md) - Authentication & authorization
   - [Chat Module](./modules/chat/00-INDEX.md) - Messaging & conversations

### For AI Agents
- **Quick Lookup** → [Pattern Quick Reference](./guides/patterns/00-QUICK-REFERENCE.md) 🔥
- **System Architecture** → [System Overview](./guides/architecture/system-overview.md)
- **Module Boundaries** → [System Overview - Module Map](./guides/architecture/system-overview.md#module-map)

### For Experienced Developers
- **Pattern Reference** → [Quick Reference](./guides/patterns/00-QUICK-REFERENCE.md) 🔥
- **Coding Standards** → [Coding Standards](./guides/codebase/coding-standards.md)
- **Testing Guide** → [Testing Workflow](./guides/workflows/testing-workflow.md)

---

## 📂 Documentation Structure

```
Docs/
├── 00-START-HERE.md              ← YOU ARE HERE
│
├── guides/                        # ALL developer guides
│   ├── 00-INDEX.md                # Master hub for all guides
│   ├── architecture/              # System design (high-level)
│   │   ├── system-overview.md     # 🔥 Architecture + C4 diagrams
│   │   ├── tech-stack.md          # Library reference by layer
│   │   └── adrs/                  # 6 Architecture Decision Records
│   │
│   ├── patterns/                  # Implementation patterns
│   │   ├── 00-QUICK-REFERENCE.md  # 🔥 HOT PATH: 80% of patterns
│   │   ├── cqrs.md                # 🔥 Command/Query with MediatR
│   │   └── domain-modeling.md     # 🔥 DDD, Result<T>, StrongId<T>
│   │
│   ├── codebase/                  # Code organization
│   │   ├── source-tree.md         # Project structure
│   │   ├── project-conventions.md # File naming, namespaces
│   │   └── coding-standards.md    # C# conventions
│   │
│   └── workflows/                 # Developer processes
│       ├── getting-started.md     # Onboarding (clone, build, run)
│       ├── development-workflow.md # Daily dev cycle, migrations
│       ├── git-workflow.md        # Branch naming, commits, PRs
│       ├── debugging.md           # Troubleshooting
│       └── testing-workflow.md    # Running tests, coverage
│
├── modules/                       # Module-specific documentation
│   ├── identity/                  # Identity & Authentication
│   │   ├── 00-INDEX.md            # Module entry point
│   │   ├── 01-domain-model.md     # Domain design
│   │   └── ... (9 files total)
│   │
│   └── chat/                      # Chat & Messaging
│       ├── 00-INDEX.md            # Module entry point
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
| **Set up locally** | `guides/workflows/getting-started.md` |
| **Learn patterns** | `guides/patterns/00-QUICK-REFERENCE.md` 🔥 |
| **Create command** | `guides/patterns/cqrs.md` |
| **Model domain** | `guides/patterns/domain-modeling.md` |
| **Handle errors** | `guides/patterns/00-QUICK-REFERENCE.md` (Error section) |
| **Understand architecture** | `guides/architecture/system-overview.md` |
| **Module boundaries** | `guides/architecture/system-overview.md` (Module Map) |
| **Follow code style** | `guides/codebase/coding-standards.md` |
| **Git workflow** | `guides/workflows/git-workflow.md` |
| **Debug issues** | `guides/workflows/debugging.md` |

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
→ Explore [Identity Module](./modules/identity/00-INDEX.md)

**Building a Feature?**
→ Check [Quick Reference](./guides/patterns/00-QUICK-REFERENCE.md) 🔥
→ Review [Coding Standards](./guides/codebase/coding-standards.md)
→ Read relevant module docs

**Debugging an Issue?**
→ Check [Quick Reference](./guides/patterns/00-QUICK-REFERENCE.md)
→ Review infrastructure docs (`infrastructure/`)
→ See [Debugging Guide](./guides/workflows/debugging.md)

---

---

## 📊 Documentation Stats

**Total Files**: 22 guide files, 6,191 lines
**Coverage**:
- ✅ Patterns: Quick Reference (384), CQRS (328), Domain Modeling (316)
- ✅ Architecture: System Overview (224), Tech Stack (141), 6 ADRs (1,001)
- ✅ Workflows: 5 comprehensive guides (2,583 lines)
- ✅ Codebase: Source tree, conventions, standards (1,002 lines)

**AI Context Loading**: ~1,400 lines covers 90% of queries

---

**Last Updated**: 2025-09-29
**Maintained By**: Axon Engineering Team