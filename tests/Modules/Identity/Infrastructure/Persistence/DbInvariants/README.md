# Identity Module DB Invariant Tests

This directory contains comprehensive Database Invariant tests for the Identity Module, implementing the TDD approach as specified in `/Docs/BMAD/TDD.md` section A.

## Purpose

These tests validate that **database constraints alone** prevent identity violations, ensuring that critical business rules are enforced at the database level before the refactoring process begins.

## Test Design Philosophy (TDD)

These tests are designed to **initially fail** to expose gaps in the current implementation:

1. **Red Phase**: Tests fail, revealing missing constraints and features
2. **Green Phase**: Implementation is added/fixed to make tests pass
3. **Refactor Phase**: Code is cleaned up while maintaining test compliance

## Test Structure

### Files Created

- **`IdentityDbInvariantsTestBase.cs`** - Enhanced test base with PostgreSQL Testcontainers support
- **`TestDataFixtures.cs`** - Canonical test data from TDD document
- **`IdentityDbInvariantsTests.cs`** - Main test suite with 6 core DB invariant tests
- **`README.md`** - This documentation file

### Test Categories

#### 1. WALLET_UNIQUE_env_chain_address
- Validates unique wallet per environment/chain/address combination
- **Expected Failure**: Environment field not implemented yet
- Tests both same and different environment scenarios

#### 2. CREDENTIAL_UNIQUE_env_provider_issuer_subject
- Validates credential upsert behavior for same provider/issuer/subject
- **Expected Failure**: Environment not part of credential constraint yet
- Tests credential deduplication logic

#### 3. OWNERSHIP_PAIR_UNIQUE
- Validates unique (principal_id, wallet_id) pairs
- **Should Pass**: This constraint exists in current implementation
- Tests duplicate ownership prevention

#### 4. EXCLUSIVITY_PARTIAL_UNIQUE_verified_signing
- Validates only one verified+signing owner per wallet
- **Should Pass**: Partial unique index exists
- Tests concurrent verification scenarios

#### 5. DEFAULT_UNIQUE_per_principal_env_chain
- Validates one default wallet per principal per chain
- **Potential Failure**: Environment may not be part of constraint
- Tests default wallet uniqueness

#### 6. DEFAULT_GUARD_verified_signing_only
- Validates defaults can only be verified+signing wallets
- **Expected Failure**: Check constraint doesn't exist yet
- Tests business rule enforcement

## Key Features

### PostgreSQL Integration
- Uses **Testcontainers** for real PostgreSQL database testing
- Tests actual database constraints, not SQLite approximations
- Validates specific PostgreSQL error codes and constraint names

### Canonical Test Data
- Implements fixtures from TDD document:
  - Environments: `mainnet`, `devnet`
  - Chains: `solana:mainnet`, `solana:devnet`
  - Wallets: `W1_main`, `W1_dev`, `W2_main`
  - Principals: `P_A`, `P_B`

### Concurrency Testing
- Separate DbContext instances for race condition testing
- Validates exclusivity under parallel operations
- Tests auto-revocation behavior

### Constraint Validation
- PostgreSQL-specific error code validation (`23505`, `23514`)
- Exact constraint name verification
- Partial unique index testing

## Expected Test Failures

The following tests will initially fail (as designed):

1. **`Test_WALLET_UNIQUE_env_chain_address_*`** - Environment field missing
2. **`Test_CREDENTIAL_UNIQUE_env_provider_issuer_subject_*`** - Environment constraint missing
3. **`Test_DEFAULT_GUARD_verified_signing_only_*`** - Check constraint missing
4. **`Test_DEFAULT_UNIQUE_per_principal_env_chain_*`** - Environment in constraint missing

## Running the Tests

```bash
# Run all DB invariant tests
dotnet test --filter "TestClass=IdentityDbInvariantsTests"

# Run specific test category
dotnet test --filter "Test_WALLET_UNIQUE"

# List all available tests
dotnet test --filter "TestClass=IdentityDbInvariantsTests" --list-tests
```

## Implementation Gaps Exposed

These tests will guide the refactoring by highlighting:

1. **Missing Environment Field** in Wallet entity
2. **Missing Environment Constraints** in unique indexes
3. **Missing Check Constraints** for business rules
4. **Incomplete Exclusivity Logic** for concurrent operations

## Next Steps

1. **Run Tests** to confirm failures
2. **Implement Environment Field** in Wallet entity
3. **Add Database Constraints** as revealed by test failures
4. **Implement Check Constraints** for business rules
5. **Re-run Tests** to validate fixes
6. **Refactor** with confidence knowing constraints are enforced

## Architecture Compliance

These tests ensure the refactored Identity Module will maintain:
- **Data Integrity** through database constraints
- **Business Rule Enforcement** at the storage level
- **Concurrent Safety** through proper indexing
- **Environment Isolation** through enhanced constraints

The TDD approach guarantees that critical invariants are not lost during the refactoring process.