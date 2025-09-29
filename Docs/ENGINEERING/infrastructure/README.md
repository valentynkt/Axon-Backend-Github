# Infrastructure Documentation

**Cross-cutting technical concerns and infrastructure patterns.**

---

**STATUS**: 🚧 Scaffold - Needs Content
**PRIORITY**: Medium
**LAST_UPDATED**: 2025-01-29

---

## 📚 Infrastructure Documentation

### Persistence
- [EF Core Configuration](./persistence/ef-core-configuration.md) - DbContext, conventions, interceptors
- [Repository Pattern](./persistence/repository-pattern.md) - Base repository, specifications
- [Migrations](./persistence/migrations.md) - Migration workflow, seeding, rollback
- [Concurrency Handling](./persistence/concurrency-handling.md) - Optimistic concurrency, row versioning
- [Query Optimization](./persistence/query-optimization.md) - Indexes, N+1 prevention, projections

### Caching
- [Caching Strategy](./caching/strategy.md) - Multi-tier architecture, TTL policies
- [Memory Cache](./caching/memory-cache.md) - IMemoryCache setup, eviction
- [Distributed Cache](./caching/distributed-cache.md) - Redis setup, serialization
- [Cache Invalidation](./caching/invalidation.md) - Event-driven, tag-based invalidation

### Security
- [Authentication Setup](./security/authentication-setup.md) - JWT config, multi-scheme
- [Authorization Policies](./security/authorization-policies.md) - Policy registration, handlers
- [Secrets Management](./security/secrets-management.md) - User secrets, Key Vault
- [Security Headers](./security/security-headers.md) - CORS, HSTS, CSP

### Observability
- [Structured Logging](./observability/structured-logging.md) - Serilog enrichment, sinks
- [OpenTelemetry Metrics](./observability/opentelemetry-metrics.md) - Meter setup, custom metrics
- [Distributed Tracing](./observability/distributed-tracing.md) - Activity source, span enrichment
- [Health Checks](./observability/health-checks.md) - Health check registration, custom checks

### Resilience
- [Resilience Patterns](./resilience/README.md) - Overview of fault tolerance strategies
- [Retry Policies](./resilience/retry-policies.md) - Polly retry, exponential backoff
- [Circuit Breakers](./resilience/circuit-breakers.md) - Break conditions, fallback
- [Timeout Handling](./resilience/timeout-handling.md) - HTTP/DB timeouts, CancellationToken

---

## Content to be filled:
- Overview of infrastructure packages structure
- Cross-cutting concern patterns
- When to use each infrastructure capability