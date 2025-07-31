---
id: AXON-20250131-ArchitectureTests-SecurityEnhancement-ARCHITECTURE
title: Security Enhancement: Architecture
module: ArchitectureTests
feature: SecurityEnhancement
gate: G1
owner: system-designer&planner
status: draft
relates_to: []
source_of_truth: doc
created: 2025-01-31
updated: 2025-01-31
version: 1
---

# Context & Scope

## Problem Statement
Current architecture tests have 87 passing tests but lack critical API security validation. The ProcessMessageEndpoint example shows no authorization attributes, indicating security gaps in endpoint protection, authentication enforcement, rate limiting, content validation, HTTPS usage, and CORS configuration.

## Requirements Summary
Add 6 Security Enhancement tests to existing SecurityComplianceRules.cs:
1. **Controllers_ShouldHave_AuthorizationAttributes** - API endpoint protection
2. **SensitiveEndpoints_ShouldRequire_Authentication** - Auth validation on sensitive operations  
3. **APIs_ShouldHave_RateLimiting** - Abuse prevention through rate limiting
4. **APIs_ShouldValidate_ContentTypes** - Proper content type validation
5. **APIs_ShouldUse_HTTPS** - Secure transport enforcement
6. **CORS_ShouldBe_RestrictivelyConfigured** - Cross-origin attack prevention

## Success Criteria
- Maintain 87/87 test success rate (100% pass rate)
- Conservative static analysis approach (minimal false positives)
- Performance target: ~391ms total execution time maintained
- Seamless integration with existing SecurityComplianceRules.cs

# Boundaries & Dependencies

## Module Dependencies
```
ArchitectureTests
├── SecurityComplianceRules.cs (existing - 414 lines)
├── SecurityTestHelpers.cs (new extension)
└── ArchitectureTestHelpers.cs (existing - 198 lines)
```

## External Dependencies
- **NetArchTest.Rules** - Static analysis framework
- **System.Reflection** - Type and attribute analysis
- **ASP.NET Core types** - Controller, Authorization attributes
- **Microsoft.AspNetCore.Mvc** - HTTP method attributes

## Boundary Constraints
- **Read-only analysis** - No code modification, only validation
- **Static analysis only** - No runtime behavior testing
- **Assembly scope** - Analysis limited to loaded assemblies
- **Conservative detection** - Prefer false negatives over false positives

# Ports & Contracts

## SecurityTestHelpers Interface
```csharp
namespace Axon.ArchitectureTests;

public static class SecurityTestHelpers
{
    // Authorization Detection
    public static bool HasAuthorizationAttribute(Type controllerType);
    public static bool HasAuthorizationAttribute(MethodInfo method);
    public static string[] GetSensitiveEndpoints(Type[] controllerTypes);
    
    // Configuration Analysis  
    public static bool HasRateLimitingConfiguration(Assembly assembly);
    public static bool HasContentTypeValidation(MethodInfo method);
    public static bool HasHttpsRedirection(Assembly assembly);
    public static bool HasCorsConfiguration(Assembly assembly);
    
    // Pattern Detection
    public static bool IsApiController(Type type);
    public static bool IsHttpEndpoint(MethodInfo method);
    public static bool IsSensitiveOperation(MethodInfo method);
}
```

## Test Contracts
Each test follows NetArchTest pattern:
```csharp
[Test]
public void TestName_ShouldEnforce_SecurityRule()
{
    // 1. Discover relevant types/methods
    // 2. Apply security validation logic  
    // 3. Collect violations with descriptive messages
    // 4. Assert no violations with formatted error
}
```

## Detection Patterns
- **Authorization**: `[Authorize]`, `[AllowAnonymous]` attributes
- **HTTP Methods**: `[HttpPost]`, `[HttpGet]`, etc. attributes  
- **Rate Limiting**: Service registration and middleware patterns
- **Content Types**: `[Consumes]` attributes, request validation
- **HTTPS**: `UseHttpsRedirection()` calls in Program.cs
- **CORS**: `AddCors()`, `UseCors()` configuration

# CQRS Mapping

## Architecture Test Commands (Read-Only)
- **DiscoverControllersQuery** → Controller types from assemblies
- **AnalyzeAuthorizationQuery** → Authorization attribute presence  
- **ValidateConfigurationQuery** → Service/middleware configuration
- **DetectViolationsQuery** → Security rule violations
- **FormatResultCommand** → Test result with violation details

## Test Execution Flow
```
Assembly Discovery → Type Filtering → Attribute Analysis → 
Configuration Check → Violation Detection → Result Formatting
```

# Data Flow / Sequence

## Happy Path: All Security Rules Pass
```
1. Load assemblies from current domain
2. Filter to API controller types  
3. Analyze each controller and method for security attributes
4. Check Program.cs/ServiceRegistration for configuration
5. No violations found → Test passes
```

## Failure Path: Security Violations Detected
```
1. Load assemblies from current domain
2. Filter to API controller types
3. Analyze security attributes → Find missing [Authorize]
4. Collect violation: "ProcessMessageEndpoint.ProcessMessage"
5. Format error message with violations
6. Test fails with descriptive message
```

## Conservative Detection Strategy
- **Authorization**: Require explicit [Authorize] or [AllowAnonymous]
- **Sensitive Endpoints**: POST/PUT/DELETE operations by default
- **Rate Limiting**: Look for service registration patterns
- **Content Types**: Check for [Consumes] or model validation
- **HTTPS**: Detect UseHttpsRedirection() middleware
- **CORS**: Detect AddCors() configuration

# Transactions, Idempotency, Consistency

## Test Execution Model
- **No Transactions** - Read-only static analysis
- **Idempotent** - Multiple test runs produce same results
- **Consistent** - Deterministic assembly loading and analysis
- **Isolated** - Each test independent, no shared state

## Performance Characteristics
- **Assembly Caching** - Reuse loaded assemblies across tests
- **Reflection Optimization** - Cache type lookups
- **Parallel Safe** - No shared mutable state
- **Memory Efficient** - Conservative analysis without IL inspection

# Observability, Security, Performance

## Observability (ILogger, Activity, W3C)
- **TestContext.WriteLine()** - NUnit test output for debugging
- **Violation Counting** - Track security gaps for metrics
- **Performance Timing** - Monitor test execution duration
- **Coverage Reporting** - Log analyzed types and methods

## Security Considerations
- **Static Analysis Limitations** - Cannot detect runtime configurations
- **Assembly Trust** - Assumes loaded assemblies are trusted
- **Reflection Safety** - Use defensive null checks
- **Pattern Evolution** - Security patterns may change over time

## Performance Targets
- **Total Test Suite**: ~391ms (current baseline)
- **Per Security Test**: ~5-10ms each (6 tests = ~60ms)
- **Memory Usage**: Minimal impact through efficient reflection
- **Assembly Loading**: Reuse existing domain assemblies

# Compatibility & Migration

## Backward Compatibility
- **Existing Tests**: 87/87 success rate maintained
- **Test Framework**: Continue using NetArchTest.Rules
- **Helper Methods**: Extend ArchitectureTestHelpers, no breaking changes
- **File Structure**: Add to existing SecurityComplianceRules.cs

## Feature Flags & Rollout
- **No Feature Flags** - Architecture tests are development-time only
- **Gradual Enhancement** - Add tests incrementally to avoid disruption
- **Configuration Flexibility** - Allow custom sensitivity detection

## Migration Strategy
1. **Phase 1**: Add SecurityTestHelpers extension methods
2. **Phase 2**: Implement 3 core tests (Authorization, Authentication, HTTPS)
3. **Phase 3**: Add remaining tests (Rate Limiting, Content Types, CORS)
4. **Phase 4**: Validate all tests pass and performance maintained

# Alternatives Considered

## Alternative 1: Runtime Integration Tests
**Rejected** - Architecture tests should be static analysis only, runtime testing belongs in separate test suite.

## Alternative 2: External Security Scanning Tools
**Rejected** - Want security enforcement as part of build process, not external dependency.

## Alternative 3: Custom Attributes for Security Metadata
**Rejected** - Avoid introducing new attributes, use existing ASP.NET Core patterns.

## Alternative 4: IL Analysis for Deep Inspection
**Rejected** - Too complex for architecture tests, conservative static analysis sufficient.

# Risks & Mitigations

## Risk 1: False Positives (High Impact)
**Mitigation**: Conservative detection patterns, prefer false negatives, provide clear violation messages.

## Risk 2: Performance Degradation (Medium Impact)  
**Mitigation**: Efficient reflection usage, assembly caching, parallel-safe implementation.

## Risk 3: Framework Evolution (Medium Impact)
**Mitigation**: Use stable ASP.NET Core attribute patterns, minimal dependency on internal APIs.

## Risk 4: Configuration Detection Complexity (Medium Impact)
**Mitigation**: Focus on standard registration patterns, document limitations in test output.

## Risk 5: Test Brittleness (Low Impact)
**Mitigation**: Robust error handling, defensive programming, comprehensive unit test coverage.