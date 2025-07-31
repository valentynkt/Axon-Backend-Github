---
id: AXON-20250131-Chat-ProcessMessage-POLICY_REPORT
title: ProcessMessage: Policy Report
module: Chat
feature: ProcessMessage
gate: G3
owner: valentyn
status: draft
relates_to: []
source_of_truth: doc
created: 2025-01-31
updated: 2025-01-31
version: 1
---

# Summary
- **Decision: FAIL**
- **Counts: blockers=2, warnings=1, advisory=2**
- **Top Blocker**: Security violation - sensitive data logging in error responses

# Architecture & Dependencies
- ✅ **PASS**: Clean Architecture boundaries respected
  - Api → Application → Domain flow maintained
  - Infrastructure properly implements Application ports
  - No forbidden cross-module references detected
  - DI composition correctly done at Api boundary

# CQRS / MediatR Shape
- ✅ **PASS**: CQRS pattern properly implemented
  - ProcessMessageCommand returns Result<ProcessMessageResponse>
  - Single handler (ProcessMessageHandler) with single responsibility
  - Proper validator (ProcessMessageValidator) in place
  - Api layer properly maps to contracts, never exposes Domain entities

# Result Pattern & Error Discipline
- ✅ **PASS**: Result pattern consistently applied
  - Business rule violations use typed Error.* instead of exceptions
  - Proper propagation through layers maintained
  - API correctly maps Result to HTTP codes via ErrorMapper
  - Exception handling follows proper patterns with specific error types

# Contracts & DTO Boundaries
- ✅ **PASS**: Contract boundaries properly maintained
  - No Domain entities exposed across API boundary
  - Proper mapping from AiResponse to ProcessMessageResponse
  - Contract fields are properly structured and versioned
  - Clear separation between internal and external DTOs

# Security / Secrets / PII
- ❌ **BLOCKER**: Sensitive data exposure in error logging
  - **Location**: `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs:167-169`
  - **Issue**: Full OpenAI API error response body logged, potentially containing sensitive data
  - **Fix**: Redact sensitive information from responseBody before logging:
    ```csharp
    // Before:
    _logger.LogError(
        "OpenAI Responses API returned error {StatusCode}: {ResponseBody}",
        response.StatusCode,
        responseBody);
    
    // After:
    _logger.LogError(
        "OpenAI Responses API returned error {StatusCode}",
        response.StatusCode);
    ```

- ❌ **BLOCKER**: Sensitive data exposure in exception messages
  - **Location**: `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs:171-172`
  - **Issue**: Full API response included in HttpRequestException message
  - **Fix**: Remove responseBody from exception message:
    ```csharp
    // Before:
    throw new HttpRequestException(
        $"OpenAI API returned {response.StatusCode}: {responseBody}");
    
    // After:
    throw new HttpRequestException(
        $"OpenAI API returned {response.StatusCode}");
    ```

- ⚠️ **WARNING**: MCP server URLs exposed in activity tags
  - **Location**: `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs:134`
  - **Issue**: Server URLs set as activity tags may be sensitive
  - **Fix**: Consider whether MCP server URLs should be tagged or redacted

# Observability
- ✅ **PASS**: Proper observability implementation
  - ILogger used appropriately with structured logging
  - Activity/tracing with W3C context propagation implemented
  - Correlation tracking via ResponseId maintained
  - Key identifiers included as attributes
  - No sensitive payloads in regular logging (except blocker above)

# Build & Analyzers
- ✅ **PASS**: Build and analyzer compliance
  - `dotnet build` produces zero warnings (confirmed)
  - No ad-hoc suppressions without justification
  - Nullable reference types properly handled
  - Appropriate analyzer suppressions with written justifications

# Performance & Reliability
- 📋 **ADVISORY**: HttpClient response not disposed
  - **Location**: `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs:161`
  - **Issue**: HttpResponseMessage not wrapped in using statement
  - **Fix**: Wrap response in using statement:
    ```csharp
    using var response = await _httpClient.PostAsync(OpenAiResponsesApiUrl, content, cancellationToken);
    ```

- 📋 **ADVISORY**: Potential memory pressure from large response bodies
  - **Location**: `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs:162`
  - **Issue**: Full response body read into string for error logging
  - **Fix**: Consider streaming or limiting error response size for large payloads

- ✅ **PASS**: Other performance patterns
  - CancellationToken properly flowed through async call chains
  - No sync-over-async patterns detected
  - Proper HttpClient configuration with timeout
  - Appropriate data access isolation in Infrastructure layer

# Required Actions

## Blockers to fix before merge:
- `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs:167-169` — Security: Remove responseBody from error logging — Replace with redacted logging
- `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs:171-172` — Security: Remove responseBody from exception message — Use status code only

## Warnings to resolve/justify:
- `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs:134` — Security: MCP server URLs in activity tags — Evaluate sensitivity and redact if needed

## Advisory improvements:
- `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs:161` — Performance: HttpResponseMessage disposal — Add using statement
- `src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs:162` — Performance: Large response body handling — Consider streaming approach

# Justifications (If any)
- CA1848 suppression — Structured logging with interpolation justified for readability
- AOT suppressions in ServiceRegistration — Configuration binding acceptable for this use case

# Additional Notes
The Direct MCP OpenAI implementation follows good architectural patterns overall. The main concern is security-related data exposure in error handling, which must be fixed before merge. The implementation correctly maintains clean architecture boundaries and proper error handling patterns throughout the system.