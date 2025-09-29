# Axon Backend - Agent-Delegated Development

**Token-efficient Claude.MD that delegates all work to specialized Axon agents for optimal development workflow.**

## 🚀 MANDATORY: Agent-First Protocol

**ALL work must be delegated to specialized agents:**

| Task Type | Delegate To | When |
|-----------|-------------|-------|
| Stories/Epics | `@axon-story-orchestrator` | Any story management |
| Research/Architecture | `@axon-research-architect` | Before implementation |
| Implementation | `@axon-implementation-specialist` | After research approval |
| Quality/Testing | `@axon-quality-guardian` | Implementation complete |

### Primary Workflow
```bash
# Standard story workflow - delegate immediately  
@axon-story-orchestrator implement-story {story-id}
@axon-research-architect research-solutions {requirement}
@axon-implementation-specialist implement-feature {feature}
@axon-quality-guardian validate-requirements {story-id}
```

## 📚 Documentation Index

### Primary Documentation Locations
```
Docs/
├── README.md                      # Master navigation (three domains)
├── PRODUCT/                       # Business & strategy
│   └── 00-INDEX.md               # Product documentation hub
├── ENGINEERING/                   # Technical implementation
│   ├── 00-START-HERE.md          # Engineering entry point ⭐
│   ├── guides/                   # Architecture, patterns, codebase, workflows
│   │   ├── 00-INDEX.md          # Guides hub
│   │   ├── patterns/00-QUICK-REFERENCE.md  # 🔥 HOT PATH
│   │   ├── architecture/        # System design & ADRs
│   │   └── codebase/            # Source tree & coding standards
│   ├── modules/                 # Module-specific docs
│   │   ├── identity/00-INDEX.md # Identity module hub
│   │   └── chat/00-INDEX.md     # Chat module hub
│   ├── infrastructure/00-INDEX.md  # Persistence, caching, security
│   ├── testing/00-INDEX.md      # Testing strategies
│   ├── api/00-INDEX.md          # REST conventions
│   └── integrations/00-INDEX.md # External services
├── PROCESS/                      # Workflow & project management
│   └── 00-INDEX.md              # Process documentation hub
└── Libraries/                    # Library implementation guides
    ├── 00-INDEX.md              # Library navigation hub
    ├── FastEndpoints/           # API endpoint patterns
    ├── FluentValidation/        # Request validation
    ├── MediatR/                 # CQRS implementation
    └── [15+ more]/              # Full library documentation
```

### Quick Reference
- **Start Here**: `Docs/ENGINEERING/00-START-HERE.md` ⭐
- **Hot Path**: `Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md` 🔥
- **Coding Standards**: `Docs/ENGINEERING/guides/codebase/coding-standards.md`
- **Tech Stack**: `Docs/ENGINEERING/guides/architecture/tech-stack.md`
- **Library Guides**: `Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md`
- **Story Template**: `Docs/PROCESS/story-template.md`

## 🔧 Essential Commands

```bash
# Development workflow
dotnet build                           # Build with warnings as errors
dotnet run --project src/Api          # Run API locally
dotnet test                           # Run all tests
dotnet build --configuration Release  # Release build with analyzers
```

## 🏗️ Architecture Summary

**Modular Monolith**: Clean Architecture + DDD + CQRS + .NET 10

### Core Patterns
- **Clean Architecture**: Domain → Application → Infrastructure → API
- **CQRS**: MediatR commands/queries  
- **Result Pattern**: `Result<T, Error>` error handling
- **Strong IDs**: Type-safe `StrongId<T>`
- **Modern C#**: File-scoped namespaces, records, nullable enabled

### Key Technologies
- **.NET 10 Preview** + **FastEndpoints** + **MediatR** + **FluentValidation**
- **EF Core 9** + **PostgreSQL** + **OpenTelemetry**
- **NUnit** + **Shouldly** + **Testcontainers**

## 💡 Development Standards (Quick Reference)

### Modern C# (MANDATORY)
```csharp
// File-scoped namespaces
namespace Axon.Modules.Chat;

// Records for DTOs  
public record UserRequest(string Name, string Email);

// Target-typed new
List<string> items = new();

// Result pattern
public Result<User, Error> CreateUser(string email) => 
    string.IsNullOrEmpty(email) 
        ? Result<User>.Failure(Error.Validation("Email required"))
        : Result<User>.Success(new User(email));
```

### Quality Gates
- **90%+ test coverage** + **Architecture compliance** + **No warnings**

## ⚡ Agent Delegation Rules

**DO NOT implement directly - Always delegate:**

```yaml
Delegation_Matrix:
  Research_Questions: "@axon-research-architect research-solutions {topic}"
  Story_Implementation: "@axon-story-orchestrator implement-story {id}"
  Code_Changes: "@axon-implementation-specialist implement-feature {name}"
  Quality_Validation: "@axon-quality-guardian validate-requirements {story}"
  
Quick_Tasks_Only:
  - File reading/analysis
  - Simple questions about existing code
  - Command execution for builds/tests
```

### Preferred Libraries (Research-Validated)
- **Auth**: Microsoft.AspNetCore.Identity + JWT Bearer
- **Validation**: FluentValidation (already integrated)
- **Caching**: Microsoft.Extensions.Caching.Memory/Redis
- **HTTP**: HttpClientFactory + Refit
- **Testing**: NUnit + Shouldly + Testcontainers + NSubstitute

### Critical Files
- `Directory.Build.props` - Build configuration
- `src/BuildingBlocks/Core/` - Core patterns (Result, StrongId)
- `src/Api/Program.cs` - Startup configuration

---
**⚠️ DELEGATION MANDATE: All non-trivial work must be delegated to specialized Axon agents. This Claude.MD serves as a routing table, not an implementation guide.**