---
id: AXON-20250730-Chat-MCP_Config_Migration-REQUIREMENTS
title: MCP Config Migration: Requirements
module: Chat
feature: MCP_Config_Migration
gate: G1
owner: <owner>
status: draft
relates_to: [AXON-20250729-Chat-Direct_MCP-TASK_PLAN]
source_of_truth: doc
created: 2025-07-30
updated: 2025-07-30
version: 1
---

# Problem
Currently, MCP (Model Context Protocol) server configuration is passed through API requests via the `McpServerRequest` field in `ProcessMessageRequest`. This approach has several issues:
- **Security**: API requests can expose sensitive headers/credentials in logs or traces
- **Usability**: Clients must provide MCP server configuration on every request
- **Maintainability**: No centralized configuration management for MCP servers
- **Scalability**: Cannot pre-configure trusted MCP servers or apply environment-specific settings

The system needs centralized MCP server configuration in `appsettings.json` while maintaining compatibility with the existing OpenAI Direct MCP integration pattern.

# Business Goal
Migrate MCP server configuration from API request payload to `appsettings.json` to improve security, usability, and maintainability. Users should be able to reference pre-configured MCP servers by name/ID rather than providing full configuration details in each API request.

**Success Measurement**: Zero MCP server credentials exposed in API requests while maintaining <2s response time and 100% backward compatibility during transition period.

# Acceptance Criteria

## AC1: Configuration Structure in appsettings.json
**Given** the application needs centralized MCP server configuration  
**When** appsettings.json is loaded  
**Then** it should support multiple MCP servers with this structure:
```json
{
  "Chat": {
    "OpenAi": {
      "McpEnabled": true
    },
    "McpServers": {
      "weather": {
        "Enabled": true,
        "ServerUrl": "https://weather.example.com/mcp",
        "ServerLabel": "Weather Service",
        "Headers": {
          "X-API-Key": "weather-api-key",
          "Authorization": "Bearer token123"
        },
        "AllowedTools": ["get_weather", "get_forecast"],
        "TimeoutSeconds": 30
      },
      "finance": {
        "Enabled": false,
        "ServerUrl": "https://finance.example.com/mcp",
        "AllowedTools": ["get_stock_price", "get_market_data"]
      }
    }
  }
}
```

## AC2: API Contract Simplification
**Given** MCP servers are pre-configured in appsettings.json  
**When** a client sends a ProcessMessageRequest  
**Then** the McpServer field should change from full configuration to simple server reference:
```csharp
// Before (remove)
public sealed record ProcessMessageRequest(
    string Message,
    McpServerRequest? McpServer = null,  // REMOVE
    string? ConversationId = null);

// After (new)
public sealed record ProcessMessageRequest(
    string Message,
    string? McpServerId = null,  // NEW: reference to pre-configured server
    string? ConversationId = null);
```

## AC3: Multiple Server Selection Support
**Given** multiple MCP servers are configured  
**When** a client specifies `McpServerId` in the request  
**Then** the system should:
- Use the specified pre-configured MCP server if enabled
- Return validation error if server ID doesn't exist
- Return service error if specified server is disabled
- Use no MCP server if `McpServerId` is null/empty

## AC4: Backward Compatibility During Transition
**Given** existing clients use the old `McpServerRequest` format  
**When** the migration is deployed  
**Then** the system should:
- Accept both old `McpServerRequest` and new `McpServerId` formats for 30 days
- Log deprecation warnings for old format usage
- Prioritize new format if both are provided
- Maintain identical functional behavior for both formats

## AC5: Security Improvements
**Given** MCP server credentials are stored in appsettings.json  
**When** API requests are processed  
**Then** sensitive data should:
- Never appear in HTTP request/response logs
- Never be exposed in API traces or metrics
- Be loaded from secure configuration (environment variables, Key Vault)
- Be validated at application startup for required fields

## AC6: Configuration Validation
**Given** MCP server configuration is loaded from appsettings.json  
**When** the application starts  
**Then** it should validate:
- Server URLs use HTTPS scheme
- Required fields are present for enabled servers
- AllowedTools arrays are not empty if specified
- TimeoutSeconds values are reasonable (1-300 seconds)
- Server IDs contain only alphanumeric characters and underscores

# Constraints

## Performance Targets
- **Response Time**: Maintain <2s response time for MCP-enabled requests
- **Startup Time**: Configuration validation must not add >500ms to startup
- **Memory Usage**: MCP server configurations should not exceed 1MB in memory

## Security Requirements
- **No Credentials in Logs**: MCP headers/tokens must not appear in any logs
- **HTTPS Only**: All MCP server URLs must use HTTPS scheme
- **Environment Isolation**: Different configurations per environment (dev/staging/prod)

## Compatibility Requirements
- **OpenAI Direct MCP**: Must maintain existing OpenAI Responses API integration
- **Result Pattern**: All new code must use Result<T> for error handling
- **CQRS Pattern**: Follow existing command/handler architecture
- **Dependency Rules**: Api → Application → Domain, no cross-module references

## Rollout Requirements
- **Feature Flag**: `Chat.McpConfigMigration.Enabled` to control new behavior
- **Gradual Migration**: Support both old and new formats during transition
- **Monitoring**: Track usage metrics for old vs new format adoption

# Non-Goals

- **Dynamic Server Registration**: Adding/removing MCP servers at runtime via API
- **Per-User MCP Configuration**: User-specific MCP server configurations
- **MCP Server Discovery**: Automatic discovery of available MCP servers
- **Configuration UI**: Admin interface for managing MCP server configurations
- **Server Health Monitoring**: Proactive health checks for configured MCP servers
- **Load Balancing**: Multiple instances of the same MCP server type

# Assumptions & Risks

## Assumptions
- **Configuration Deployment**: appsettings.json changes can be deployed independently of code
- **Environment Variables**: Secure credential storage is available (Azure Key Vault, env vars)
- **Client Updates**: API clients can be updated to use new McpServerId format
- **Server Stability**: Pre-configured MCP servers are relatively stable (URLs don't change frequently)

## Risks
- **Migration Complexity** (Medium): Supporting both old and new formats simultaneously
  - *Mitigation*: Use feature flags and comprehensive testing
- **Security Exposure** (Low): Configuration files might contain sensitive data
  - *Mitigation*: Use environment variables for secrets, clear documentation
- **Client Compatibility** (Medium): Existing clients need updates to use new format
  - *Mitigation*: 30-day backward compatibility period with deprecation warnings
- **Configuration Errors** (Low): Invalid appsettings.json could break MCP functionality
  - *Mitigation*: Startup validation with clear error messages

# Open Questions

1. **Should we support runtime configuration reload without restart?**
   - Impact on caching strategies and OpenAI client instances

2. **How should we handle MCP server authentication token renewal?**
   - Static tokens vs dynamic refresh mechanisms

3. **What's the preferred secure storage method for MCP credentials in different environments?**
   - Azure Key Vault for production, environment variables for development

4. **Should we validate MCP server connectivity at startup?**
   - Trade-off between startup reliability and startup time

5. **How should we handle configuration inheritance between environments?**
   - Base configuration with environment-specific overrides

6. **Should we support wildcard/regex patterns in AllowedTools configuration?**
   - e.g., "weather_*" to allow all weather-related tools