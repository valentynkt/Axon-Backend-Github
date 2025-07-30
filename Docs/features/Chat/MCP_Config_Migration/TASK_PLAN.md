---
id: AXON-20250730-Chat-MCP_Config_Migration-TASK_PLAN
title: MCP Config Migration: Task Plan
module: Chat
feature: MCP_Config_Migration
gate: G1
owner: <owner>
status: draft
relates_to: [AXON-20250730-Chat-MCP_Config_Migration-ARCHITECTURE]
source_of_truth: doc
created: 2025-07-30
updated: 2025-07-30
version: 1
---

# Plan Summary

Migrate MCP server configuration from API request payloads to centralized `appsettings.json` configuration with backward compatibility support. Implementation follows Domain → Application → Infrastructure → Api dependency order with feature flag controls.

**Key Changes**:
- Add MCP server configuration options in Infrastructure layer
- Introduce MCP server resolver service in Application layer  
- Update API contracts to support both legacy and new formats
- Add comprehensive validation and observability

**Backward Compatibility**: 30-day transition period supporting both `McpServerRequest` (legacy) and `McpServerId` (new) formats simultaneously.

## Tasks (Domain → Application → Infrastructure → Api)

### T1: Domain Layer Updates
**Why**: No domain changes required - MCP configuration is infrastructure concern

**Steps**: None required

**Files to touch**: None

### T2: Application Layer - Core Abstractions & Services
**Why**: Add MCP server resolution abstraction and update command/handler to support both formats

**Steps**:
1. Create IMcpServerResolver interface for configuration resolution
2. Update ProcessMessageCommand to include McpServerId field
3. Update ProcessMessageHandler to use resolver service
4. Maintain backward compatibility with existing MCP fields

**Files to touch**:
- `src/Modules/Chat/Application/Abstractions/IMcpServerResolver.cs` (NEW)
- `src/Modules/Chat/Application/Commands/ProcessMessage/ProcessMessageCommand.cs` (MODIFY)
- `src/Modules/Chat/Application/Commands/ProcessMessage/ProcessMessageHandler.cs` (MODIFY)

### T3: Infrastructure Layer - Configuration & Implementation  
**Why**: Implement MCP server configuration options, resolver service, and wire up dependency injection

**Steps**:
1. Create McpServersOptions configuration classes with validation
2. Implement McpServerResolver service with both format support
3. Add configuration extensions for DI registration
4. Update OpenAiClient to work with new configuration model
5. Add startup configuration validation

**Files to touch**:
- `src/Modules/Chat/Infrastructure/Configuration/McpServersOptions.cs` (NEW)
- `src/Modules/Chat/Infrastructure/Configuration/McpServerOptions.cs` (NEW)
- `src/Modules/Chat/Infrastructure/Services/McpServerResolver.cs` (NEW)
- `src/Modules/Chat/Infrastructure/Extensions/ServiceCollectionExtensions.cs` (MODIFY)
- `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs` (MODIFY - minimal changes)

### T4: Api Layer - Contract Updates & Validation
**Why**: Update API contracts to support new McpServerId field while maintaining backward compatibility

**Steps**:
1. Update ProcessMessageRequest to include McpServerId field
2. Update ProcessMessageValidator for new field validation
3. Update endpoint mapping to pass new field to command
4. Ensure both legacy and new formats are handled properly

**Files to touch**:
- `src/Api/Contracts/Chat/ProcessMessageRequest.cs` (MODIFY)
- `src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageValidator.cs` (MODIFY)
- `src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageEndpoint.cs` (MODIFY)

### T5: Configuration & Startup
**Why**: Add sample configuration and ensure proper validation at application startup

**Steps**:
1. Update appsettings.json with MCP servers section and feature flags
2. Add environment-specific configuration examples
3. Ensure configuration validation runs at startup

**Files to touch**:
- `src/Api/appsettings.json` (MODIFY)
- `src/Api/appsettings.Development.json` (MODIFY)

### T6: Testing Updates
**Why**: Ensure comprehensive test coverage for both legacy and new configuration formats

**Steps**:
1. Update existing tests to cover new McpServerId field
2. Add tests for McpServerResolver with various scenarios
3. Add configuration validation tests
4. Add backward compatibility test scenarios

**Files to touch**:
- `tests/Api.Tests/Endpoints/Chat/ProcessMessage/ProcessMessageEndpointTests.cs` (MODIFY)
- `tests/Api.Tests/Endpoints/Chat/ProcessMessage/ProcessMessageValidator.Tests.cs` (MODIFY)
- `tests/Modules.Chat.Application.Tests/Commands/ProcessMessage/ProcessMessageHandlerTests.cs` (MODIFY)
- `tests/Modules.Chat.Infrastructure.Tests/Services/McpServerResolverTests.cs` (NEW)
- `tests/Modules.Chat.Infrastructure.Tests/Configuration/McpServersOptionsTests.cs` (NEW)

## Milestones & Criteria

### M1: Infrastructure Foundation Complete
**Criteria**:
- McpServersOptions classes created with proper validation
- McpServerResolver service implemented and registered
- Configuration loading and validation working at startup
- All Infrastructure layer tests passing

### M2: Application Layer Integration Complete  
**Criteria**:
- ProcessMessageCommand updated with McpServerId field
- ProcessMessageHandler using resolver service
- Both legacy and new formats supported in handler
- All Application layer tests passing

### M3: API Surface Updated
**Criteria**:
- ProcessMessageRequest supports both McpServerId and legacy McpServer fields
- Request validation handles both formats correctly
- Endpoint properly maps new field to command
- All API layer tests passing

### M4: End-to-End Integration Working
**Criteria**:
- Sample configuration in appsettings.json works
- Full request flow works with new McpServerId format
- Legacy format still works with deprecation warnings
- Integration tests passing for both formats

### M5: Production Ready
**Criteria**:
- All tests passing (unit, integration, behavioral equivalence)
- Performance benchmarks show <2s response time maintained
- Security review passed (no credentials in logs)
- Documentation updated with migration guide

## Rollback Plan

### Immediate Rollback (if critical issues found)
1. **Disable Feature Flag**: Set `Chat.McpConfigMigration.Enabled = false` in appsettings.json
2. **Force Legacy Mode**: Set `Chat.McpConfigMigration.AllowLegacyFormat = true`
3. **Monitor**: Check logs for errors, verify legacy format works
4. **Client Communication**: Notify clients to continue using McpServerRequest format

### Code Rollback (if feature flag insufficient)
1. **Revert Git Commits**: 
   ```bash
   git revert HEAD~6..HEAD  # Revert last 6 commits from this feature
   git push origin main
   ```

2. **Remove New Configuration**: Delete McpServers section from appsettings.json

3. **Restore Dependencies**: 
   - Remove IMcpServerResolver from DI container
   - Ensure ProcessMessageHandler uses old CreateMcpConfigurationFromRequest method

4. **Database/State Cleanup**: None required (configuration-only changes)

### Partial Rollback (rollback specific components)
1. **Api Layer Only**: Revert ProcessMessageRequest changes, keep infrastructure changes
2. **Infrastructure Only**: Disable resolver service, fallback to legacy creation method
3. **Configuration Only**: Remove McpServers config, disable feature flags

## Effort Estimate

**Size: M (Medium)**

**Breakdown**:
- **T1 (Domain)**: 0 hours - no changes needed
- **T2 (Application)**: 4 hours - interface, command update, handler modification
- **T3 (Infrastructure)**: 8 hours - configuration classes, resolver service, DI setup
- **T4 (Api)**: 3 hours - contract updates, validation, endpoint changes  
- **T5 (Configuration)**: 1 hour - appsettings.json updates
- **T6 (Testing)**: 6 hours - comprehensive test coverage for both formats

**Total Estimate**: 22 hours (3 days)

**Risk Buffer**: +30% for backward compatibility complexity = 29 hours total

**Dependencies**: 
- No external team dependencies
- No database schema changes required
- Configuration deployment can be done independently

**Parallelization Opportunities**:
- T2 and T3 can be developed in parallel after interfaces are defined
- T6 testing can start as soon as T2-T4 are complete
- Configuration examples (T5) can be prepared while implementation is in progress