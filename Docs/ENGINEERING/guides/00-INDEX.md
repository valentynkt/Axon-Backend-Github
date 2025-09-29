# 📘 Developer Guides

**Complete guide to building Axon: architecture, patterns, code conventions, and workflows.**

---

## 🎯 Quick Start

**New to Axon?** Start here:
1. [System Overview](./architecture/system-overview.md) - Understand the big picture
2. [Quick Reference](./patterns/00-QUICK-REFERENCE.md) 🔥 - Common patterns cheatsheet
3. [Getting Started](./workflows/getting-started.md) - Set up your environment
4. [Coding Standards](./codebase/coding-standards.md) - Write consistent code

---

## 📚 Guide Categories

### 🏛️ Architecture (System Design)
High-level system design, architectural styles, and technology decisions.

- [System Overview](./architecture/system-overview.md) - Architecture vision, C4 diagrams
- [Modular Monolith](./architecture/modular-monolith.md) - Module boundaries & communication
- [Clean Architecture](./architecture/clean-architecture.md) - Layer structure & dependency rules
- [Tech Stack](./architecture/tech-stack.md) - Technology choices & rationale
- [ADRs](./architecture/adrs/README.md) - Architecture Decision Records

**When to use**: Understanding system structure, making architectural decisions

---

### 🎨 Patterns (Implementation)
Concrete implementation patterns with code examples.

- [**Quick Reference**](./patterns/00-QUICK-REFERENCE.md) 🔥 **HOT PATH** - Pattern cheatsheet
- [CQRS](./patterns/cqrs.md) - Command/Query patterns with MediatR
- [Domain Modeling](./patterns/domain-modeling.md) - DDD, aggregates, Result<T>, StrongId<T>
- [Error Handling](./patterns/error-handling.md) - Error types, Result pattern, HTTP mapping
- [Validation](./patterns/validation.md) - FluentValidation patterns

**When to use**: Implementing features, writing application code

---

### 💻 Codebase (Code Organization)
Code conventions, file structure, and C# standards.

- [Source Tree](./codebase/source-tree.md) - Project structure & navigation
- [Coding Standards](./codebase/coding-standards.md) - C# conventions, naming, style

**When to use**: Writing code, organizing files, code reviews

---

### 🔧 Workflows (Developer Processes)
Day-to-day developer workflows and tooling.

- [Getting Started](./workflows/getting-started.md) - Clone, build, run, verify
- [Git Workflow](./workflows/git-workflow.md) - Branch naming, commits, PRs
- [Debugging Guide](./workflows/debugging-guide.md) - Common issues, debugging tools
- [IDE Setup](./workflows/ide-setup/) - JetBrains Rider, VS Code configuration

**When to use**: Onboarding, daily development, troubleshooting

---

## 🗺️ Quick Navigation by Task

| Task | Guide |
|------|-------|
| **Understand system architecture** | [System Overview](./architecture/system-overview.md) |
| **Learn implementation patterns** | [Quick Reference](./patterns/00-QUICK-REFERENCE.md) 🔥 |
| **Write a command** | [CQRS](./patterns/cqrs.md) |
| **Model a domain** | [Domain Modeling](./patterns/domain-modeling.md) |
| **Handle errors** | [Error Handling](./patterns/error-handling.md) |
| **Validate input** | [Validation](./patterns/validation.md) |
| **Find a file** | [Source Tree](./codebase/source-tree.md) |
| **Follow code style** | [Coding Standards](./codebase/coding-standards.md) |
| **Set up locally** | [Getting Started](./workflows/getting-started.md) |
| **Create a PR** | [Git Workflow](./workflows/git-workflow.md) |
| **Debug an issue** | [Debugging Guide](./workflows/debugging-guide.md) |

---

## 🧭 Related Documentation

**Not in guides/**:
- **Infrastructure** → [../infrastructure/README.md](../infrastructure/README.md) - Technical implementation (persistence, caching, observability)
- **Modules** → [../modules/](../modules/) - Business domain documentation (Identity, Chat)
- **Testing** → [../testing/README.md](../testing/README.md) - Testing strategies & practices
- **Deployment** → [../deployment/README.md](../deployment/README.md) - Operations & environments
- **API** → [../api/README.md](../api/README.md) - REST API conventions
- **Integrations** → [../integrations/README.md](../integrations/README.md) - External services (Dynamic, Helius)

---

## 🤖 For AI Agents

**Context Loading Strategy**:
1. Load `guides/patterns/00-QUICK-REFERENCE.md` first (80% of queries)
2. For architecture questions → `guides/architecture/system-overview.md`
3. For code questions → `guides/patterns/{specific-pattern}.md`
4. For setup questions → `guides/workflows/getting-started.md`

**Hot Paths**:
- `patterns/00-QUICK-REFERENCE.md` - Always load first
- `patterns/cqrs.md` - Command/query implementation
- `patterns/domain-modeling.md` - Result<T>, StrongId<T>, aggregates
- `patterns/error-handling.md` - Error pattern usage

---

## 📏 Documentation Philosophy

**guides/** contains:
- ✅ How to design systems (architecture)
- ✅ How to implement features (patterns)
- ✅ How to write code (codebase conventions)
- ✅ How to work daily (workflows)

**guides/** does NOT contain:
- ❌ Module-specific docs → see `modules/`
- ❌ Infrastructure implementation → see `infrastructure/`
- ❌ Deployment procedures → see `deployment/`

---

**Last Updated**: 2025-01-29
**Maintained By**: Axon Engineering Team