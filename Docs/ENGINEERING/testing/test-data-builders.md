# Test Data Builders

**Object Mother and Builder patterns for test data creation.**

---

**STATUS**: 🚧 Scaffold - Needs Content
**PRIORITY**: Medium
**LAST_UPDATED**: 2025-01-29

---

## Content to be filled:

### Builder Pattern
- Fluent test data builders for aggregates
- Builder chaining and method naming conventions
- Default values vs explicit configuration
- Example: `UserBuilder`, `ConversationBuilder`, `MessageBuilder`

### Object Mother Pattern
- Pre-configured test data scenarios
- Common test fixtures (valid user, invalid user, etc.)
- Naming conventions for mothers
- Example: `TestUsers`, `TestConversations`

### Test Data Management
- Seed data for integration tests
- Test data cleanup strategies
- Randomized vs deterministic data
- Database fixtures with Testcontainers

### Examples
```csharp
// Builder pattern
var user = new UserBuilder()
    .WithEmail("test@example.com")
    .WithWallet("solana-address")
    .Build();

// Object Mother pattern
var validUser = TestUsers.ValidUser();
var userWithMultipleWallets = TestUsers.UserWithMultipleWallets();
```

### Best Practices
- One builder per aggregate root
- Builders return valid objects by default
- Explicit methods for invalid states
- Builders in test projects only
- Reusable across unit, integration, and API tests

### Anti-Patterns to Avoid
- Builders with too many methods
- Coupling builders to specific test scenarios
- Mutable builders (prefer immutable)
- Builders that do I/O or have side effects

---

**Related Documentation:**
- [Unit Testing Guide](./unit-testing-guide.md) - Using builders in unit tests
- [Integration Testing Guide](./integration-testing-guide.md) - Database fixtures
- [Identity Testing Guide](../modules/identity/09-testing-guide.md) - Identity-specific builders
- [Chat Testing Guide](../modules/chat/09-testing-guide.md) - Chat-specific builders