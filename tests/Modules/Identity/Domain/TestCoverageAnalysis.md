# Identity Domain Test Coverage Analysis

## Current Coverage Status

### ✅ Tested Rules (5/12 = 42%)
1. **ValidRiskTierRule** - 8 tests
2. **MaxWalletsPerPrincipalRule** - 7 tests  
3. **TimestampMonotonicityRule** - 8 tests
4. **TagMustBeAllowedRule** - 21 tests
5. **PrincipalMustHaveTypeRule** - 4 tests

**Total: 48 tests passing**

### ❌ Untested Rules (7/12 = 58%)
1. **CredentialMustBeUniqueRule** - Requires IdentityCredential mocking
2. **PrincipalCanBeDeletedRule** - Requires WalletOwnership/IdentityCredential mocking
3. **PrincipalMustBeActiveRule** - Requires AxonPrincipal mocking
4. **WalletMustBeActiveRule** - Requires Wallet mocking
5. **WalletMustBeOwnedByPrincipalRule** - Requires WalletOwnership mocking
6. **WalletMustNotBeOwnedByPrincipalRule** - Requires WalletOwnership mocking
7. **WalletMustNotExistRule** - Has static initialization issue with ChainId

### ❌ Untested Domain Components
- **Aggregates**: AxonPrincipal, Wallet (0% coverage)
- **Entities**: IdentityCredential, WalletOwnership, PrincipalProfile, etc. (0% coverage)
- **Value Objects**: Complex ones beyond simple validation (partial coverage)
- **Domain Events**: 0% coverage
- **Domain Services**: 0% coverage

## Coverage by Category

| Category | Coverage | Tests | Notes |
|----------|----------|-------|-------|
| Business Rules | 42% | 48 | 5 of 12 rules tested |
| Aggregates | 0% | 0 | Requires factory pattern or test builders |
| Entities | 0% | 0 | Private constructors prevent mocking |
| Value Objects | ~10% | Some via rules | Only indirect testing |
| Domain Events | 0% | 0 | Not tested |
| Integration | 0% | 0 | No aggregate interaction tests |

## Business Value Assessment

### High Value (Top 20% that covers 80% of risk)
1. **Aggregate Invariants** - Critical business logic lives here
2. **Complex Business Rules** - Already partially covered
3. **State Transitions** - Delete/Restore/Verify operations
4. **Domain Event Generation** - Ensures proper event sourcing

### Medium Value (Next 30%)
1. **Entity Relationships** - Ownership, credentials
2. **Value Object Validation** - Input sanitization
3. **Query Methods** - Read operations

### Low Value (Bottom 50%)
1. **Simple getters/setters**
2. **Generated code (Vogen)**
3. **Infrastructure concerns**