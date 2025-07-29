---
id: AXON-20250729-Chat-Direct_MCP-TASK_PLAN
title: Direct_MCP: Task Plan
module: Chat
feature: Direct_MCP
gate: G1
owner: <owner>
status: draft
relates_to: []
source_of_truth: doc
created: 2025-07-29
updated: 2025-07-29
version: 1
---

# Plan Summary

## Approach
Implement Direct MCP feature using Clean Architecture + CQRS pattern, following strict dependency order (Domain → Application → Infrastructure → Api). The implementation creates a new endpoint that enables OpenAI to directly communicate with remote MCP servers, eliminating application-mediated tool orchestration.

**Key Strategy:**
- Build minimal viable skeleton first (Domain → Application)
- Add Infrastructure adapter with OpenAI Responses API integration
- Complete with API endpoint and validation
- Use Result<T> pattern for error handling throughout
- Implement observability with Activity tracing and structured logging

## Estimated Timeline
**Total Effort: Medium (M) - 3-4 days**
- Domain layer: 4 hours (minimal - mostly value objects)
- Application layer: 8 hours (command/handler/port definitions)
- Infrastructure layer: 12 hours (OpenAI client with MCP integration)
- API layer: 4 hours (endpoint + contracts + DI wiring)
- Testing & integration: 4 hours

# Tasks (Domain → Application → Infrastructure → Api)

## T1: Shared Foundation Setup
**Why**: Establish common Result<T> patterns and error types needed across all layers

**Steps:**
1. Add Result<T> and Error types to Shared.Common
2. Add IRequest/IRequestHandler abstractions to Shared.Common.Abstractions
3. Verify project references and namespace consistency

**Files to Touch:**
- `src/Shared/Common/Result.cs` (new)
- `src/Shared/Common/Error.cs` (new) 
- `src/Shared/Common.Abstractions/IRequest.cs` (new)
- `src/Shared/Common.Abstractions/IRequestHandler.cs` (new)
- Project references updated

**Dependencies**: None
**Duration**: 2 hours
**Completion Criteria**: Result<T> patterns available, compiles clean

## T2: Domain Layer - Value Objects and Types
**Why**: Define core domain concepts (MessageId, ConversationId) and tool execution types

**Steps:**
1. Create basic value objects for strongly-typed IDs
2. Add tool execution domain types
3. Create domain-specific Result error types
4. Ensure no external dependencies in Domain

**Files to Touch:**
- `src/Modules/Chat/Domain/ValueObjects/MessageId.cs` (new)
- `src/Modules/Chat/Domain/ValueObjects/ConversationId.cs` (new)
- `src/Modules/Chat/Domain/ValueObjects/McpServerUrl.cs` (new)
- `src/Modules/Chat/Domain/Types/ToolExecution.cs` (new)
- `src/Modules/Chat/Domain/Errors/ChatErrors.cs` (new)
- `src/Modules/Chat/Domain/Axon.Modules.Chat.Domain.csproj` (new)

**Dependencies**: T1 (Shared foundation)
**Duration**: 3 hours
**Completion Criteria**: Domain types defined, no external dependencies

## T3: Application Layer - Ports and Commands
**Why**: Define application contracts (IAiClient port) and CQRS command/handler structure

**Steps:**
1. Create IAiClient port interface with MCP-specific methods
2. Define AiRequest/AiResponse DTOs for port communication
3. Create ProcessMessageCommand and handler structure
4. Add FluentValidation for command validation
5. Set up project structure and references

**Files to Touch:**
- `src/Modules/Chat/Application/Abstractions/IAiClient.cs` (new)
- `src/Modules/Chat/Application/DTOs/AiRequest.cs` (new)  
- `src/Modules/Chat/Application/DTOs/AiResponse.cs` (new)
- `src/Modules/Chat/Application/DTOs/McpServerConfig.cs` (new)
- `src/Modules/Chat/Application/Commands/ProcessMessage/ProcessMessageCommand.cs` (new)
- `src/Modules/Chat/Application/Commands/ProcessMessage/ProcessMessageHandler.cs` (new)
- `src/Modules/Chat/Application/Commands/ProcessMessage/ProcessMessageValidator.cs` (new)
- `src/Modules/Chat/Application/Axon.Modules.Chat.Application.csproj` (new)

**Dependencies**: T2 (Domain layer)
**Duration**: 4 hours  
**Completion Criteria**: Command/handler pattern complete, port interfaces defined

## T4: Infrastructure Layer - OpenAI MCP Client
**Why**: Implement the IAiClient port using OpenAI Responses API with direct MCP integration

**Steps:**
1. Add OpenAI SDK and HTTP client dependencies
2. Implement OpenAiClient that calls Responses API with MCP tools
3. Add request/response mapping logic for MCP-specific fields
4. Implement error handling and Result<T> mapping
5. Add structured logging and Activity tracing
6. Create service registration for DI

**Files to Touch:**
- `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs` (new)
- `src/Modules/Chat/Infrastructure/Ai/OpenAiOptions.cs` (new)
- `src/Modules/Chat/Infrastructure/Ai/Models/OpenAiRequest.cs` (new)
- `src/Modules/Chat/Infrastructure/Ai/Models/OpenAiResponse.cs` (new)
- `src/Modules/Chat/Infrastructure/Ai/Models/McpTool.cs` (new)
- `src/Modules/Chat/Infrastructure/Configuration/ServiceRegistration.cs` (new)
- `src/Modules/Chat/Infrastructure/Axon.Modules.Chat.Infrastructure.csproj` (new)

**Dependencies**: T3 (Application ports)
**Duration**: 8 hours
**Completion Criteria**: OpenAI integration working, MCP tools configured, error handling complete

## T5: API Layer - Endpoint and Contracts  
**Why**: Expose HTTP endpoint for chat processing with MCP configuration options

**Steps:**
1. Create API request/response DTOs
2. Implement ProcessMessageEndpoint with proper HTTP mapping
3. Add endpoint registration and routing
4. Wire up dependency injection for Chat module
5. Add configuration settings for MCP defaults
6. Update Program.cs to register Chat module

**Files to Touch:**
- `src/Api/Contracts/Chat/ProcessMessageRequest.cs` (new)
- `src/Api/Contracts/Chat/ProcessMessageResponse.cs` (new)
- `src/Api/Contracts/Chat/McpServerRequest.cs` (new)
- `src/Api/Endpoints/Chat/ProcessMessageEndpoint.cs` (new)
- `src/Api/Configuration/ServiceRegistration.cs` (update)
- `src/Api/Program.cs` (update)
- `src/Api/appsettings.json` (update - MCP config)

**Dependencies**: T4 (Infrastructure implementation)
**Duration**: 3 hours
**Completion Criteria**: HTTP endpoint functional, DI properly configured

## T6: Integration Testing and Verification
**Why**: Ensure end-to-end functionality and proper error handling

**Steps:**
1. Create integration tests for the full flow
2. Test with mock MCP server responses
3. Verify error handling scenarios (invalid URLs, timeouts)
4. Test conversation context caching via previous_response_id
5. Validate performance targets (<2s response time)
6. Verify observability (logs, activities, metrics)

**Files to Touch:**
- `tests/Api.Tests/Endpoints/Chat/ProcessMessageEndpointTests.cs` (new)
- `tests/Modules.Chat.Infrastructure.Tests/Ai/OpenAiClientTests.cs` (new)
- `tests/Modules.Chat.Application.Tests/Commands/ProcessMessageHandlerTests.cs` (new)
- Test configuration and mock setups

**Dependencies**: T5 (API complete)
**Duration**: 4 hours
**Completion Criteria**: All tests passing, performance targets met

# Milestones & Criteria

## M1: Foundation Complete (After T1-T2)
**Criteria:**
- [ ] Result<T> patterns available in Shared.Common
- [ ] Domain value objects defined with proper validation
- [ ] No external dependencies in Domain layer
- [ ] Solution builds without warnings

## M2: Application Contracts Defined (After T3)
**Criteria:**  
- [ ] IAiClient port interface complete with MCP support
- [ ] ProcessMessageCommand/Handler structure in place
- [ ] FluentValidation rules implemented
- [ ] All application dependencies properly referenced
- [ ] Command validation tests passing

## M3: MCP Integration Working (After T4)
**Criteria:**
- [ ] OpenAI Responses API integration functional
- [ ] MCP tool configuration working (server_url, headers, allowed_tools)
- [ ] Error handling for MCP server failures implemented
- [ ] Conversation context caching via previous_response_id working  
- [ ] Structured logging and Activity tracing in place
- [ ] Performance targets achieved (<2s response time)

## M4: End-to-End Complete (After T5-T6)
**Criteria:**
- [ ] HTTP endpoint accessible at POST /api/chat/process
- [ ] Full request/response flow working with real MCP server
- [ ] Error scenarios handled gracefully (invalid config, MCP unavailable)
- [ ] Integration tests passing for happy path and error cases
- [ ] Configuration settings working (appsettings.json)
- [ ] DI registration complete and functional

# Rollback Plan

## Immediate Rollback (if implementation blocked)
1. **Revert commits**: Roll back all commits related to Direct_MCP feature
2. **Remove project references**: Clean up any new project references in Api layer
3. **Restore clean state**: Ensure solution builds and existing functionality unaffected
4. **Toggle feature flag**: Set `Chat.DirectMcp.Enabled = false` in configuration

## Partial Rollback (if external dependencies fail)
1. **Disable MCP integration**: Remove OpenAI MCP tool configuration
2. **Fallback to standard chat**: Process messages without tool integration  
3. **Maintain API contract**: Keep endpoint structure, return "MCP unavailable" responses
4. **Monitor and alert**: Set up monitoring for when MCP can be re-enabled

## Dependencies Rollback
1. **OpenAI SDK issues**: Pin to last known working version, disable MCP-specific features
2. **MCP server unavailable**: Configure fallback behavior, clear user messaging
3. **Configuration problems**: Provide sensible defaults, validate configuration on startup

## Recovery Steps
1. **Identify root cause**: Check logs, external service status, configuration issues
2. **Apply targeted fix**: Address specific issue without full rollback if possible
3. **Validate fix**: Run integration tests, check performance targets
4. **Gradual re-enable**: Use feature flags to slowly restore functionality
5. **Monitor closely**: Watch for error rates, performance impacts, user feedback

# Effort Estimate

## Size: Medium (M)

**Breakdown by Layer:**
- **Domain (Simple)**: 3 hours - Basic value objects, minimal complexity
- **Application (Medium)**: 4 hours - CQRS pattern, port definitions, validation  
- **Infrastructure (Complex)**: 8 hours - OpenAI integration, MCP protocol, error handling
- **API (Simple)**: 3 hours - Endpoint creation, DTO mapping, DI wiring
- **Testing (Medium)**: 4 hours - Integration tests, mock setups, edge cases

**Total: 22 hours (~3 development days)**

**Risk Factors Adding Complexity:**
- OpenAI Responses API preview stability (could add 2-4 hours debugging)
- MCP server integration challenges (could add 2-3 hours)  
- Performance optimization for <2s target (could add 1-2 hours)

**Confidence Level: Medium-High**
- Well-defined requirements with clear acceptance criteria
- Proven patterns (Clean Architecture, CQRS) reduce architectural risk
- External dependencies (OpenAI, MCP) are documented but preview status adds risk
- Clear rollback plan reduces implementation risk

**Dependencies External to Team:**
- OpenAI API key and access to Responses API
- Available MCP server for testing (weather, calculator, etc.)
- Network connectivity for external service calls during development