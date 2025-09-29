# Resilience Patterns

**Fault tolerance, retry policies, and graceful degradation.**

---

**STATUS**: 🚧 Scaffold - Needs Content
**PRIORITY**: Medium
**LAST_UPDATED**: 2025-01-29

---

## 📚 Resilience Documentation

### Retry Strategies
- [Retry Policies](./retry-policies.md) - Polly retry configuration, exponential backoff, jitter

### Circuit Breakers
- [Circuit Breakers](./circuit-breakers.md) - Break conditions, fallback strategies, state management

### Timeout Management
- [Timeout Handling](./timeout-handling.md) - HTTP/DB timeouts, CancellationToken propagation

---

## 🎯 Quick Navigation by Concern

| Concern | Document |
|---------|----------|
| **External API calls failing** | [Retry Policies](./retry-policies.md) |
| **Cascading failures** | [Circuit Breakers](./circuit-breakers.md) |
| **Long-running operations** | [Timeout Handling](./timeout-handling.md) |

---

## Content to be filled:
- Overview of resilience strategy
- When to use each pattern
- Common failure scenarios and solutions
- Monitoring and observability integration
- Best practices for resilient microservices