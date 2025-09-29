# Library Implementation Guides

**Complete documentation for all third-party libraries used in Axon Backend.**

---

## 🎯 Purpose

This directory contains research notes, implementation guides, and best practices for every major library dependency in the Axon project. Each library folder provides:

- **IMPLEMENTATION_GUIDE.md** - How to use the library in Axon context
- **RESEARCH_NOTES.md** - Why we chose this library, alternatives considered
- **Examples** - Code snippets and usage patterns specific to our architecture

---

## 📚 Library Categories

### Core Architecture
- **[MediatR](./MediatR/)** - CQRS command/query handling
- **[FastEndpoints](./FastEndpoints/)** - Minimal API endpoints without controllers
- **[FluentValidation](./FluentValidation/)** - Request and command validation

### Testing
- **[NUnit](./NUnit/)** - Test framework for unit and integration tests
- **[Shouldly](./Shouldly/)** - Fluent assertion library
- **[Moq](./Moq/)** - Mocking framework for unit tests
- **[NET10_Compatibility](./NET10_Compatibility/)** - .NET 10 testing compatibility notes

### Observability
- **[OpenTelemetry](./OpenTelemetry/)** - Distributed tracing, metrics, and logging
- **[SystemDiagnosticsActivity](./SystemDiagnosticsActivity/)** - Activity source for tracing

### Resilience & Performance
- **[Polly](./Polly/)** - Resilience patterns (retry, circuit breaker, timeout)

### External Integrations
- **[dynamic_auth](./dynamic_auth/)** - Dynamic.xyz authentication integration
- **[Helius](./Helius/)** - Helius Solana RPC and webhook services
- **[OpenAI](./OpenAI/)** - OpenAI API integration (Chat module)

### Model Context Protocol (MCP)
- **[ModelContextProtocol](./ModelContextProtocol/)** - Core MCP abstractions
- **[ModelContextProtocolCore](./ModelContextProtocol/ModelContextProtocolCore/)** - MCP core library
- **[ModelContextProtocolAspNetCore](./ModelContextProtocolAspNetCore/)** - ASP.NET Core integration

---

## 📖 Library Quick Reference

| Library | Purpose | When to Use |
|---------|---------|-------------|
| **MediatR** | CQRS mediator | Every command/query handler |
| **FastEndpoints** | HTTP endpoints | All API endpoints |
| **FluentValidation** | Input validation | Request/command validation |
| **NUnit** | Testing framework | All tests |
| **Shouldly** | Assertions | All test assertions |
| **Moq** | Mocking | Unit tests with dependencies |
| **OpenTelemetry** | Observability | Distributed tracing, metrics |
| **Polly** | Resilience | External API calls, retries |
| **dynamic_auth** | Authentication | Wallet-based auth with Dynamic.xyz |
| **Helius** | Solana RPC | Blockchain interactions |
| **OpenAI** | AI Integration | Chat AI processing |
| **MCP** | Model Context | AI model context management |

---

## 🔍 How to Use This Directory

### For Developers
1. **New to a library?** → Read `{LibraryName}/IMPLEMENTATION_GUIDE.md`
2. **Need examples?** → Check implementation guide code samples
3. **Why this library?** → Read `{LibraryName}/RESEARCH_NOTES.md`
4. **Library-specific patterns?** → Each guide shows Axon-specific usage

### For AI Agents
When implementing features:
1. Identify required library (e.g., MediatR for commands)
2. Load `Libraries/{LibraryName}/IMPLEMENTATION_GUIDE.md`
3. Follow patterns from guide
4. Reference `ENGINEERING/guides/patterns/` for architectural patterns
5. Combine library usage with Axon domain patterns (Result<T>, StrongId<T>, etc.)

---

## 📝 Adding New Library Documentation

When adding a new library dependency:

1. **Create library folder:** `Libraries/{LibraryName}/`
2. **Create IMPLEMENTATION_GUIDE.md:**
   - Installation instructions
   - Configuration in Axon context
   - Usage examples
   - Best practices
   - Common patterns
3. **Create RESEARCH_NOTES.md (optional):**
   - Why we chose this library
   - Alternatives considered
   - Pros/cons analysis
   - Version compatibility notes

**Template:**
```markdown
# {Library Name} - Implementation Guide

## Installation
[NuGet package, version, installation steps]

## Configuration
[Axon-specific setup in Program.cs, DI registration]

## Usage Patterns
[Code examples in Axon context]

## Best Practices
[Dos and don'ts]

## Related Documentation
[Links to ENGINEERING/ docs]
```

---

## 🔗 Related Documentation

- **Architecture Patterns** → [ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md](../ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md)
- **Coding Standards** → [ENGINEERING/guides/codebase/coding-standards.md](../ENGINEERING/guides/codebase/coding-standards.md)
- **Module Documentation** → [ENGINEERING/modules/](../ENGINEERING/modules/)
- **Infrastructure Guides** → [ENGINEERING/infrastructure/](../ENGINEERING/infrastructure/)

---

## 🤖 For AI Agents: Library Context Loading

**Standard Workflow:**
1. User asks to implement feature → Identify required libraries
2. Load relevant library guides from this directory
3. Load architectural patterns from `ENGINEERING/guides/patterns/`
4. Combine library usage with Axon patterns
5. Follow coding standards from `ENGINEERING/guides/codebase/`

**Example - Implementing a Command:**
1. Load `Libraries/MediatR/IMPLEMENTATION_GUIDE.md` (command pattern)
2. Load `ENGINEERING/guides/patterns/cqrs.md` (CQRS architecture)
3. Load `ENGINEERING/guides/patterns/domain-modeling.md` (Result<T> pattern)
4. Load `Libraries/FluentValidation/IMPLEMENTATION_GUIDE.md` (validation)
5. Implement command following all patterns

---

**Last Updated:** 2025-01-29
**Maintained By:** Axon Engineering Team
**Total Libraries Documented:** 15+