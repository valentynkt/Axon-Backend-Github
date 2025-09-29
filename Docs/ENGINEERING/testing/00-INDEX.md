# Testing Documentation

**Test philosophy, practices, and specialized guides.**

---

## 📚 Testing Guides

### Core Testing Guide
- **[Testing Guide](./TESTING-GUIDE.md)** ⭐ - **START HERE**: Comprehensive testing practices
  - Test organization & naming conventions
  - AAA pattern (Arrange-Act-Assert)
  - Test data patterns (Fixtures & Builders)
  - Testing by layer (Domain, Application, Infrastructure)
  - Result<T, Error> testing
  - Mocking with NSubstitute
  - Testcontainers for database tests
  - Best practices & anti-patterns

### Specialized Guides
- **[Concurrency Testing Guide](./concurrency-testing-guide.md)** - Optimistic concurrency testing
  - EF Core identity map pitfalls
  - ConcurrencyTestBase pattern
  - Separate DbContext instances
  - Concurrent update scenarios
  - Repository concurrency handling

---

## 🎯 Quick Reference

| Task | Guide |
|------|-------|
| **Learn testing basics** | [Testing Guide](./TESTING-GUIDE.md) - AAA, fixtures, builders |
| **Create test data** | [Testing Guide](./TESTING-GUIDE.md) - Object Mother & Builders |
| **Test concurrency** | [Concurrency Testing Guide](./concurrency-testing-guide.md) |
| **Test domain logic** | [Testing Guide](./TESTING-GUIDE.md) - Domain Layer Tests |
| **Test commands/queries** | [Testing Guide](./TESTING-GUIDE.md) - Application Layer Tests |
| **Test repositories** | [Testing Guide](./TESTING-GUIDE.md) - Infrastructure Layer Tests |

---

## 📊 Test Philosophy

### Test Pyramid
```
      E2E (10%)
    ─────────────
   Integration (40%)
  ─────────────────────
 Domain/Unit (50%)
```

**Focus**: Domain business logic > Integration > E2E critical flows

### Coverage Goals
- Domain Layer: 95%+
- Application Layer: 90%+
- Infrastructure Layer: 80%+
- Overall: 90%+

### Test Categories
- **Domain**: Pure business logic, invariants, state transitions
- **Application**: Command/query handlers, orchestration
- **Infrastructure**: EF Core, repositories, concurrency, database invariants
- **Integration**: Cross-layer flows, external service integration
- **E2E**: Critical user journeys (auth, messaging)

---

## 🔧 Tooling

- **Test Framework**: NUnit
- **Assertions**: Shouldly (fluent assertions)
- **Mocking**: NSubstitute
- **Database Testing**: Testcontainers (PostgreSQL)
- **Coverage**: dotnet test with XPlat Code Coverage

---

## 🏗️ Test Organization

```
tests/
├── BuildingBlocks/
│   ├── Core/              # Core pattern tests (Result<T>, StrongId<T>)
│   ├── Application/       # CQRS behavior tests (MediatR pipelines)
│   └── Infrastructure/    # Repository, EF Core base tests
└── Modules/
    ├── Identity/
    │   ├── Domain/        # Aggregate invariants (AxonPrincipal)
    │   ├── Application/   # Command/query handlers
    │   │   └── _TestInfrastructure/  # Fixtures, builders
    │   └── Infrastructure/ # Persistence, concurrency tests
    └── Chat/
        ├── Domain/        # Conversation aggregate tests
        ├── Application/   # Messaging flow tests
        └── Infrastructure/ # Database tests
```

---

## 🚀 Running Tests

```bash
# All tests
dotnet test

# Specific module
dotnet test --filter "FullyQualifiedName~Identity"

# Specific category
dotnet test --filter "Category=Concurrency"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## 📖 Module-Specific Testing

- **[Identity Testing Strategy](../modules/identity/testing-strategy.md)** - TDD plan for Identity module
  - DB invariants & constraints
  - Resolution algorithm determinism
  - Concurrency & exclusivity
  - Token & proof validation

---

## 🔗 Related Documentation

- **Libraries**:
  - [NUnit Implementation Guide](../../Libraries/NUnit/IMPLEMENTATION_GUIDE.md) - Test framework
  - [Shouldly Usage Guide](../../Libraries/Shouldly/USAGE_GUIDE.md) - Assertions
  - [Moq Implementation Guide](../../Libraries/Moq/IMPLEMENTATION_GUIDE.md) - Mocking (deprecated, use NSubstitute)

- **Workflows**:
  - [Testing Workflow](../guides/workflows/testing-workflow.md) - CI/CD integration
  - [Development Workflow](../guides/workflows/development-workflow.md) - TDD in dev cycle

---

**Last Updated**: 2025-09-30
**Maintained By**: Axon Engineering Team