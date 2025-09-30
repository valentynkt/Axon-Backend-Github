# API Workflow - Validation Checklist

**Comprehensive validation for API/FastEndpoints module enhancement**

Use this checklist after api-workflow execution to validate REST endpoint implementation, REPR pattern compliance, OpenAPI documentation, and API testing.

---

## ✅ PHASE 0: API CONTEXT INITIALIZATION

### API Documentation Loaded
- [ ] **API Docs Loaded (2 files)**
  - [ ] `Docs/ENGINEERING/api/00-INDEX.md` (REST conventions, status codes, versioning)
  - [ ] `Docs/ENGINEERING/guides/codebase/coding-standards.md` (C# coding standards)

- [ ] **Library Docs Loaded (1 file)**
  - [ ] `Docs/Libraries/FastEndpoints/IMPLEMENTATION_GUIDE.md` (REPR pattern, validation, auth)

- [ ] **Subdomain Classified**
  - [ ] Story classified to ONE of 4 API subdomains:
    - [ ] REST Endpoint Development
    - [ ] Request/Response Contracts
    - [ ] API Documentation
    - [ ] Error Handling

---

## ✅ PHASE 1: API PRE-FLIGHT VALIDATION

### Codebase Discovery (@axon-archaeologist)
- [ ] **Existing Endpoints Discovered**
  - [ ] Searched Identity endpoints (`src/Api/Endpoints/V1/Auth/`)
  - [ ] Searched Chat endpoints (`src/Api/Endpoints/V1/Chat/`)
  - [ ] Similar endpoint patterns identified
  - [ ] Reuse recommendations documented (REUSE/EXTEND/ADAPT/CREATE)

- [ ] **Validator Patterns Discovered**
  - [ ] Existing FluentValidation validators found
  - [ ] Validation rule patterns identified
  - [ ] Custom validator extensions cataloged

- [ ] **Error Handling Patterns Discovered**
  - [ ] Result<T> → HTTP status mappings found
  - [ ] Problem Details format usage identified
  - [ ] SendErrorsAsync() patterns cataloged

### Library Validation (@axon-library-sage)
- [ ] **FastEndpoints 7.0.1 Validated**
  - [ ] REPR pattern capabilities confirmed
  - [ ] Authentication/authorization features verified
  - [ ] OpenAPI generation capabilities confirmed
  - [ ] Vertical slice architecture supported

- [ ] **FluentValidation Integration Validated**
  - [ ] Request validation capabilities confirmed
  - [ ] Custom validation rule support verified
  - [ ] Validator<T> pattern usage validated

- [ ] **4-Factor Scoring Complete**
  - [ ] Capability match assessed
  - [ ] Complexity reduction calculated
  - [ ] Maintenance burden evaluated
  - [ ] Integration cost estimated

### Pattern Validation (@axon-doc-oracle)
- [ ] **REST Conventions Validated (5 dimensions)**
  - [ ] Resource naming: Plural nouns, lowercase-hyphens, no verbs
  - [ ] HTTP verbs: Correct verb for operation semantics
  - [ ] Status codes: 200/201/204 (success), 400/401/403/404/422 (client), 500 (server)
  - [ ] Versioning: `/api/v1/{resource}` pattern
  - [ ] Error format: Problem Details RFC 7807

- [ ] **REPR Pattern Validated**
  - [ ] Request DTO: Sealed record with init-only properties
  - [ ] Response DTO: Sealed record with computed properties
  - [ ] Endpoint: Endpoint<TRequest, TResponse> with Configure() and HandleAsync()
  - [ ] Validator: Validator<TRequest> with FluentValidation rules

- [ ] **OpenAPI Documentation Validated**
  - [ ] Summary() configured for endpoint
  - [ ] Tags() applied for organization
  - [ ] Response examples defined
  - [ ] Authentication schemes documented

- [ ] **ADR Compliance Validated**
  - [ ] ADR-006 (FastEndpoints) - REPR pattern, vertical slice architecture

---

## ✅ CHECKPOINT 2: API PRE-FLIGHT APPROVAL

- [ ] **Pre-Flight Summary Reviewed**
  - [ ] Subdomain classification confirmed
  - [ ] Reuse score acceptable (High/Medium)
  - [ ] Library recommendations approved
  - [ ] Pattern compliance score ≥ 95%
  - [ ] User approved proceeding to implementation

---

## ✅ PHASE 2: API IMPLEMENTATION

### Endpoint Implementation
- [ ] **File Structure (Vertical Slice)**
  - [ ] Endpoint file created: `{Feature}Endpoint.cs` in `src/Api/Endpoints/V1/{Module}/{Feature}/`
  - [ ] Validator file created: `{Feature}Validator.cs` (co-located)
  - [ ] Request/Response DTOs created (if not co-located)

- [ ] **Request DTO Implementation**
  - [ ] Sealed record declared
  - [ ] Init-only properties used
  - [ ] Default values provided for strings
  - [ ] Nullable reference types used correctly
  - [ ] XML documentation comments added

- [ ] **Response DTO Implementation**
  - [ ] Sealed record declared
  - [ ] Init-only properties used
  - [ ] Computed properties if needed
  - [ ] XML documentation comments added

- [ ] **Validator Implementation**
  - [ ] Validator<TRequest> inherited
  - [ ] FluentValidation rules defined
  - [ ] Custom validation logic if needed
  - [ ] Error messages clear and actionable
  - [ ] Edge cases covered (empty, null, invalid formats)

- [ ] **Endpoint Implementation**
  - [ ] Endpoint<TRequest, TResponse> inherited
  - [ ] IMediator injected in constructor
  - [ ] Configure() method implemented:
    - [ ] HTTP verb configured (Post/Get/Put/Patch/Delete)
    - [ ] Route configured (`/api/v1/{resource}`)
    - [ ] Authentication configured (Claims/Roles/AllowAnonymous)
    - [ ] Summary() configured (summary, description, responses)
    - [ ] Tags() applied
  - [ ] HandleAsync() method implemented:
    - [ ] Request mapped to Command/Query
    - [ ] Command/Query sent via _mediator.Send()
    - [ ] Result<T> handling (IsFailure check)
    - [ ] Errors sent via SendErrorsAsync() if failure
    - [ ] Response sent via SendOkAsync/SendCreatedAtAsync if success
    - [ ] CancellationToken passed through

### REST Conventions Applied
- [ ] **Resource Naming**
  - [ ] Plural nouns used (/users, /conversations)
  - [ ] Lowercase with hyphens (/wallet-ownerships)
  - [ ] No verbs in URLs

- [ ] **HTTP Verb Semantics**
  - [ ] GET: Read operation (idempotent, safe)
  - [ ] POST: Create operation (non-idempotent)
  - [ ] PUT: Replace operation (idempotent)
  - [ ] PATCH: Partial update
  - [ ] DELETE: Delete operation (idempotent)

- [ ] **Status Codes**
  - [ ] Success: 200 OK, 201 Created, 204 No Content
  - [ ] Client errors: 400, 401, 403, 404, 422
  - [ ] Server errors: 500

### Error Handling Implementation
- [ ] **Problem Details RFC 7807 Format**
  - [ ] Error responses include: type, title, status, detail, traceId
  - [ ] Validation errors include: errors dictionary (field → messages[])
  - [ ] Error type catalog followed (validation, not-found, business-rule, conflict, etc.)

- [ ] **Result<T> Mapping**
  - [ ] Error.Validation() → 422 Unprocessable Entity
  - [ ] Error.NotFound() → 404 Not Found
  - [ ] Error.Conflict() → 409 Conflict
  - [ ] Error.BusinessRule() → 422 Unprocessable Entity
  - [ ] Error.Unauthorized() → 401 Unauthorized
  - [ ] Error.Forbidden() → 403 Forbidden

### OpenAPI Documentation
- [ ] **Endpoint Summary**
  - [ ] Summary text provided
  - [ ] Description text provided (if complex)
  - [ ] Response examples defined (200, 201, 400, 422, etc.)

- [ ] **Swagger Generation**
  - [ ] Endpoint appears in Swagger UI
  - [ ] Request schema correct
  - [ ] Response schema correct
  - [ ] Authentication scheme visible (JWT Bearer)

---

## ✅ CHECKPOINT 3: API IMPLEMENTATION REVIEW

- [ ] **Implementation Preview Reviewed**
  - [ ] REPR pattern followed
  - [ ] REST conventions applied
  - [ ] Error handling correct
  - [ ] OpenAPI documentation complete
  - [ ] User approved changes

---

## ✅ PHASE 3: API VALIDATION & TESTING

### Validator Tests (Unit)
- [ ] **FluentValidation Rules Tested**
  - [ ] Required fields validated
  - [ ] Format validation tested (email, URL, etc.)
  - [ ] Length constraints tested (min, max)
  - [ ] Custom validation logic tested
  - [ ] Edge cases tested (empty, null, invalid)

- [ ] **Test Coverage**
  - [ ] All validation rules have tests
  - [ ] Positive cases tested (valid input)
  - [ ] Negative cases tested (invalid input)
  - [ ] Error messages verified

### Integration Tests (WebApplicationFactory)
- [ ] **Endpoint Routing Tested**
  - [ ] Endpoint reachable at correct route
  - [ ] HTTP verb correct
  - [ ] Route parameters bound correctly

- [ ] **Authentication/Authorization Tested**
  - [ ] 401 Unauthorized for missing token
  - [ ] 403 Forbidden for insufficient permissions
  - [ ] Authenticated requests succeed

- [ ] **Request/Response Contracts Tested**
  - [ ] Request DTO deserialized correctly
  - [ ] Response DTO serialized correctly
  - [ ] Content-Type headers correct (application/json)

- [ ] **Success Scenarios Tested**
  - [ ] 200 OK for GET requests (with response body)
  - [ ] 201 Created for POST requests (with Location header if applicable)
  - [ ] 204 No Content for DELETE requests

- [ ] **Error Scenarios Tested**
  - [ ] 400 Bad Request for malformed JSON
  - [ ] 422 Unprocessable Entity for validation errors (Problem Details format)
  - [ ] 404 Not Found for non-existent resources
  - [ ] 409 Conflict for concurrency conflicts

- [ ] **Result<T> Mapping Tested**
  - [ ] Domain errors mapped to correct HTTP status codes
  - [ ] Error messages preserved in Problem Details
  - [ ] TraceId included in error responses

### Contract Tests (OpenAPI)
- [ ] **OpenAPI Schema Validated**
  - [ ] Swagger JSON generation successful
  - [ ] Request schema matches DTO
  - [ ] Response schema matches DTO
  - [ ] Authentication scheme documented

- [ ] **API Documentation Validated**
  - [ ] Endpoint summary visible in Swagger UI
  - [ ] Tags applied correctly
  - [ ] Response examples accurate

### API Test Scenarios (6/6)
- [ ] **1. Request Validation**
  - [ ] Invalid input returns 422 with validation errors
  - [ ] Error messages clear and actionable

- [ ] **2. Authentication**
  - [ ] Missing token returns 401 Unauthorized
  - [ ] Invalid token returns 401 Unauthorized
  - [ ] Expired token returns 401 Unauthorized

- [ ] **3. Authorization**
  - [ ] Insufficient permissions returns 403 Forbidden
  - [ ] Correct permissions allow access

- [ ] **4. Success Responses**
  - [ ] Correct status code (200/201/204)
  - [ ] Response DTO correct structure
  - [ ] Response data accurate

- [ ] **5. Error Responses**
  - [ ] Problem Details RFC 7807 format
  - [ ] Correct status codes (400/404/422/409)
  - [ ] TraceId included

- [ ] **6. HTTP Verb Semantics**
  - [ ] Idempotent operations (GET, PUT, DELETE) can be retried safely
  - [ ] Safe operations (GET) have no side effects

### Build & Quality Gates
- [ ] **Build Success**
  - [ ] `dotnet build` passes with zero warnings
  - [ ] Warnings-as-errors enforced
  - [ ] No nullable reference warnings

- [ ] **Test Execution**
  - [ ] All tests pass (validator, integration, contract)
  - [ ] Test coverage ≥ 90%
  - [ ] AC coverage = 100%

### Documentation Sync
- [ ] **API Documentation Updated**
  - [ ] OpenAPI spec regenerated (automatic)
  - [ ] Endpoint examples added if needed
  - [ ] Integration docs updated if needed

- [ ] **Code Documentation**
  - [ ] XML comments on Request/Response DTOs
  - [ ] XML comments on Endpoint class
  - [ ] Summary comments on public methods

---

## ✅ CHECKPOINT 4: FINAL API COMMIT APPROVAL

- [ ] **Final Validation Complete**
  - [ ] All 11 quality gates passed (5 base + 6 API-specific)
  - [ ] REST conventions: 100%
  - [ ] REPR pattern: 100%
  - [ ] OpenAPI documentation: 100%
  - [ ] Error handling: 100%
  - [ ] Authentication: 100%
  - [ ] API test scenarios: 6/6
  - [ ] Test coverage: 90%+
  - [ ] AC coverage: 100%
  - [ ] Build success: 100%
  - [ ] Doc sync: Zero drift

- [ ] **Commit Message Prepared**
  - [ ] Includes: module (API), subdomain, story ID
  - [ ] Includes: quality metrics summary
  - [ ] Includes: files changed summary

- [ ] **User Approved Final Commit**

---

## 📊 API-SPECIFIC SUCCESS METRICS

### REST Conventions (5/5 = 100%)
- [ ] Resource naming: Plural nouns, lowercase-hyphens, no verbs
- [ ] HTTP verbs: Correct semantics (idempotency, safety)
- [ ] Status codes: Correct mapping (2xx, 4xx, 5xx)
- [ ] Versioning: `/api/v1/{resource}` pattern
- [ ] Error format: Problem Details RFC 7807

### REPR Pattern (100%)
- [ ] Request: Sealed record, init-only properties
- [ ] Response: Sealed record, computed properties
- [ ] Endpoint: Endpoint<TRequest, TResponse>, Configure(), HandleAsync()
- [ ] Validator: Validator<TRequest>, FluentValidation rules

### OpenAPI Documentation (100%)
- [ ] Summary() configured
- [ ] Tags() applied
- [ ] Response examples defined
- [ ] Authentication schemes documented
- [ ] Swagger UI generation successful

### Error Handling (100%)
- [ ] Problem Details RFC 7807 format
- [ ] Result<T> → HTTP status mapping correct
- [ ] Validation errors formatted correctly
- [ ] TraceId included in responses

### Authentication (100%)
- [ ] JWT Bearer configured
- [ ] Claims() or Roles() applied
- [ ] 401/403 responses correct
- [ ] Authentication schemes documented

### API Test Scenarios (6/6 = 100%)
- [ ] Request validation tested
- [ ] Authentication tested
- [ ] Authorization tested
- [ ] Success responses tested
- [ ] Error responses tested
- [ ] HTTP verb semantics tested

---

## 🎯 COMPLETION CRITERIA

**API Workflow Complete When:**
- ✅ All Phase 0-3 checkboxes checked
- ✅ All 4 checkpoints approved by user
- ✅ All 11 quality gates passed (5 base + 6 API-specific)
- ✅ REST conventions: 100%
- ✅ REPR pattern: 100%
- ✅ OpenAPI documentation: 100%
- ✅ API test scenarios: 6/6
- ✅ Test coverage: 90%+
- ✅ Build success: 100%
- ✅ Git commit created with quality metrics

---

**Version:** 1.0.0  
**Last Updated:** 2025-09-30  
**Checklist Items:** 150+  
**Estimated Validation Time:** 10-15 minutes