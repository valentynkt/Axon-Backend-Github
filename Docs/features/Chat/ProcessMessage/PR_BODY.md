---
id: AXON-20250730-Chat-ProcessMessage-PR_BODY
title: FastEndpoints Migration: PR Body
module: Chat
feature: ProcessMessage
gate: Ship
owner: system
status: blocked
relates_to: []
source_of_truth: doc
created: 2025-07-30
updated: 2025-07-30
version: 1
---

# Summary
**STATUS: BLOCKED - Build compilation errors must be resolved before merge**

Migration of ProcessMessage endpoint from MVC Controllers to FastEndpoints with unified error handling and simplified MCP configuration via server ID resolution.

# Scope of Change
- **Primary Change**: MVC → FastEndpoints migration for `/api/chat/process` endpoint
- **Error Handling**: Introduced unified ErrorMapper for consistent API responses
- **MCP Simplification**: Replaced direct URL/headers with server ID-based configuration
- **Architecture**: Maintained REPR pattern and clean separation of concerns

Links: [ARCHITECTURE.md](./ARCHITECTURE.md) | [TASK_PLAN.md](./TASK_PLAN.md)

# Risks & Mitigations
**CRITICAL: Build is currently failing - merge blocked until compilation errors resolved**

- **Risk**: Contract compatibility during migration
- **Mitigation**: Maintained identical request/response structure
- **Risk**: Runtime behavior changes  
- **Mitigation**: FastEndpoints provides similar validation and processing pipeline

# Testing Evidence
- **Build**: ❌ FAILED - 53 compilation errors (ref: 8aef54b)
- **Tests**: ❌ BLOCKED - Cannot run tests due to build failures
- **Health Check**: ❌ BLOCKED - Application cannot start due to compilation errors

## Build Errors Summary
Critical compilation issues in test files:
- ProcessMessageCommand constructor parameter mismatches (53 errors)
- Missing McpServerRequest references in API tests
- Obsolete IMcpServerResolver method calls
- ProcessMessageRequest constructor incompatibilities

**Required before merge**: Fix all compilation errors and achieve green build

# Contract Changes
- **Endpoint Route**: `/api/chat/process` (maintained from MVC version)
- **Request Structure**: ProcessMessageRequest unchanged for compatibility
- **Response Structure**: ProcessMessageResponse unchanged
- **Error Responses**: Standardized via ErrorMapper

Link to contracts: `contracts/Chat/API_CONTRACT.md` (sync pending after build fix)

# Breaking Changes
**None** - API surface maintains full backward compatibility

# Follow-ups
1. Fix compilation errors in test files (blocking)
2. Update API contract documentation after successful build
3. Run full test suite and update TEST_REPORT.md
4. Complete health check verification
5. Post-merge: Monitor endpoint performance and error rates