# 📋 Prioritized Test Plan - Identity Domain (80/20 Focus)

## 🎯 Priority 1: Critical Path Tests (20% effort, 80% value)

### 1.1 Aggregate Factory Methods & Invariants
**Why Critical**: These are the entry points to the domain - if creation fails, nothing works.

```csharp
// Tests needed for AxonPrincipal
- Create_WithValidCredential_ShouldSucceed
- Create_WithInvalidCredential_ShouldFail
- Create_EnforcesBusinessRules
- Create_RaisesCorrectDomainEvents

// Tests needed for Wallet
- Register_WithValidAddress_ShouldSucceed
- Register_WithDuplicateAddress_ShouldFail
- Register_RaisesWalletRegisteredEvent
```

**Implementation Strategy**: Use reflection or internal test assemblies to access private constructors.

### 1.2 Critical State Transitions
**Why Critical**: Core business operations that affect system state.

```csharp
// AxonPrincipal State Changes
- SoftDelete_WhenActive_ShouldMarkDeleted
- Restore_WhenDeleted_ShouldReactivate
- LinkCredential_WhenValid_ShouldAddToCollection
- RevokeCredential_WhenExists_ShouldMarkDeleted

// Wallet Ownership
- LinkWallet_WhenUnderLimit_ShouldSucceed
- LinkWallet_WhenAtLimit_ShouldFail
- VerifyOwnership_ChangesStateToVerified
- UnlinkWallet_RemovesOwnership
```

**Implementation Strategy**: Create test-specific factory methods that use internal constructors.

### 1.3 Integration Between Aggregates
**Why Critical**: Most bugs occur at boundaries between aggregates.

```csharp
// Principal-Wallet Interactions
- Principal_CanLinkMultipleWallets_UpToLimit
- Principal_CannotLinkSameWalletTwice
- Wallet_CanHaveMultipleOwners
- DeletedPrincipal_CannotLinkNewWallets
```

## 🔄 Priority 2: Domain Event Tests (15% effort, 15% value)

### 2.1 Event Generation
```csharp
// Verify correct events are raised
- PrincipalCreated_ContainsCorrectData
- WalletLinked_ContainsWalletAndPrincipalIds
- CredentialRevoked_ContainsRevokedCredentialId
```

### 2.2 Event Ordering
```csharp
// Ensure events maintain correct sequence
- MultipleOperations_GenerateEventsInCorrectOrder
- ConcurrentOperations_MaintainEventConsistency
```

## 📊 Priority 3: Complex Query Tests (5% effort, 5% value)

### 3.1 Aggregate Queries
```csharp
// Read operations that involve business logic
- GetActiveCredentials_ExcludesDeleted
- GetVerifiedWallets_OnlyReturnsVerified
- HasActiveWallets_CorrectlyCalculates
```

## 🚀 Implementation Approach

### Week 1: Foundation (Priority 1.1)
1. **Create TestableAggregateFactory** helper class
2. **Implement AxonPrincipal creation tests** (10 tests)
3. **Implement Wallet registration tests** (8 tests)

### Week 2: State Management (Priority 1.2)
1. **Test all state transitions** (15 tests)
2. **Test business rule enforcement** during transitions
3. **Verify domain events** are raised

### Week 3: Integration (Priority 1.3)
1. **Test aggregate interactions** (10 tests)
2. **Test complex scenarios** (multi-step workflows)
3. **Add performance tests** for bulk operations

## 📈 Expected Coverage Improvement

| Metric | Current | Target | Improvement |
|--------|---------|--------|-------------|
| Business Rules | 42% | 85% | +43% |
| Aggregates | 0% | 75% | +75% |
| Critical Paths | 0% | 90% | +90% |
| Overall Domain | ~15% | 70% | +55% |

## 🛠️ Technical Solutions for Testing Challenges

### Challenge 1: Private Constructors
**Solution**: Create `InternalsVisibleTo` attribute for test assembly
```csharp
[assembly: InternalsVisibleTo("Axon.Modules.Identity.Domain.Tests")]
```

### Challenge 2: Complex Entity Setup
**Solution**: Builder pattern with reflection
```csharp
public class AggregateTestBuilder<T>
{
    public T BuildWithReflection(params object[] ctorArgs)
    {
        var ctor = typeof(T).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null, 
            ctorArgs.Select(a => a.GetType()).ToArray(), 
            null);
        return (T)ctor.Invoke(ctorArgs);
    }
}
```

### Challenge 3: Domain Event Testing
**Solution**: Event capture helper
```csharp
public class DomainEventCapture
{
    public List<IDomainEvent> CapturedEvents { get; } = new();
    
    public void CaptureFrom(AggregateRoot aggregate)
    {
        var events = aggregate.GetDomainEvents();
        CapturedEvents.AddRange(events);
    }
}
```

## ⏭️ Next Steps

1. **Immediate**: Add `InternalsVisibleTo` to Domain project
2. **Day 1-2**: Implement TestableAggregateFactory
3. **Day 3-5**: Write Priority 1.1 tests
4. **Week 2**: Continue with Priority 1.2 and 1.3

## 📊 Success Metrics

- **Test Count**: Increase from 48 to 100+ tests
- **Coverage**: Achieve 70%+ domain coverage
- **Critical Paths**: 90%+ coverage on state transitions
- **Build Time**: Keep test execution under 1 second
- **Maintainability**: All tests follow consistent patterns