# Comprehensive Test Plan: FastEndpoints Boundary Surgery Refactoring

## Executive Summary

This test plan covers the boundary surgery refactoring that introduced FastEndpoints alongside existing MVC endpoints for ProcessMessage functionality. The goal is to ensure ≥90% test coverage on new code while proving behavioral equivalence between both implementations.

## Scope of Changes

### Files Modified
- `/src/Api/Axon.Api.csproj` - Added FastEndpoints packages
- `/src/Api/Configuration/ServiceRegistration.cs` - Service registration updates
- `/src/Api/Program.cs` - Pipeline configuration changes
- `/src/Api/Common/ErrorHandling/` - New unified error handling infrastructure
- `/src/Api/Endpoints/Chat/ProcessMessage/` - New FastEndpoints implementation

### New Code Analysis
- **ProcessMessageEndpoint** (FastEndpoints): 91 lines, 8 methods/properties
- **ProcessMessageValidator**: 35 lines, 2 methods  
- **ErrorMapper**: 60 lines, 2 methods
- **IErrorMapper**: Interface with 2 method signatures

## Test Strategy

### 1. Behavioral Equivalence Tests (Critical)
**Purpose**: Prove both endpoints behave identically for all inputs

#### Test Categories:
- **Identity Tests**: Same inputs → same outputs
- **Error Parity Tests**: Same errors → same HTTP responses
- **Edge Case Consistency**: Boundary conditions handled identically
- **Performance Parity**: Response times within acceptable variance

### 2. FastEndpoints-Specific Tests
**Purpose**: Test FastEndpoints implementation details

#### Test Categories:
- **Validation Pipeline**: FastEndpoints validator behavior
- **Route Registration**: Endpoint discovery and routing
- **OpenAPI Documentation**: Swagger generation
- **Dependency Injection**: Service resolution in FastEndpoints context

### 3. Error Handling Infrastructure Tests
**Purpose**: Comprehensive testing of new ErrorMapper

#### Test Categories:
- **Status Code Mapping**: All ErrorType → HTTP status code mappings
- **ProblemDetails Generation**: RFC 7807 compliance
- **Error Message Sanitization**: InternalError handling
- **Null Safety**: Defensive programming validation

### 4. Integration Tests
**Purpose**: End-to-end functionality verification

#### Test Categories:
- **Middleware Coexistence**: Both frameworks in same pipeline
- **Service Registration**: No DI conflicts
- **Health Endpoints**: Existing functionality preserved
- **Swagger Documentation**: Unified API documentation

### 5. Chaos & Resilience Tests
**Purpose**: Robustness under adverse conditions

#### Test Categories:
- **Timeout Scenarios**: Request timeout handling
- **Rate Limiting**: 429 response behavior
- **Resource Exhaustion**: Memory/CPU pressure tests
- **Network Failures**: Service dependency failures

## Detailed Test Cases

### A. Behavioral Equivalence Test Suite

#### A1. Response Identity Tests
```csharp
[Test]
public async Task BothEndpoints_ShouldReturnIdenticalResponses_GivenSameValidRequest()
[Test]
public async Task BothEndpoints_ShouldHandleToolExecutions_Identically()
[Test]
public async Task BothEndpoints_ShouldGenerateConversationIds_Identically()
```

#### A2. Error Response Parity Tests
```csharp
[Test]
public async Task BothEndpoints_ShouldReturnIdenticalValidationErrors()
[Test]
public async Task BothEndpoints_ShouldReturnIdenticalNotFoundErrors()
[Test]
public async Task BothEndpoints_ShouldReturnIdenticalExternalServiceErrors()
[Test]
public async Task BothEndpoints_ShouldReturnIdenticalInternalErrors()
```

### B. FastEndpoints Implementation Tests

#### B1. Endpoint Configuration Tests
```csharp
[Test]
public void Configure_ShouldSetCorrectRoute()
[Test]
public void Configure_ShouldAllowAnonymousAccess()
[Test]
public void Configure_ShouldHaveCorrectTags()
[Test]
public void Configure_ShouldHaveProperOpenApiDocumentation()
```

#### B2. Request Handling Tests
```csharp
[Test]
public async Task HandleAsync_ShouldThrowArgumentNullException_GivenNullRequest()
[Test]
public async Task HandleAsync_ShouldUseErrorMapper_ForFailureResults()
[Test]
public async Task HandleAsync_ShouldWriteJsonResponse_ForSuccessResults()
```

#### B3. Validation Tests
```csharp
[Test]
public void ProcessMessageValidator_ShouldRejectEmptyMessage()
[Test]
public void ProcessMessageValidator_ShouldRejectTooLongMessage()
[Test]
public void ProcessMessageValidator_ShouldValidateUrlFormat()
[Test]
public void ProcessMessageValidator_ShouldAllowValidRequests()
```

### C. Error Handling Infrastructure Tests

#### C1. ErrorMapper Status Code Tests
```csharp
[Test]
[TestCase(ErrorType.Validation, 400)]
[TestCase(ErrorType.NotFound, 404)]
[TestCase(ErrorType.Conflict, 409)]
[TestCase(ErrorType.ExternalService, 502)]
[TestCase(ErrorType.Unauthorized, 401)]
[TestCase(ErrorType.Forbidden, 403)]
[TestCase(ErrorType.InternalError, 500)]
public void MapToStatusCode_ShouldReturnCorrectStatusCode(ErrorType errorType, int expectedStatusCode)
```

#### C2. ProblemDetails Generation Tests
```csharp
[Test]
public void MapToProblemDetails_ShouldCreateValidProblemDetails()
[Test]
public void MapToProblemDetails_ShouldSanitizeInternalErrorMessages()
[Test]
public void MapToProblemDetails_ShouldThrowForNullError()
```

### D. Integration Test Suite

#### D1. Service Registration Tests
```csharp
[Test]
public void ServiceRegistration_ShouldRegisterBothMvcAndFastEndpoints()
[Test]
public void ServiceRegistration_ShouldRegisterErrorMapperCorrectly()
[Test]
public void ServiceRegistration_ShouldNotHaveDependencyConflicts()
```

#### D2. Pipeline Integration Tests
```csharp
[Test]
public async Task Pipeline_ShouldHandleBothEndpointTypes()
[Test]
public async Task Pipeline_ShouldPreserveMvcFunctionality()
[Test]
public async Task Pipeline_ShouldUnifySwaggerDocumentation()
```

### E. Chaos & Resilience Test Suite

#### E1. Timeout Tests
```csharp
[Test]
public async Task BothEndpoints_ShouldHandleTimeouts_Gracefully()
[Test]
public async Task BothEndpoints_ShouldPropagateCanellationTokens()
```

#### E2. Resource Pressure Tests
```csharp
[Test]
public async Task BothEndpoints_ShouldHandleHighConcurrency()
[Test]
public async Task BothEndpoints_ShouldHandleMemoryPressure()
```

## Coverage Requirements

### Primary Coverage Targets (≥90%)
- **ProcessMessageEndpoint** (FastEndpoints): All methods and decision branches
- **ProcessMessageValidator**: All validation rules and conditions
- **ErrorMapper**: All error type mappings and edge cases
- **Service Registration**: All new registrations and configurations

### Secondary Coverage Targets (≥80%)
- **Program.cs** changes: Middleware registration and ordering
- **Integration paths**: End-to-end request flows

## Test Infrastructure

### Test Fixtures
```csharp
public class BehavioralEquivalenceTestFixture
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    
    // Deterministic test data generators
    // Mock configurations for IAiClient
    // Response comparison utilities
}
```

### Test Data Builders
```csharp
public class ProcessMessageRequestBuilder
public class AiResponseBuilder  
public class ErrorBuilder
```

### Comparison Utilities
```csharp
public static class ResponseComparer
{
    public static void AssertResponsesEquivalent(HttpResponseMessage mvc, HttpResponseMessage fastEndpoints)
    public static void AssertErrorResponsesEquivalent(string mvcContent, string fastEndpointsContent)
}
```

## Determinism Enforcement

### Time-Based Dependencies
- Use `IDateTimeProvider` for any time-related operations
- Freeze time in tests using `FakeTimeProvider`

### Random Values
- Replace `Guid.NewGuid()` with deterministic GUID generation in tests
- Use fixed seed for any random operations

### External Dependencies
- Mock `IMediator` with predictable responses
- Mock `IAiClient` with deterministic tool executions
- Use `TestServer` instead of real HTTP clients

## Risk Assessment

### High-Risk Areas Requiring Extra Testing
1. **Route Conflicts**: Ensure `/api/chat/process` and `/api/chat/process-v2` don't interfere
2. **Middleware Ordering**: FastEndpoints before MVC could affect processing
3. **Error Handling Consistency**: Different error approaches might diverge
4. **Service Resolution**: DI container behavior with two frameworks
5. **Memory Leaks**: Dual framework overhead impact

### Medium-Risk Areas
1. **Performance Regression**: Two framework stacks running concurrently
2. **Documentation Drift**: Swagger generation for both endpoint types
3. **Validation Differences**: FastEndpoints vs MVC validation pipelines

## Success Criteria

### Functional Requirements
- [ ] All existing tests continue to pass
- [ ] New tests achieve ≥90% coverage on changed files
- [ ] Behavioral equivalence proven through automated testing
- [ ] No performance regression >10% in P95 response times

### Quality Requirements
- [ ] Zero flaky tests across 10 consecutive runs
- [ ] All tests use deterministic fixtures and mocking
- [ ] Comprehensive error scenario coverage
- [ ] Clear test failure diagnostics and logging

### Documentation Requirements
- [ ] Test plan covers all identified risk areas
- [ ] Test cases include clear arrange-act-assert structure
- [ ] Performance benchmarks established for both endpoints
- [ ] Runbook for identifying behavioral divergence

## Execution Plan

### Phase 1: Foundation (Days 1-2)
1. Create test infrastructure and fixtures
2. Implement behavioral equivalence test framework
3. Build deterministic test data generators

### Phase 2: Core Testing (Days 3-4)
1. Implement all behavioral equivalence tests
2. Create FastEndpoints-specific test suite
3. Build comprehensive error handling tests

### Phase 3: Integration & Chaos (Days 5-6)
1. Implement integration test suite
2. Create chaos and resilience tests
3. Performance benchmark establishment

### Phase 4: Validation & Documentation (Day 7)
1. Execute full test suite multiple times
2. Measure and document coverage metrics
3. Create test maintenance documentation

## Appendix: Test Execution Commands

```bash
# Run all tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run behavioral equivalence tests only
dotnet test --filter "Category=BehavioralEquivalence"

# Run FastEndpoints-specific tests
dotnet test --filter "Category=FastEndpoints"

# Run chaos tests  
dotnet test --filter "Category=Chaos"

# Generate coverage reports
dotnet tool run reportgenerator -reports:**/coverage.cobertura.xml -targetdir:TestResults/CoverageReport
```