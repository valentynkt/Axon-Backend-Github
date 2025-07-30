---
id: AXON-20250730-Chat-MCP_Config_Migration-REVIEW_REPORT
title: MCP Config Migration: Review Report
module: Chat
feature: MCP_Config_Migration
gate: G3
owner: valentynkit
status: draft
relates_to: []
source_of_truth: doc
created: 2025-07-30
updated: 2025-07-30
version: 1
---

# Readability & Naming
- **McpServerResolver.GetEnabledServers()** → Consider renaming to `GetEnabledServerConfigurations()` to be more explicit about return type
- **ProcessMessageHandler.MapToApiResponse()** → Method name is clear but parameter could be `aiResponse` instead of `aiClientResponse` for brevity
- **Variable naming in McpServerResolver** → `serverId` and `serverOptions` are clear, but consider `configKey` and `serverConfig` to better reflect the configuration context

# Cohesion/Complexity
- **ProcessMessageHandler.Handle() method** → At 45 lines, this method handles multiple concerns (MCP resolution, AI processing, response mapping). Consider extracting:
  ```csharp
  private async Task<Result<AiRequest>> BuildAiRequestAsync(ProcessMessageCommand request, CancellationToken ct)
  ```
- **McpServerResolver.GetEnabledServers()** → The server filtering and transformation logic could be extracted into smaller methods:
  ```csharp
  private bool ShouldIncludeServer(McpServerOptions options) => options.Enabled;
  private McpServerConfig ToMcpServerConfig(string serverId, McpServerOptions options)
  ```

# Micro-Refactors (safe)
- **Early return pattern in McpServerResolver** → Replace continue with early validation:
  ```csharp
  // Before: if (!serverOptions.Enabled) continue;
  // After: if (!serverOptions.Enabled) { LogAndSkip(); continue; }
  ```
- **Null-conditional simplification in ProcessMessageHandler**:
  ```csharp
  // Before: enabledMcpServers.Count > 0 ? enabledMcpServers : null
  // After: enabledMcpServers.Count > 0 ? enabledMcpServers : []
  ```
- **Guard clause extraction in ProcessMessageEndpoint.HandleAsync()**:
  ```csharp
  private static void ValidateRequest(ProcessMessageRequest req)
  {
      ArgumentNullException.ThrowIfNull(req);
  }
  ```

# Maintainability Notes
- **Configuration validation** → The McpServerResolver assumes valid configuration. Consider adding validation for required fields (ServerUrl, etc.) at startup
- **Error correlation** → Both ProcessMessageHandler and ProcessMessageEndpoint log errors but without correlation IDs. Consider adding request tracking for debugging
- **Magic numbers** → The tool count logging uses `?? 0` pattern consistently, which is good for defensive programming
- **Future extensibility** → The IMcpServerResolver interface is well-designed for potential caching or dynamic configuration scenarios

# Ready for Policy?
yes — Code follows project patterns, uses Result<T> consistently, maintains clean architecture boundaries, and implements modern C# idioms correctly