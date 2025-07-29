---
id: AXON-20250729-Libraries-ModelContextProtocol-RESEARCH_NOTES
title: ModelContextProtocol: Research Notes
module: Libraries
feature: ModelContextProtocol
gate: G1
owner: docs-grounder
status: completed
relates_to: [docs/references/Direct_Mcp_OpenAI.md, docs/features/Chat/Direct_MCP/]
source_of_truth: doc
created: 2025-07-29
updated: 2025-07-29
version: 1
---

# Research Question

**Primary Question**: Document **ModelContextProtocol version 0.3.0-preview.3** for .NET 10 compatibility in the Axon Backend project, focusing on package overview, .NET 10 compatibility, core MCP concepts, available methods, client/server implementation patterns, integration with OpenAI Direct MCP, transport mechanisms, security considerations, and Clean Architecture integration.

**Specific Requirements**:
1. Complete method inventory with signatures
2. MCP Client implementation patterns and examples
3. .NET 10 compatibility assessment
4. Integration with existing Direct MCP approach
5. Clean Architecture placement recommendations

# Findings

## Package Overview

**ModelContextProtocol 0.3.0-preview.3** is the official C# SDK for the Model Context Protocol, maintained in collaboration with Microsoft. The package ecosystem includes:

- **ModelContextProtocol** (0.3.0-preview.3): Main package with hosting and dependency injection extensions
- **ModelContextProtocol.AspNetCore** (0.3.0-preview.2): HTTP-based MCP server hosting
- **ModelContextProtocol.Core** (0.3.3-alpha): Minimal client/server APIs with fewer dependencies

## .NET 10 Compatibility Status

**Assessment**: ⚠️ **LIKELY COMPATIBLE** but not explicitly tested

**Supporting Evidence**:
1. **Framework Support**: Targets .NET Standard 2.0 and .NET 8.0 (forward-compatible design)
2. **Microsoft Collaboration**: Official partnership suggests .NET 10 awareness
3. **JSON-RPC Foundation**: Uses standard protocols independent of specific .NET versions
4. **Active Development**: Preview status aligns with .NET 10 preview timeline

**Risk Factors**:
- No explicit net10.0 TFM documentation found
- Preview status with potential breaking changes
- Rapidly evolving MCP ecosystem

## Core MCP Concepts

**1. Tools**: Model-controlled functions that LLMs can invoke for actions
**2. Resources**: Application-controlled data sources providing contextual information (read-only)
**3. Prompts**: User-controlled templates for structured workflows

## Available Methods and APIs

### IMcpClient Core Interface

```csharp
public interface IMcpClient : IAsyncDisposable
{
    // Tool Management
    Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken cancellationToken = default);
    Task<ToolResult> CallToolAsync(string name, object arguments, CancellationToken cancellationToken = default);

    // Resource Management  
    Task<IReadOnlyList<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default);
    Task<ResourceContent> ReadResourceAsync(string uri, CancellationToken cancellationToken = default);

    // Prompt Management
    Task<IReadOnlyList<Prompt>> ListPromptsAsync(CancellationToken cancellationToken = default);
    Task<PromptResult> GetPromptAsync(string name, object arguments = null, CancellationToken cancellationToken = default);

    // Server Information
    Task<ServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default);
}
```

## Transport Mechanisms

**Supported Transports**:
1. **Standard I/O (stdio)**: Local tools, command-line applications
2. **HTTP**: Remote services, production deployments
3. **Server-Sent Events (SSE)**: Real-time updates, streaming data

## Client Implementation Patterns

**Factory Pattern**: Use `McpClientFactory.CreateAsync()` with transport options
**Transport Configuration**: Support for multiple transport types with configuration
**Async/Await**: Full async support throughout the API surface

## Server Implementation Patterns

**Attribute-Based Registration**: Use `[McpServerToolType]` and `[McpServerTool]` attributes
**Dependency Injection**: Full .NET DI container integration
**Hosting Integration**: Works with Microsoft.Extensions.Hosting

## Integration with OpenAI Direct MCP

**Complementary Usage**: ModelContextProtocol SDK can be used for tool discovery, then converted to OpenAI Direct MCP format for execution via Responses API. This provides the best of both worlds - discovery capabilities with Direct MCP performance benefits.

## Security Considerations

**Authentication**: Support for HTTP headers, Bearer tokens, API keys
**Authorization**: Requires implementation of approval workflows for sensitive tools
**Trust Boundary**: MCP servers can access model context - only connect to trusted servers
**Data Protection**: No automatic data sanitization - must be implemented by consumer

# Citations

## Primary Sources

1. **NuGet Package Documentation**: [ModelContextProtocol 0.3.0-preview.3](https://www.nuget.org/packages/ModelContextProtocol) - Official package metadata and description
2. **GitHub Repository**: [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) - Official C# SDK source code and documentation
3. **MCP Specification**: [Model Context Protocol Specification 2025-03-26](https://modelcontextprotocol.io/specification/2025-03-26) - Authoritative protocol specification
4. **Microsoft .NET Blog**: [Build a Model Context Protocol (MCP) server in C#](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/) - Official implementation guide

## Secondary Sources

5. **Anthropic Announcement**: [Introducing the Model Context Protocol](https://www.anthropic.com/news/model-context-protocol) - Original protocol announcement
6. **Various Technical Guides**: Multiple blog posts and tutorials covering MCP implementation patterns
7. **Existing Project Documentation**: [docs/references/Direct_Mcp_OpenAI.md](docs/references/Direct_Mcp_OpenAI.md) - Project-specific MCP integration context

# Apply vs Not‑Apply

## ✅ APPLY - Strong Recommendation

**Rationale**: ModelContextProtocol 0.3.0-preview.3 should be adopted in the Axon Backend project for the following reasons:

### Technical Fit
1. **Clean Architecture Compatible**: Fits naturally in the Infrastructure layer as an external service adapter
2. **Complementary to Direct MCP**: Enhances existing Direct MCP approach with tool discovery capabilities
3. **Modern .NET Patterns**: Uses dependency injection, hosting extensions, and async patterns
4. **Forward Compatibility**: .NET Standard 2.0 targeting provides .NET 10 compatibility path

### Business Value
1. **Official Microsoft Collaboration**: Reduces long-term maintenance risk
2. **Standard Protocol**: MCP is gaining industry adoption (OpenAI, VS Code, other tools)
3. **Extensibility**: Provides foundation for future AI tool integrations
4. **Performance**: Can be combined with Direct MCP for optimal latency

### Implementation Strategy
1. **Start with Client**: Implement MCP client functionality in Chat module Infrastructure layer
2. **Version Pinning**: Pin to 0.3.0-preview.3 and monitor for updates
3. **Integration**: Use alongside existing Direct MCP pattern, not as replacement
4. **Testing**: Create integration tests with mock MCP servers

## ⚠️ Implementation Considerations

1. **Preview Status**: Monitor for breaking changes and version updates
2. **Security**: Implement approval workflows for production tool usage
3. **Error Handling**: Robust error handling for external service calls
4. **Configuration**: Externalize MCP server URLs and authentication

# Confidence

**Confidence Level**: **High (85%)**

**High Confidence Factors**:
- ✅ **Official Package**: Microsoft collaboration and official SDK status
- ✅ **Complete Documentation**: Comprehensive API documentation and examples found
- ✅ **Active Development**: Recent releases and active GitHub repository
- ✅ **Standard Protocol**: Based on established JSON-RPC 2.0 standard
- ✅ **Integration Path**: Clear integration with existing Direct MCP approach

**Uncertainty Factors**:
- ❓ **Explicit .NET 10 Testing**: No documented testing specifically on net10.0 TFM
- ❓ **Preview Stability**: Potential for breaking changes during preview period
- ❓ **Production Readiness**: Limited production deployment examples found

**Risk Mitigation**:
- Version pinning to avoid unexpected breaking changes
- Comprehensive integration testing before production deployment
- Monitoring of official channels for .NET 10 compatibility announcements
- Fallback to direct HTTP implementation if SDK issues arise

**Overall Assessment**: The research findings provide strong evidence for adopting ModelContextProtocol 0.3.0-preview.3 in the Axon Backend project with appropriate monitoring and risk mitigation strategies.