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

- [System Overview](./architecture/system-overview.md) - Architecture vision, C4 diagrams ⭐
- [Tech Stack](./architecture/tech-stack.md) - Libraries by layer with docs links
- [ADRs](./architecture/adrs/00-INDEX.md) - Architecture Decision Records ⭐

**When to use**: Understanding system structure, making architectural decisions

---

### 🎨 Patterns (Implementation)
Concrete implementation patterns with code examples.

- [**Quick Reference**](./patterns/00-QUICK-REFERENCE.md) 🔥 **HOT PATH** - 80% of patterns
- [CQRS](./patterns/cqrs.md) - Command/Query patterns with MediatR ⭐
- [Domain Modeling](./patterns/domain-modeling.md) - DDD, Result<T>, StrongId<T> ⭐

**When to use**: Implementing features, writing application code

---

### 💻 Codebase (Code Organization)
Code conventions, file structure, and C# standards.

- [Source Tree](./codebase/source-tree.md) - Project structure & navigation ⭐
- [Coding Standards](./codebase/coding-standards.md) - C# conventions, naming, style
- [Project Conventions](./codebase/project-conventions.md) - File naming, namespaces, project structure ⭐

**When to use**: Writing code, organizing files, code reviews

---

### 🔧 Workflows (Developer Processes)
Day-to-day developer workflows and tooling.

- [**Getting Started**](./workflows/getting-started.md) ⭐ - Clone, build, run, verify (NEW DEVELOPERS START HERE)
- [Development Workflow](./workflows/development-workflow.md) - Daily dev cycle, migrations, testing ⭐
- [Git Workflow](./workflows/git-workflow.md) - Branch naming, commits, PRs ⭐
- [Debugging Guide](./workflows/debugging.md) - Common issues, troubleshooting ⭐
- [Testing Workflow](./workflows/testing-workflow.md) - Running tests, coverage, debugging tests ⭐

**When to use**: Onboarding, daily development, troubleshooting

---

## 🗺️ Quick Navigation by Task

| Task | Guide |
|------|-------|
| **Understand system architecture** | [System Overview](./architecture/system-overview.md) |
| **Learn implementation patterns** | [Quick Reference](./patterns/00-QUICK-REFERENCE.md) 🔥 |
| **Write a command** | [CQRS](./patterns/cqrs.md) |
| **Model a domain** | [Domain Modeling](./patterns/domain-modeling.md) |
| **Handle errors** | [Quick Reference](./patterns/00-QUICK-REFERENCE.md) (Error section) |
| **Validate input** | [CQRS](./patterns/cqrs.md) (ValidationBehavior) |
| **Find a file** | [Source Tree](./codebase/source-tree.md) |
| **Follow code style** | [Coding Standards](./codebase/coding-standards.md) |
| **Follow naming conventions** | [Project Conventions](./codebase/project-conventions.md) |
| **Set up locally** | [Getting Started](./workflows/getting-started.md) |
| **Daily development** | [Development Workflow](./workflows/development-workflow.md) |
| **Create a PR** | [Git Workflow](./workflows/git-workflow.md) |
| **Debug an issue** | [Debugging Guide](./workflows/debugging.md) |
| **Run tests** | [Testing Workflow](./workflows/testing-workflow.md) |
| **Understand ADR** | [Architecture Decisions](./architecture/adrs/00-INDEX.md) |

---

## 🧭 Related Documentation

**Not in guides/**:
- **Infrastructure** → [../infrastructure/00-INDEX.md](../infrastructure/00-INDEX.md) - Technical implementation (persistence, caching, observability)
- **Modules** → [../modules/](../modules/) - Business domain documentation (Identity, Chat)
- **Testing** → [../testing/00-INDEX.md](../testing/00-INDEX.md) - Testing strategies & practices
- **API** → [../api/00-INDEX.md](../api/00-INDEX.md) - REST API conventions
- **Integrations** → [../integrations/00-INDEX.md](../integrations/00-INDEX.md) - External services (Dynamic, Helius)

---

## 🤖 For AI Agents

**Context Loading Strategy**:
1. Load `guides/patterns/00-QUICK-REFERENCE.md` first (80% of queries)
2. For architecture questions → `guides/architecture/system-overview.md`
3. For code questions → `guides/patterns/{specific-pattern}.md`
4. For setup questions → `guides/workflows/getting-started.md`

**Hot Paths**:
- `patterns/00-QUICK-REFERENCE.md` - Always load first (80% coverage)
- `patterns/cqrs.md` - Command/query implementation
- `patterns/domain-modeling.md` - Result<T>, StrongId<T>, aggregates
- `architecture/system-overview.md` - C4 diagrams, module boundaries

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

**Last Updated**: 2025-09-29
**Maintained By**: Axon Engineering Team

---

## 🆕 What's New (2025-09-29)

**Phase 2 Complete - Token-Efficient Documentation**:
- ✅ **Quick Reference**: 384 lines covering 80% of patterns
- ✅ **Domain Modeling**: 316 lines of DDD templates
- ✅ **CQRS**: 328 lines of MediatR patterns
- ✅ **System Overview**: 224 lines with C4 diagrams
- ✅ **Tech Stack**: 141 lines library reference with docs links

**Phase 1 Complete**:
- ✅ **Workflows**: 5 guides (2,583 lines)
- ✅ **ADRs**: 6 decision records (1,001 lines)
- ✅ **Codebase**: Source tree, conventions, standards (1,002 lines)

**Total**: 23 files, ~5,600 lines of AI-optimized documentation