---
id: AXON-20250730-chat-processmessage-ARCHITECTURE
title: ProcessMessage: Architecture
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

# Context & Scope

## Current State Analysis
The ProcessMessageEndpoint currently violates SOLID principles and architectural boundaries:

**Issues Identified:**
1. **SRP Violation**: 106-line endpoint with multiple responsibilities (mapping, validation, error handling, response formatting)
2. **Architecture Misalignment**: Uses MVC Controller pattern instead of FastEndpoints REPR pattern
3. **Boundary Violations**: Direct error mapping logic embedded in presentation layer
4. **Inconsistent Patterns**: Mixes MVC patterns with Clean Architecture principles
5. **Maintenance Overhead**: Complex error handling logic duplicated across endpoints

**Current Dependencies:**
- MVC Controllers (`ControllerBase`)
- Direct MediatR integration
- Inline error mapping
- Manual problem details creation

## Refactor Scope
**Boundaries Affected:**
- `src/Api/Endpoints/Chat/ProcessMessageEndpoint.cs` (primary)
- `src/Api/Program.cs` (FastEndpoints registration)
- `src/Api/Configuration/ServiceRegistration.cs` (service registration)

**Invariants Preserved:**
- HTTP Contract: `POST /api/chat/process`
- Request/Response DTOs remain unchanged
- Application layer command/handler unchanged
- Security requirements maintained

# Boundaries & Dependencies

## Current Module Graph
```
Api (MVC Controllers) → Modules.Chat.Application → Modules.Chat.Domain
                    ↓
                Infrastructure
```

## Target Module Graph  
```
Api (FastEndpoints) → Modules.Chat.Application → Modules.Chat.Domain
                   ↓
               Infrastructure
```

**Key Changes:**
- Replace MVC Controllers with FastEndpoints REPR pattern
- Extract unified error handling as reusable component
- Maintain Clean Architecture boundaries (Api → Application only)

# Ports & Contracts

## Existing Interfaces (Preserved)
```csharp
// Application boundary - NO CHANGES
namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;
public sealed record ProcessMessageCommand(
    string Message,
    string? McpServerUrl = null,
    Dictionary<string, string>? McpHeaders = null,
    string[]? AllowedTools = null,
    string? PreviousResponseId = null) : IRequest<Result<ProcessMessageResponse>>;
```

## New Port Definitions
```csharp
// Unified error handling port
namespace Axon.Api.Common.ErrorHandling;
public interface IErrorMapper
{
    Task SendErrorAsync(HttpContext context, Error error, CancellationToken ct);
}

// FastEndpoints error mapping extension
namespace Axon.Api.Extensions;
public static class EndpointExtensions
{
    public static async Task SendResultAsync<T>(this IEndpoint endpoint, Result<T> result, CancellationToken ct);
    public static async Task SendResultAsync(this IEndpoint endpoint, Result result, CancellationToken ct);
}
```

## Contract Namespaces
- **Request/Response**: `Axon.Api.Contracts.Chat.*` (unchanged)
- **Error Handling**: `Axon.Api.Common.ErrorHandling.*` (new)
- **Extensions**: `Axon.Api.Extensions.*` (new)

# CQRS Mapping

## Command/Query Mapping (Unchanged)
```csharp
HTTP Request → ProcessMessageCommand → ProcessMessageHandler → Result<ProcessMessageResponse>
```

**Validation Flow:**
```csharp
FastEndpoints Validator → FluentValidation → ProcessMessageValidator (Application)
```

**Error Handling Flow:**
```csharp
Application Result<T> → IErrorMapper → HTTP Response
```

# Data Flow / Sequence

## Happy Path Sequence
```
1. POST /api/chat/process
2. FastEndpoints deserializes ProcessMessageRequest
3. Built-in validator validates request (if present)
4. Endpoint maps to ProcessMessageCommand
5. MediatR sends command to ProcessMessageHandler
6. Handler returns Result<ProcessMessageResponse>
7. Extension method maps Result to HTTP response
8. FastEndpoints serializes ProcessMessageResponse
```

## Failure Path Sequence
```
1. POST /api/chat/process (invalid data)
2. FastEndpoints validation fails → 400 Bad Request
   OR
1. Application returns Result.Failure
2. IErrorMapper maps Error to appropriate HTTP status
3. FastEndpoints returns error response
```

# Transactions, Idempotency, Consistency

**Transaction Scope**: No changes - maintained at Application layer
**Idempotency**: No changes - handled by business logic
**Consistency**: No changes - maintained by domain invariants

**Error Handling Consistency:**
- Unified error mapping across all endpoints
- Consistent HTTP status code mapping
- Standardized problem details format

# Observability, Security, Performance

## Observability
- **Logging**: ILogger integration maintained through MediatR behaviors
- **Activity**: W3C tracing preserved through FastEndpoints middleware
- **Metrics**: Endpoint-level metrics via FastEndpoints built-in support

## Security
- **Authentication**: Migrate from `[Authorize]` to FastEndpoints `Claims()`
- **Authorization**: Preserve existing security requirements
- **Input Validation**: Enhanced through FastEndpoints + FluentValidation

## Performance
- **Improvement Expected**: FastEndpoints performs better than MVC Controllers
- **Memory**: Reduced allocations through REPR pattern
- **Throughput**: Better request/response processing

# Compatibility & Migration

## Feature Flags
No feature flags required - direct replacement migration

## Rollout Strategy
1. **Parallel Implementation**: Create FastEndpoints version alongside MVC
2. **Route Migration**: Change routing to FastEndpoints
3. **Remove MVC**: Delete old MVC controller
4. **Validation**: Comprehensive testing at each step

## Breaking Changes
**None** - HTTP contract preserved completely

## Migration Shims
**Temporary Adapter** (if needed):
```csharp
// Bridge pattern during migration
public sealed class ProcessMessageAdapter : ControllerBase
{
    private readonly ProcessMessageEndpoint _fastEndpoint;
    
    [HttpPost("process")]
    public async Task<ActionResult> Process([FromBody] ProcessMessageRequest request, CancellationToken ct)
    {
        return await _fastEndpoint.HandleAsync(request, ct);
    }
}
```

# Alternatives Considered

## Option 1: Keep MVC Controllers (Rejected)
- **Pros**: No migration effort
- **Cons**: Continues architectural inconsistency, poor performance

## Option 2: Migrate to Minimal APIs (Rejected)  
- **Pros**: Built-in to .NET
- **Cons**: Less structured than FastEndpoints, harder to organize vertically

## Option 3: FastEndpoints REPR Pattern (Selected)
- **Pros**: Better performance, SOLID compliance, vertical slice alignment
- **Cons**: Learning curve, migration effort

# Risks & Mitigations

## Risk 1: Breaking Changes During Migration
**Likelihood**: Low
**Impact**: High
**Mitigation**: Comprehensive integration tests, HTTP contract validation

## Risk 2: Performance Regression
**Likelihood**: Very Low  
**Impact**: Medium
**Mitigation**: Load testing, performance benchmarks pre/post migration

## Risk 3: Security Model Changes
**Likelihood**: Low
**Impact**: High
**Mitigation**: Security review, authorization testing

## Risk 4: Team Learning Curve
**Likelihood**: Medium
**Impact**: Low
**Mitigation**: Documentation, pair programming, gradual rollout