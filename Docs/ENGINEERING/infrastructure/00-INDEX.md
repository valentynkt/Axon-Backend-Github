# Infrastructure Documentation

**Cross-cutting technical concerns: Persistence, caching, security, observability, and resilience.**

---

## 📚 Infrastructure Guides

### Core Infrastructure
- **[Persistence Guide](./PERSISTENCE.md)** ⭐ - **START HERE**: EF Core + PostgreSQL
  - DbContext architecture (Read/Write separation)
  - Owned entity pattern (EF Core OwnsMany)
  - Optimistic concurrency with PostgreSQL `xmin`
  - Repository pattern implementation
  - Migrations workflow
  - Query optimization (indexes, N+1 prevention, projections)
  - Best practices & anti-patterns

- **[Caching Guide](./CACHING.md)** - Multi-tier caching strategy
  - L1: IMemoryCache (in-process)
  - L2: IDistributedCache (optional Redis)
  - Query caching behavior (MediatR pipeline)
  - Cache invalidation (event-driven)
  - CurrentUserService hierarchy

### Security, Observability, Resilience
> **STATUS**: 🚧 To be created based on actual implementation
>
> **Next Steps**: Document JWT authentication, OpenTelemetry setup, and Polly patterns based on codebase usage

---

## 🎯 Quick Reference

| Need | Guide | Section |
|------|-------|---------|
| **Set up DbContext** | [Persistence](./PERSISTENCE.md) | DbContext Architecture |
| **Configure owned entities** | [Persistence](./PERSISTENCE.md) | Owned Entity Pattern |
| **Handle concurrency** | [Persistence](./PERSISTENCE.md) | Concurrency Control |
| **Create migrations** | [Persistence](./PERSISTENCE.md) | Migrations |
| **Cache queries** | [Caching](./CACHING.md) | Cacheable Queries |
| **Invalidate cache** | [Caching](./CACHING.md) | Cache Invalidation |

---

## 🏗️ Infrastructure Layers

```
BuildingBlocks/
├── Core/                    # Abstractions, primitives
├── Application/             # CQRS behaviors (caching, validation)
└── Infrastructure/          # EF Core, repositories, auth

Modules/{Module}/Infrastructure/
├── Persistence/             # DbContext, repositories, configurations
├── ExternalServices/        # API clients, integrations
└── DependencyInjection/     # Service registration
```

---

## 🔧 Key Technologies

- **Database**: PostgreSQL 16 + EF Core 9
- **Caching**: IMemoryCache (+ optional Redis via IDistributedCache)
- **Authentication**: JWT Bearer + Dynamic.xyz integration
- **Observability**: OpenTelemetry + Serilog
- **Resilience**: Polly (retry, circuit breaker, timeout)

---

## 📊 Cross-Cutting Concerns

### Persistence
- **Pattern**: CQRS with Read/Write DbContexts
- **Concurrency**: Optimistic locking (PostgreSQL xmin)
- **Aggregates**: Owned entities via EF Core OwnsMany

### Caching
- **Strategy**: Multi-tier (memory → distributed)
- **Scope**: Query results only (not commands)
- **Invalidation**: Event-driven via domain events

### Security
- **Authentication**: JWT Bearer tokens
- **Authorization**: Policy-based
- **Secrets**: User Secrets (dev) + Environment Variables (prod)

### Observability
- **Logging**: Serilog with structured logging
- **Metrics**: OpenTelemetry
- **Tracing**: Distributed tracing with Activity API
- **Health**: Health check endpoints

### Resilience
- **Retry**: Exponential backoff with Polly
- **Circuit Breaker**: Fail-fast for external services
- **Timeout**: Cancellation token propagation

---

## 🧪 Testing Infrastructure

See [Testing Guide](../testing/TESTING-GUIDE.md) for comprehensive testing patterns.

**Key Tests**:
- **Persistence**: DbContext configurations, concurrency, owned entities
- **Caching**: Cache hit/miss, invalidation, TTL expiration
- **Integration**: Cross-layer flows with Testcontainers

**Specialized Guide**:
- [Concurrency Testing](../testing/concurrency-testing-guide.md) - EF Core concurrency patterns

---

## 🔗 Related Documentation

### Guides
- [Architecture Overview](../guides/architecture/system-overview.md) - System design
- [CQRS Pattern](../guides/patterns/cqrs.md) - Command/Query separation
- [Domain Modeling](../guides/patterns/domain-modeling.md) - Aggregate design

### Modules
- [Identity Module](../modules/identity/00-INDEX.md) - Authentication & authorization
- [Chat Module](../modules/chat/00-INDEX.md) - Messaging & AI integration

### Libraries
- [MediatR](../../Libraries/MediatR/IMPLEMENTATION_GUIDE.md) - CQRS mediator
- [Polly](../../Libraries/Polly/IMPLEMENTATION_GUIDE.md) - Resilience patterns
- [OpenTelemetry](../../Libraries/OpenTelemetry/IMPLEMENTATION_GUIDE.md) - Observability

---

## 📝 Contributing

**Adding New Infrastructure**:
1. Implement in `BuildingBlocks/Infrastructure/` or module-specific infrastructure
2. Document patterns in relevant guide (extend or create new)
3. Add examples from actual codebase
4. Update this index with new capabilities

**Documentation Standard**:
- Show real code from `src/` directory
- Include practical examples
- Explain WHY (rationale) not just HOW
- Link to related patterns/guides

---

**Last Updated**: 2025-09-30
**Maintained By**: Axon Engineering Team