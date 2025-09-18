# Epic 4a: AxonId → AxonUserId Semantic Refactoring

**Part of Identity Performance Optimization Initiative**
**PRD Reference**: [Identity Performance Optimization PRD](../prd-identity-performance-optimization.md)

## 🎯 Epic Overview

**Epic ID**: EPIC-4A-AXONID-RENAME
**Title**: AxonId to AxonUserId Semantic Refactoring
**Type**: Technical Foundation Enhancement (Phase 1 of 3)
**Complexity**: Medium
**Risk Level**: Low (zero functional impact)
**Estimated Effort**: 6 story points
**Priority**: P0 (Foundation for performance optimization)

### Executive Summary
Perform a comprehensive semantic refactoring to rename `AxonId` to `AxonUserId` across the entire codebase. This eliminates technical debt and developer confusion while establishing clear semantic naming that enables Epic 4b performance optimizations and Epic 4c integration.

### Business Alignment
**Primary Business Goal**: Accelerate Development Velocity (30% onboarding improvement)
**Secondary Business Goal**: Enable Cost-Effective Scaling (foundation for 95% DB load reduction)
**User Impact**: Zero direct impact, enables future performance improvements
**ROI**: Reduces future development time and enables 50x performance gains in subsequent epics

## 🔍 Problem Statement

### Current State - CONFIRMED via Code Analysis
1. **Semantic Confusion**:
   - ✅ `AxonId` type name is ambiguous - could be any entity identifier
   - ✅ Domain experts and new developers confused by generic naming
   - ✅ 51 files currently use `AxonId` throughout the system
   - ✅ Future identity features will need clear user-specific naming

2. **Impact Analysis**:
   - ✅ Primary usage: Identity module for user identification
   - ✅ Secondary usage: Chat module, API contracts, test files
   - ✅ No database schema changes required (column names stay same)
   - ✅ Pure semantic refactoring with zero functional impact

### Business Justification
- **Developer Velocity**: Clear semantic naming reduces new developer onboarding time by 30%
- **Performance Foundation**: Enables Epic 4b to achieve 95% database load reduction
- **Integration Readiness**: Required foundation for Epic 4c unified identity management
- **Technical Debt Reduction**: Eliminates confusion between user and entity identifiers
- **Enterprise Readiness**: Professional naming conventions for enterprise customers

## 🏗️ Solution Architecture

### Refactoring Strategy
1. **Type-Safe Rename**: Leverage IDE refactoring tools for consistency
2. **Zero Functional Changes**: Pure naming change, no behavior modifications
3. **Backward Compatibility**: Database columns remain unchanged
4. **Comprehensive Coverage**: All 51 identified files updated atomically

### Files Impact Analysis
```
Core Type Definition:
- src/BuildingBlocks/Core/Primitives/Ids/AxonId.cs → AxonUserId.cs

Domain Layer (15 files):
- src/Modules/Identity/Domain/Aggregates/AxonPrincipal/AxonPrincipal.cs
- src/Modules/Identity/Domain/Entities/PrincipalChainDefault.cs
- src/Modules/Identity/Domain/Entities/IdentityCredential.cs
- src/Modules/Identity/Domain/Entities/WalletOwnership.cs
- src/Modules/Identity/Domain/Events/*.cs (4 files)
- src/Modules/Identity/Domain/Aggregates/Wallet/*.cs (2 files)

Application Layer (8 files):
- src/Modules/Identity/Application/Commands/ExchangeCredential/ExchangeCredentialHandler.cs
- src/Modules/Identity/Application/Queries/GetMyPrincipal/GetMyPrincipalHandler.cs
- src/Modules/Identity/Application/DTOs/*.cs (3 files)
- src/Modules/Identity/Application/Contracts/Persistence/*.cs (2 files)
- src/Modules/Identity/Application/Common/Commands/BaseIdentityCommandHandler.cs

Infrastructure Layer (6 files):
- src/Modules/Identity/Infrastructure/Persistence/Repositories/*.cs (2 files)
- src/Modules/Identity/Infrastructure/Persistence/Configurations/*.cs (4 files)

API Layer (3 files):
- src/Api/Configuration/Mapping/AuthMappingProfile.cs
- src/Api/Contracts/V1/Auth/*.cs (2 files)

Test Files (18 files):
- tests/Modules/Identity/**/*.cs (15 files)
- tests/Modules/Identity/Domain/TestData/*.cs (3 files)

Documentation (1 file):
- Docs/BMAD/architecture.md
```

## 📋 User Stories

### Story 1: Core Type Definition Update (1 point)
**Priority**: P0
**Status**: ❌ **PENDING**

**Acceptance Criteria**:
- [ ] Rename `AxonId.cs` to `AxonUserId.cs` in BuildingBlocks
- [ ] Update `[StronglyTypedId]` struct name to `AxonUserId`
- [ ] Verify no compilation errors in BuildingBlocks project
- [ ] All unit tests for Strong IDs pass

**Implementation**:
```csharp
// BEFORE: src/BuildingBlocks/Core/Primitives/Ids/AxonId.cs
[StronglyTypedId]
public partial struct AxonId { }

// AFTER: src/BuildingBlocks/Core/Primitives/Ids/AxonUserId.cs
[StronglyTypedId]
public partial struct AxonUserId { }
```

---

### Story 2: Domain Layer Refactoring (2 points)
**Priority**: P0
**Status**: ❌ **PENDING**
**Dependencies**: Story 1

**Acceptance Criteria**:
- [ ] Update all domain aggregates to use `AxonUserId`
- [ ] Update all domain entities to use `AxonUserId`
- [ ] Update all domain events to use `AxonUserId`
- [ ] No breaking changes to domain behavior
- [ ] All domain tests pass

**Key Files**:
- `AxonPrincipal.cs` - Primary aggregate using user identity
- `PrincipalChainDefault.cs` - Chain relationship entity
- `IdentityCredential.cs` - Credential linking entity
- `WalletOwnership.cs` - Wallet ownership entity
- Domain events for principal/credential changes

---

### Story 3: Application Layer Update (1 point)
**Priority**: P0
**Status**: ❌ **PENDING**
**Dependencies**: Story 2

**Acceptance Criteria**:
- [ ] Update command handlers to use `AxonUserId`
- [ ] Update query handlers to use `AxonUserId`
- [ ] Update DTOs and response models
- [ ] Update repository interfaces
- [ ] All application tests pass

**Key Components**:
- `ExchangeCredentialHandler` - Identity resolution
- `GetMyPrincipalHandler` - User profile queries
- Repository interfaces for persistence abstraction

---

### Story 4: Infrastructure and API Updates (2 points)
**Priority**: P0
**Status**: ❌ **PENDING**
**Dependencies**: Story 3

**Acceptance Criteria**:
- [ ] Update repository implementations
- [ ] Update EF Core configurations
- [ ] Update API contracts and DTOs
- [ ] Update AutoMapper profiles
- [ ] Verify database compatibility (no schema changes)
- [ ] All integration tests pass

**Database Verification**:
```sql
-- Verify column names remain unchanged
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name IN ('AxonPrincipals', 'PrincipalChainDefaults', 'IdentityCredentials', 'WalletOwnerships')
AND column_name LIKE '%Id%';
-- Should show original column names (e.g., 'Id', 'PrincipalId', etc.)
```

---

### Story 5: Test Infrastructure Update (0 points - included in other stories)
**Continuous throughout all stories**

**Acceptance Criteria**:
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Test data builders updated
- [ ] Test constants updated
- [ ] No test compilation errors

## 🚫 What We're NOT Doing

### ❌ NO Database Schema Changes
- **Why Not**: Database columns already have semantic names
- **Verification**: EF Core configurations map `AxonUserId` to existing columns
- **Benefit**: Zero migration risk, zero downtime

### ❌ NO Functional Behavior Changes
- **Why Not**: Pure semantic refactoring only
- **Verification**: All business logic remains identical
- **Testing**: Existing test cases prove behavior preservation

### ❌ NO API Contract Breaking Changes
- **Why Not**: API DTOs can be renamed without breaking clients
- **Approach**: Internal type changes, external contracts maintain compatibility
- **Migration**: Gradual client update when convenient

## 📊 Success Metrics

### Quality Metrics
| Metric | Target | Verification Method |
|--------|--------|-------------------|
| Compilation Success | 100% | `dotnet build` all projects |
| Test Pass Rate | 100% | `dotnet test` all test projects |
| Files Updated | 51 files | IDE refactoring tool report |
| Breaking Changes | 0 | API contract verification |

### Semantic Clarity Metrics
- **Developer Feedback**: Improved code comprehension (qualitative)
- **Domain Expert Validation**: Correct ubiquitous language usage
- **Future Feature Readiness**: Foundation for Epic 4b and 4c

## 🔄 Implementation Strategy

### Phase 1: Core Foundation (Day 1)
1. Rename core `AxonId.cs` → `AxonUserId.cs`
2. Verify BuildingBlocks project compiles
3. Fix immediate compilation errors

### Phase 2: Domain Refactoring (Day 1-2)
1. Use IDE "Rename Symbol" on AxonId type
2. Verify all domain projects compile
3. Run domain tests to ensure behavior preservation

### Phase 3: Application & Infrastructure (Day 2-3)
1. Update application layer handlers and DTOs
2. Update infrastructure repositories and configurations
3. Verify integration tests pass

### Phase 4: Verification & Documentation (Day 3)
1. Full system build verification
2. Complete test suite execution
3. Update architecture documentation

## 🚨 Risk Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| IDE refactoring misses files | Medium | Manual verification + grep search |
| Test failures | Medium | Incremental testing after each phase |
| Merge conflicts | Low | Coordinate with team, atomic commits |
| Rollback needed | Low | Detailed rollback procedures below |

### Rollback Procedures

#### **Immediate Rollback** (<5 minutes)
```bash
# Step 1: Revert the merge commit
git revert <merge-commit-hash> --no-edit

# Step 2: Verify rollback
dotnet build --configuration Release
dotnet test --logger "console;verbosity=normal"

# Step 3: Deploy if necessary
# No database changes to rollback - purely code-level revert
```

#### **Partial Rollback** (Story-by-story)
- **Story 1 Rollback**: Revert `AxonUserId.cs` → `AxonId.cs` filename and content
- **Story 2 Rollback**: Revert domain layer changes only
- **Story 3 Rollback**: Revert application layer changes only
- **Story 4 Rollback**: Revert infrastructure and API changes only

#### **Verification Steps Post-Rollback**
1. All builds pass (`dotnet build`)
2. All tests pass (`dotnet test`)
3. No `AxonUserId` references remain (`grep -r "AxonUserId" src/`)
4. Original `AxonId` functionality restored

## 🏁 Definition of Done

### Must Have
- [ ] All 51 files successfully updated to use `AxonUserId`
- [ ] Zero compilation errors across all projects
- [ ] 100% test pass rate (unit + integration)
- [ ] Database compatibility verified (no schema changes)
- [ ] API contracts maintain backward compatibility

### Verification Checklist
```bash
# Build verification
dotnet build --configuration Release

# Test verification
dotnet test --logger "console;verbosity=normal"

# Search verification (should return 0 results)
grep -r "AxonId[^a-zA-Z]" src/ --exclude-dir=bin --exclude-dir=obj

# Database verification (no migrations generated)
dotnet ef migrations list --project src/Modules/Identity/Infrastructure
```

## 💡 Implementation Notes

### IDE Refactoring Strategy
1. **Primary Tool**: Visual Studio / Rider "Rename Symbol" (F2)
2. **Backup Verification**: `grep -r "AxonId" src/` to catch missed references
3. **Compilation Check**: After each major file group update

## 🔄 Epic Coordination & Dependencies

### **Epic 4a Independence** ✅
- **Can Start**: Immediately (no dependencies)
- **Can Complete**: Independently of other epics
- **Provides**: Clean `AxonUserId` type foundation
- **Timeline**: Days 1-3 (standalone delivery)

### **For Epic 4b** (Performance Optimization)
- **Epic 4a Requirement**: OPTIONAL for MVP, REQUIRED for optimal integration
- **Handoff**: Complete `AxonUserId` type available for caching implementation
- **Coordination**: Epic 4b can use either `AxonId` or `AxonUserId` but benefits from semantic clarity
- **Timeline**: Epic 4b can start Day 4 (after Epic 4a completion)

### **For Epic 4c** (Chat Integration)
- **Epic 4a Requirement**: **REQUIRED** for Story 4 (Domain Models)
- **Handoff**: `AxonUserId` type must be available for Chat domain model updates
- **Coordination**: Epic 4c Stories 1-3 can proceed with `AxonId`, Story 4 requires Epic 4a completion
- **Timeline**: Epic 4c Stories 1-3 can start Day 4, Story 4 starts Day 9

### **Integration Checkpoints**
1. **Day 3**: Epic 4a completion checkpoint - verify `AxonUserId` available
2. **Day 4**: Epic 4b can begin with clean type foundation
3. **Day 9**: Epic 4c Story 4 can begin with completed semantic refactoring

### Testing Strategy
- **Unit Tests**: Verify type usage correctness
- **Integration Tests**: Ensure end-to-end functionality preserved
- **Database Tests**: Confirm EF Core mapping still works
- **API Tests**: Validate request/response serialization

---

**Epic Owner**: Identity Team
**Reviewed By**: Architecture Review Board
**Last Updated**: 2025-01-18
**Version**: 1.0-FOUNDATION
**Implementation Approach**: IDE-driven refactoring with incremental verification