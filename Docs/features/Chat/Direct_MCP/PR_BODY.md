---
id: AXON-20250731-Chat-Direct_MCP-PR_BODY
title: Direct_MCP: PR Body
module: Chat
feature: Direct_MCP
gate: Ship
owner: valentynkit
status: approved
relates_to: [AXON-20250729-Chat-Direct_MCP-REQUIREMENTS, AXON-20250729-Chat-Direct_MCP-ARCHITECTURE]
source_of_truth: doc
created: 2025-07-31
updated: 2025-07-31
version: 1
---

# Summary
Migrated from OpenAI SDK Chat Completions API to direct HTTP Responses API calls for true Direct MCP integration. This eliminates app-mediated tool orchestration, enabling server-to-server communication between OpenAI and MCP servers while achieving sub-2 second response times for tool-based queries.

# Scope of Change
**Modules Touched**: Chat Infrastructure layer  
**Core Changes**:
- Complete rewrite of `OpenAiClient.cs` for Direct MCP via Responses API
- Updated DI configuration in `ServiceRegistration.cs` for HttpClient usage  
- Package reference updates in `Infrastructure.csproj`

**Architecture Links**: [ARCHITECTURE.md](./ARCHITECTURE.md) | [TASK_PLAN.md](./TASK_PLAN.md)

# Risks & Mitigations
**Performance Risk**: Dependency on OpenAI Responses API (preview)  
*Mitigation*: Graceful error handling with fallback messaging; monitoring in place

**Security Risk**: Direct API calls with authentication headers  
*Mitigation*: Sensitive data removed from logs and exceptions (critical security fixes applied)

**Operational Risk**: Network calls to external OpenAI service  
*Mitigation*: 30-second timeout configured; proper error categorization implemented

# Testing Evidence
- **Build**: ✅ SUCCESS (commit: 780d9df) - 0 warnings, 0 errors
- **Tests**: ✅ PASS - 270 total tests (22 App + 65 Domain + 29 Architecture + 98 Infrastructure + 56 API)
- **Health Check**: ✅ SUCCESS - Build and test pipeline verified at 2025-07-31

# Contract Changes
**API Surface**: No breaking changes - all existing endpoints and DTOs preserved  
**Internal Changes**: `OpenAiClient` implementation completely rewritten but maintains same `IAiClient` interface contract

# Breaking Changes
None - All public API contracts maintained, Clean Architecture boundaries preserved

# Follow-ups
- Monitor OpenAI Responses API stability in production
- Consider streaming response support in Phase 2
- Evaluate multi-MCP server support based on usage patterns