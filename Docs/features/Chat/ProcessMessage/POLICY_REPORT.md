---
id: AXON-20250730-Chat-ProcessMessage-POLICY_REPORT
title: ProcessMessage: Policy Report
module: Chat
feature: ProcessMessage
gate: G3
owner: policy-enforcer
status: draft
relates_to: []
source_of_truth: doc
created: 2025-07-30
updated: 2025-07-30
version: 1
---

# Summary
- **Decision: FAIL**
- **Counts: blockers=8, warnings=2, advisory=1**
- **Top Blocker**: Build fails with 8 compilation errors due to contract mismatch

# Architecture & Dependencies
- Findings:
  - [PASS] Clean Architecture boundaries respected - Api → Application via MediatR
  - [PASS] No cross-module references detected
  - [PASS] Proper DI composition at Api boundary
  - [PASS] Domain remains persistence-agnostic

# CQRS / MediatR Shape
- Findings:
  - [PASS] Commands return `Result<T>` pattern maintained
  - [PASS] Single handler per command/query
  - [WARNING] Validator exists but validation logic conflicts with contract definition
  - [PASS] MediatR behaviors order is appropriate

# Result Pattern & Error Discipline
- Findings:
  - [PASS] Business rule violations use typed `Error.*` pattern
  - [PASS] Proper propagation through layers maintained
  - [PASS] API maps to HTTP codes at boundary via ErrorMapper
  - [PASS] No exception swallowing detected

# Contracts & DTO Boundaries
- Findings:
  - [BLOCKER] Contract mismatch: ProcessMessageRequest contract defines (Message, McpServerId?, ConversationId?) but endpoint/validator reference non-existent McpServer property — src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageEndpoint.cs:38,61-63
  - [BLOCKER] ProcessMessageValidator references undefined McpServer property — src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageValidator.cs:27,30,34
  - [PASS] No Domain entities exposed across API boundary
  - [PASS] Contract fields are nullable-safe

# Security / Secrets / PII
- Findings:
  - [PASS] No hardcoded secrets detected
  - [PASS] Input validation framework in place
  - [PASS] No sensitive data in logs

# Observability
- Findings:
  - [WARNING] Missing structured logging in ProcessMessageEndpoint - no ILogger injection
  - [ADVISORY] Consider adding Activity/tracing context for end-to-end correlation

# Build & Analyzers
- Findings:
  - [BLOCKER] Build fails with 8 compilation errors:
    - CS1739: ProcessMessageRequest constructor parameter 'McpServer' does not exist
    - CS1061: ProcessMessageRequest missing 'McpServer' property (6 occurrences)
  - [BLOCKER] Cannot assess warnings until compilation errors are resolved

# Performance & Reliability
- Findings:
  - [PASS] Async/await patterns correctly implemented
  - [PASS] CancellationToken flows through call chain
  - [PASS] No sync-over-async detected

# Required Actions

## Blockers to fix before merge:
1. **Contract Definition Mismatch**:
   - File: src/Api/Contracts/Chat/ProcessMessageRequest.cs
   - Issue: Missing McpServer property that endpoint/validator expect
   - Fix: Add McpServer property or remove references to it
   
2. **Endpoint Implementation Error**:
   - File: src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageEndpoint.cs:38,61-63
   - Issue: References req.McpServer property that doesn't exist
   - Fix: Use req.McpServerId or update contract to include McpServer
   
3. **Validator Implementation Error**:
   - File: src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageValidator.cs:27,30,34
   - Issue: References x.McpServer property that doesn't exist
   - Fix: Update validation logic to match actual contract

## Warnings to resolve/justify:
1. **Missing Logging**:
   - File: src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageEndpoint.cs
   - Issue: No ILogger for observability
   - Fix: Inject ILogger<ProcessMessageEndpoint> and add structured logging

2. **Validation Logic Consistency**:
   - File: src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageValidator.cs
   - Issue: Validator logic assumes different contract shape
   - Fix: Align validator with actual ProcessMessageRequest contract

# Justifications (If any)
None provided. All violations must be addressed.

---

## Specific Fix Recommendations

### 1. Contract Alignment (Choose One Approach)

**Option A: Update Contract to Match Implementation**
```csharp
public sealed record ProcessMessageRequest(
    string Message,
    string? McpServerId = null,
    McpServerConfig? McpServer = null,
    string? ConversationId = null);

public sealed record McpServerConfig(
    string ServerUrl,
    Dictionary<string, string>? Headers = null,
    string[]? AllowedTools = null);
```

**Option B: Update Implementation to Match Contract**
```csharp
// In ProcessMessageEndpoint.cs - remove McpServer references
var command = new ProcessMessageCommand(
    Message: req.Message,
    McpServerId: req.McpServerId,
    McpServerUrl: null, // Remove
    McpHeaders: null,   // Remove
    AllowedTools: null, // Remove
    PreviousResponseId: req.ConversationId);
```

### 2. Add Observability
```csharp
public ProcessMessageEndpoint(IMediator mediator, IErrorMapper errorMapper, ILogger<ProcessMessageEndpoint> logger)
{
    _mediator = mediator;
    _errorMapper = errorMapper;
    _logger = logger;
}
```

**POLICY DECISION: FAIL** - Cannot proceed with 8 blocking compilation errors and contract inconsistencies.