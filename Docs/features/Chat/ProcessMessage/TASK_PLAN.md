---
id: AXON-20250730-chat-processmessage-TASK_PLAN
title: ProcessMessage: Task Plan
module: Chat
feature: ProcessMessage
gate: G1
owner: system-designer&planner
status: draft
relates_to: []
source_of_truth: doc
created: 2025-07-30
updated: 2025-07-30
version: 1
---

# Plan Summary

This task plan migrates the ProcessMessageEndpoint from MVC Controllers to FastEndpoints REPR pattern following atomic, reversible steps. Each task represents one compile-safe, testable change that can be individually rolled back.

## Tasks (Infrastructure → Api Pattern)

### T1: Add FastEndpoints Infrastructure
**Why**: Establish FastEndpoints foundation without affecting existing functionality

**Steps:**
1. Add FastEndpoints NuGet package to `src/Api/Axon.Api.csproj`
2. Update `src/Api/Configuration/ServiceRegistration.cs` to register FastEndpoints alongside MVC
3. Update `src/Api/Program.cs` to configure FastEndpoints pipeline alongside MVC

**Files to Touch:**
- `src/Api/Axon.Api.csproj` (add PackageReference)
- `src/Api/Configuration/ServiceRegistration.cs` (add .AddFastEndpoints())
- `src/Api/Program.cs` (add .UseFastEndpoints() before MapControllers)

**Acceptance Criteria:**
- Application starts successfully
- Existing MVC endpoint remains functional
- No breaking changes to HTTP contract
- Swagger documentation unchanged

**Rollback:** Revert all 3 files to previous versions

---

### T2: Create Unified Error Handling Infrastructure
**Why**: Extract error handling logic to eliminate duplication and improve maintainability

**Steps:**
1. Create `src/Api/Common/ErrorHandling/IErrorMapper.cs` interface
2. Create `src/Api/Common/ErrorHandling/ErrorMapper.cs` implementation
3. Create `src/Api/Extensions/EndpointExtensions.cs` for Result handling
4. Register ErrorMapper in ServiceRegistration

**Files to Touch:**
- `src/Api/Common/ErrorHandling/IErrorMapper.cs` (new)
- `src/Api/Common/ErrorHandling/ErrorMapper.cs` (new)
- `src/Api/Extensions/EndpointExtensions.cs` (new)
- `src/Api/Configuration/ServiceRegistration.cs` (register IErrorMapper)

**Acceptance Criteria:**
- Error handling components compile successfully
- Service registration succeeds
- No functional changes to existing behavior
- Unit tests pass for error mapping logic

**Rollback:** Delete new files, revert ServiceRegistration.cs

---

### T3: Create FastEndpoints ProcessMessage Implementation
**Why**: Implement REPR pattern version alongside existing MVC controller

**Steps:**
1. Create `src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageEndpoint.cs`
2. Create `src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageValidator.cs` (optional)
3. Configure endpoint route as `POST /api/chat/process-v2` (temporary different route)
4. Implement endpoint using unified error handling

**Files to Touch:**
- `src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageEndpoint.cs` (new)
- `src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageValidator.cs` (new, optional)

**Acceptance Criteria:**
- New endpoint compiles and registers successfully
- Endpoint handles requests at `/api/chat/process-v2`
- Error handling works consistently
- Request/response mapping identical to MVC version
- Integration tests pass for new endpoint

**Rollback:** Delete new endpoint files

---

### T4: Validation & Testing Phase
**Why**: Ensure new implementation matches existing behavior exactly

**Steps:**
1. Create integration tests comparing MVC vs FastEndpoints responses
2. Run load tests to verify performance improvements
3. Validate HTTP contract compatibility
4. Verify error responses match exactly

**Files to Touch:**
- `tests/Api.Tests/Integration/Chat/ProcessMessageEndpointTests.cs` (new/updated)
- Performance test scripts (if needed)

**Acceptance Criteria:**
- Both endpoints return identical responses for same inputs
- Performance metrics show improvement
- All error scenarios handled identically
- HTTP status codes match exactly

**Rollback:** Remove test files (non-breaking)

---

### T5: Route Migration
**Why**: Switch traffic to FastEndpoints version while preserving rollback capability

**Steps:**
1. Change FastEndpoints route from `/api/chat/process-v2` to `/api/chat/process`
2. Change MVC route from `/api/chat/process` to `/api/chat/process-legacy`
3. Update Swagger documentation to show new endpoint as primary

**Files to Touch:**
- `src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageEndpoint.cs` (route change)
- `src/Api/Endpoints/Chat/ProcessMessageEndpoint.cs` (route change)

**Acceptance Criteria:**
- FastEndpoints handles `/api/chat/process`
- MVC fallback available at `/api/chat/process-legacy`
- Swagger shows FastEndpoints as primary
- All tests pass with new routing

**Rollback:** Swap routes back (MVC to `/api/chat/process`, FastEndpoints to `/api/chat/process-v2`)

---

### T6: Remove Legacy MVC Implementation
**Why**: Clean up codebase after successful migration

**Steps:**
1. Remove `src/Api/Endpoints/Chat/ProcessMessageEndpoint.cs` (MVC version)
2. Update routing configuration to remove legacy route
3. Clean up any MVC-specific references if no other controllers remain

**Files to Touch:**
- `src/Api/Endpoints/Chat/ProcessMessageEndpoint.cs` (delete)
- `src/Api/Program.cs` (potentially remove MapControllers if last controller)
- `src/Api/Configuration/ServiceRegistration.cs` (potentially remove AddControllers)

**Acceptance Criteria:**
- Only FastEndpoints version remains
- No references to deleted controller
- Application starts and functions normally
- Reduced memory footprint from eliminating MVC pipeline

**Rollback:** Restore deleted MVC controller file, revert Program.cs/ServiceRegistration.cs

---

### T7: Documentation & Optimization
**Why**: Update documentation and apply any final optimizations

**Steps:**
1. Update API documentation to reflect FastEndpoints pattern
2. Optimize endpoint configuration if needed
3. Update architectural documentation
4. Add performance metrics to monitoring

**Files to Touch:**
- API documentation files
- Monitoring/telemetry configuration
- Architecture documentation updates

**Acceptance Criteria:**
- Documentation accurately reflects new implementation
- Monitoring captures FastEndpoints metrics
- Team understands new patterns

**Rollback:** Revert documentation changes (non-functional)

## Milestones & Criteria

### M1: Infrastructure Ready (T1-T2 Complete)
**Criterion**: FastEndpoints and unified error handling infrastructure in place, existing functionality unchanged

### M2: Parallel Implementation (T3 Complete)
**Criterion**: Both MVC and FastEndpoints versions working side-by-side with identical behavior

### M3: Migration Complete (T4-T5 Complete)
**Criterion**: FastEndpoints serving production traffic, MVC version available as fallback

### M4: Legacy Removed (T6-T7 Complete)
**Criterion**: Clean codebase with only FastEndpoints implementation and updated documentation

## Rollback Plan

### Per-Task Rollback
Each task includes specific rollback instructions that can be executed independently:

- **T1 Rollback**: Remove PackageReferences, revert Program.cs and ServiceRegistration.cs
- **T2 Rollback**: Delete error handling files, revert ServiceRegistration.cs  
- **T3 Rollback**: Delete FastEndpoints implementation files
- **T4 Rollback**: Remove test files (non-breaking)
- **T5 Rollback**: Swap routes back to original configuration
- **T6 Rollback**: Restore deleted MVC controller, revert configuration files
- **T7 Rollback**: Revert documentation (non-functional)

### Emergency Full Rollback
If issues are discovered after multiple tasks:

1. **Immediate**: Restore MVC controller to `/api/chat/process` route
2. **Short-term**: Remove FastEndpoints package and configuration
3. **Clean-up**: Remove all FastEndpoints-related files and error handling infrastructure

### Rollback Validation
After any rollback:
- Integration tests must pass
- HTTP contract must be identical to pre-migration state
- Performance must be at least equivalent to original
- No regression in functionality

## Effort Estimate

**Size: M (Medium)**

**Breakdown:**
- **T1-T2**: 4-6 hours (infrastructure setup)
- **T3**: 6-8 hours (FastEndpoints implementation)
- **T4**: 4-6 hours (testing and validation)
- **T5**: 2-4 hours (route migration)
- **T6**: 2-3 hours (cleanup)
- **T7**: 2-3 hours (documentation)

**Total**: 20-30 hours over 3-5 working days

**Dependencies:**
- Requires understanding of FastEndpoints patterns
- Needs access to integration testing environment
- Performance testing capabilities helpful but not blocking

**Risks:**
- Learning curve for FastEndpoints patterns (medium)
- Potential HTTP contract compatibility issues (low)
- Performance regression risk (very low)