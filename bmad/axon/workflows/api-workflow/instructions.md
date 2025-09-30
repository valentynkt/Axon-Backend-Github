# API Workflow - Module Enhancement Instructions

<workflow>

<critical>Governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>Loaded config: {project-root}/bmad/axon/workflows/api-workflow/workflow.yaml</critical>
<critical>EXTENDS story-implementation - inject at 4 strategic points, do NOT duplicate</critical>

## Overview

Enhances **story-implementation** with API/FastEndpoints expertise for REST endpoint development, REPR pattern, OpenAPI documentation, and comprehensive API testing in brownfield .NET + Clean Architecture + CQRS.

**Invocation**: `story-orchestrator` when `module = "API"`

**Core Patterns**: FastEndpoints REPR (Request-Endpoint-Response), REST conventions, Problem Details RFC 7807, FluentValidation, OpenAPI generation, JWT Bearer auth.

---

<step n="0" goal="Initialize API context">
<action>Load {installed_path}/workflow.yaml</action>
<action>Confirm module = "API"</action>
<action>Set API context flag for all agents</action>
</step>

---

## ENHANCEMENT POINT 1: API DOC LOADING

<step n="1" goal="Load API documentation (3 files)">
<action>Load all 3 API docs referenced in workflow.yaml:

**API Docs** (2 files - api_docs in workflow.yaml):
- api/00-INDEX.md, guides/codebase/coding-standards.md

**Library Docs** (1 file - api_library_docs in workflow.yaml):
- FastEndpoints/IMPLEMENTATION_GUIDE.md
</action>

<action>Parse workflow.yaml context:
- api_subdomains (4)
- rest_conventions
- fastendpoints_patterns
</action>

<critical>All 3 docs must be loaded before proceeding</critical>
</step>

<step n="2" goal="Classify to API subdomain">
<action>Analyze story, match to ONE primary subdomain:
1. **REST Endpoint Development** - FastEndpoints, REPR, routing
2. **Request/Response Contracts** - DTOs, validation, serialization
3. **API Documentation** - OpenAPI, Swagger, examples
4. **Error Handling** - Problem Details, status codes, Result<T> mapping
</action>

<action>Set subdomain context from workflow.yaml:
- {{api_subdomain}} (primary)
- {{api_patterns}} (key patterns)
</action>

<output section="subdomain_classification">
Subdomain: {{api_subdomain}}
Patterns: {{api_patterns}}
</output>
</step>

---

## ENHANCEMENT POINT 2: API PRE-FLIGHT VALIDATION

<step n="3" goal="API codebase discovery (@axon-archaeologist)">
<action>Invoke @axon-archaeologist:

**Search Strategy** (3 layers):
1. **Existing Endpoints**: Identity endpoints (src/Api/Endpoints/V1/Auth/), Chat endpoints (V1/Chat/) - pattern examples
2. **Contracts**: Request/Response DTOs (src/Api/Contracts/) - naming conventions
3. **Tests**: Endpoint tests (tests/Api/) - integration test patterns

**Reuse Focus**: Find similar endpoint patterns, existing validators, HTTP verb conventions, status code mappings.
</action>

<output section="discovery_report">
**Reuse Recommendations**:
- REUSE: [Endpoint patterns as-is]
- EXTEND: [Validators to extend]
- ADAPT: [Error handling patterns]
- CREATE: [New endpoints]
</output>
</step>

<step n="4" goal="API library validation (@axon-library-sage)">
<action>Invoke @axon-library-sage:

**Validate against API library stack** (2 libraries from workflow.yaml):
1. FastEndpoints 7.0.1 - REPR pattern, validation, auth, OpenAPI
2. FluentValidation - Request validation rules

**4-Factor Scoring**: Capability match, complexity reduction, maintenance burden, integration cost.
</action>

<output section="library_validation">
**Library Recommendations**:
- FastEndpoints: [REPR, routing, OpenAPI]
- FluentValidation: [Request validation]
- Manual: [Justification if any]
</output>
</step>

<step n="5" goal="API pattern validation (@axon-doc-oracle)">
<action>Invoke @axon-doc-oracle:

**Validate REST conventions + FastEndpoints patterns**:
1. **REST Conventions** (from workflow.yaml rest_conventions): Plural nouns, lowercase-hyphens, HTTP verbs, status codes
2. **REPR Pattern**: Sealed records (Request/Response), Endpoint<TRequest, TResponse>, Validator<T>
3. **Error Handling**: Problem Details RFC 7807, Result<T> → HTTP status mapping
4. **OpenAPI**: Summary(), Tags(), examples, auth schemes
5. **Authentication**: JWT Bearer, Claims(), Roles(), policies

**ADR Compliance**: ADR-006 (FastEndpoints)
</action>

<output section="pattern_validation">
**Compliance Score**: [95-100%]
**REST Conventions**: [100%]
**REPR Pattern**: [100%]
</output>
</step>

---

## ✅ CHECKPOINT 2: API PRE-FLIGHT APPROVAL

<step n="6" goal="Approve API pre-flight">
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 2: API PRE-FLIGHT APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

**Subdomain**: {{api_subdomain}}
**Reuse Score**: [High/Medium/Low]
**Libraries**: FastEndpoints, FluentValidation
**Compliance**: [95-100%]

Approve proceeding to implementation?
[c] Continue | [e] Edit | [a] Abort
</ask>
</step>

---

## ENHANCEMENT POINT 3: API IMPLEMENTATION GUIDANCE

<step n="7" goal="API-specific implementation guidance">
<action>Provide surgical implementation guidance based on subdomain:

**Endpoint Implementation** (from workflow.yaml fastendpoints_patterns):
- File location: src/Api/Endpoints/V1/{Module}/{Feature}/
- Pattern: Endpoint<TRequest, TResponse>
- Methods: Configure() + HandleAsync(TRequest, CancellationToken)
- Routing: Post/Get/Put/Patch/Delete("/api/v1/{resource}")
- Auth: Claims("UserID"), Roles("User"), policies
- OpenAPI: Summary(s => ...), Tags(...), examples

**Request/Response DTOs**:
- Location: src/Api/Contracts/{Module}/ (if shared) OR co-located with endpoint
- Pattern: Sealed record with init-only properties
- Example: `public sealed record CreateUserRequest { public string Email { get; init; } = ""; }`

**Validators**:
- Location: Co-located with endpoint (vertical slice)
- Pattern: Validator<TRequest> with FluentValidation rules
- Example: `public sealed class CreateUserValidator : Validator<CreateUserRequest> { }`

**Error Handling**:
- Result<T, Error> → HTTP status mapping (from workflow.yaml rest_conventions.status_codes)
- SendErrorsAsync() for validation errors
- AddError() for business rule violations
- Problem Details RFC 7807 format

**Integration with CQRS**:
- Inject IMediator in constructor
- Map Request → Command/Query
- Send via _mediator.Send(command, ct)
- Map Result<T> → Response or Errors
</action>

<output section="implementation_plan">
**Files to Create**:
1. {EndpointName}Endpoint.cs (REPR pattern)
2. {EndpointName}Validator.cs (FluentValidation)
3. {Request/Response}DTOs (if not co-located)
</output>
</step>

---

## ENHANCEMENT POINT 4: API VALIDATION

<step n="8" goal="API comprehensive testing (@axon-quality-guardian)">
<action>Invoke @axon-quality-guardian:

**3-Layer API Test Strategy**:
1. **Unit Tests**: Validator tests (FluentValidation rules)
2. **Integration Tests**: Endpoint tests (WebApplicationFactory, in-memory DB, IMediator mocking)
3. **Contract Tests**: OpenAPI schema validation (request/response contracts)

**API Test Scenarios** (from workflow.yaml success_metrics):
- Request validation (FluentValidation rules enforced)
- Authentication/authorization (401, 403 responses)
- Success responses (200, 201, 204 with correct DTOs)
- Error responses (400, 404, 422 with Problem Details)
- OpenAPI documentation (Swagger generation correct)
- HTTP verb semantics (idempotency, safety)
</action>

<output section="test_plan">
**Coverage Breakdown**: [Validator, Integration, Contract]
**AC Coverage**: 100%
**API Scenarios**: 6/6 (100%)
**Estimated Coverage**: 90%+
</output>
</step>

<step n="9" goal="Validate REST conventions">
<action>Validate all REST conventions from workflow.yaml:
1. **Resource Naming**: Plural nouns, lowercase-hyphens, no verbs
2. **HTTP Verbs**: Correct verb for operation (GET read, POST create, etc.)
3. **Status Codes**: Correct codes (200 OK, 201 Created, 422 Validation, 404 Not Found, etc.)
4. **Error Format**: Problem Details RFC 7807 (type, title, status, detail, traceId, errors)
5. **Versioning**: /api/v1/{resource} pattern

**Validation**: Code check + API test check for each convention.
</action>

<output section="convention_validation">
**Conventions Validated**: 5/5 (100%) ✅
**REST Compliance**: 100%
</output>
</step>

---

## COMPLETION

<step n="10" goal="API summary">
<output section="completion_summary">
**API Workflow Complete** ✅

**Module**: API/FastEndpoints
**Subdomain**: {{api_subdomain}}

**Deliverables**:
- ✓ 3 API docs loaded
- ✓ Subdomain classified
- ✓ Codebase discovery (endpoint patterns)
- ✓ Library validation (FastEndpoints, FluentValidation)
- ✓ Pattern compliance (REPR, REST conventions)
- ✓ REST conventions validated (5/5)
- ✓ Comprehensive test suite (3 layers)

**Quality Metrics**:
- Test Coverage: 90%+
- AC Coverage: 100%
- REST Conventions: 100%
- REPR Pattern: 100%
- Build: 100%
- Doc Sync: Zero drift
</output>

<critical>Append to base story-implementation summary</critical>
</step>

</workflow>