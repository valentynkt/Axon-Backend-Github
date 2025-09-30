# API Workflow - FastEndpoints REST Development

**Tier 3 Module Enhancement** | Extends `story-implementation`

Specialized workflow for REST API endpoint development with FastEndpoints 7.0, REPR pattern implementation, OpenAPI documentation, and comprehensive API testing.

---

## 🎯 Purpose

Enhances brownfield .NET development with API-first expertise:
- **FastEndpoints REPR Pattern** - Request-Endpoint-Response architecture
- **REST Conventions** - Resource naming, HTTP verbs, status codes
- **OpenAPI Documentation** - Swagger generation, examples, auth schemes
- **Error Handling** - Problem Details RFC 7807, Result<T> mapping
- **API Testing** - Integration tests, contract tests, validator tests

---

## 📋 When to Use

**Invoked by** `@axon-story-orchestrator` when:
```yaml
Story Type: Feature
Module Context: API
Examples:
  - "Create new conversation endpoint"
  - "Add wallet verification API"
  - "Implement user profile endpoint"
  - "Add pagination to conversations list"
```

---

## 🏗️ Subdomain Classification

**4 API Subdomains:**

1. **REST Endpoint Development**
   - FastEndpoints configuration, routing, HTTP verbs
   - Vertical slice architecture (endpoint + validator co-located)
   - Authentication (JWT Bearer, claims, roles, policies)

2. **Request/Response Contracts**
   - DTO design with sealed records, init-only properties
   - FluentValidation integration for request validation
   - Serialization, binding, model validation

3. **API Documentation**
   - OpenAPI/Swagger generation
   - Endpoint summaries, tags, examples
   - Authentication schemes documentation

4. **Error Handling**
   - Problem Details RFC 7807 format
   - Result<T, Error> → HTTP status code mapping
   - Validation errors, business rule violations

---

## 🔄 Enhancement Strategy

**Extends `story-implementation` at 4 strategic points:**

```yaml
Base Workflow (story-implementation):
  Phase 0: Story Understanding
    → Enhancement Point 1: Load API docs (3 files)
  Phase 1: Pre-Flight Validation
    → Enhancement Point 2: API-specific discovery + validation
  Phase 2: Implementation
    → Enhancement Point 3: FastEndpoints + REPR guidance
  Phase 3: Validation
    → Enhancement Point 4: API testing (3 layers)
```

**NO duplication** - Inherits all 4 phases, 6 agents, quality gates.

---

## 📚 Documentation Loaded

**Progressive Loading (3 files):**

```yaml
API Docs (2 files):
  - Docs/ENGINEERING/api/00-INDEX.md              # REST conventions, status codes
  - Docs/ENGINEERING/guides/codebase/coding-standards.md  # C# standards

Library Docs (1 file):
  - Docs/Libraries/FastEndpoints/IMPLEMENTATION_GUIDE.md  # REPR pattern, auth
```

**Related Docs (inherited from base):**
- FluentValidation library guide (request validation)
- Pattern guides (Result<T>, error handling)
- Module-specific docs (if endpoint touches Identity/Chat)

---

## 🔍 Pre-Flight Validation

**3-Layer Discovery:**

1. **@axon-archaeologist** - Find existing endpoints
   - Identity endpoints: `src/Api/Endpoints/V1/Auth/`
   - Chat endpoints: `src/Api/Endpoints/V1/Chat/`
   - Reuse patterns: Validators, error mappings, auth configs

2. **@axon-library-sage** - Validate libraries
   - FastEndpoints 7.0.1 capabilities
   - FluentValidation integration
   - Out-of-box features vs manual code

3. **@axon-doc-oracle** - Pattern compliance
   - REST conventions (5 dimensions)
   - REPR pattern correctness
   - Problem Details format
   - ADR-006 (FastEndpoints)

---

## ⚙️ Implementation Patterns

### REPR Pattern (Request-Endpoint-Response)

**File Structure (Vertical Slice):**
```
src/Api/Endpoints/V1/{Module}/{Feature}/
├── {Feature}Endpoint.cs        # Endpoint<TRequest, TResponse>
├── {Feature}Validator.cs       # Validator<TRequest>
└── {Request/Response}.cs       # Sealed records (if complex)
```

**Endpoint Template:**
```csharp
// Request DTO
public sealed record CreateUserRequest
{
    public string Email { get; init; } = "";
    public string? Name { get; init; }
}

// Response DTO
public sealed record CreateUserResponse
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = "";
}

// Validator
public sealed class CreateUserValidator : Validator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

// Endpoint
public sealed class CreateUserEndpoint : Endpoint<CreateUserRequest, CreateUserResponse>
{
    private readonly IMediator _mediator;
    
    public CreateUserEndpoint(IMediator mediator) => _mediator = mediator;
    
    public override void Configure()
    {
        Post("/api/v1/users");
        Claims("UserID");
        
        Summary(s =>
        {
            s.Summary = "Create a new user";
            s.Response<CreateUserResponse>(201, "User created");
            s.Response(422, "Validation error");
        });
        
        Tags("Users", "Identity");
    }
    
    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        var command = new CreateUserCommand(req.Email, req.Name);
        var result = await _mediator.Send(command, ct);
        
        if (result.IsFailure)
        {
            await SendErrorsAsync(ct);
            return;
        }
        
        await SendCreatedAtAsync<GetUserEndpoint>(
            new { userId = result.Value.Id },
            new CreateUserResponse { UserId = result.Value.Id, Email = result.Value.Email },
            cancellation: ct
        );
    }
}
```

### REST Conventions

**Resource Naming:**
- ✅ Plural nouns: `/users`, `/conversations` (not `/user`)
- ✅ Lowercase-hyphens: `/wallet-ownerships`
- ✅ No verbs: Use HTTP verbs (not `/createUser`)

**HTTP Verbs:**
- GET - Read (idempotent, safe)
- POST - Create (non-idempotent)
- PUT - Replace (idempotent)
- PATCH - Partial update
- DELETE - Delete (idempotent)

**Status Codes:**
- 200 OK, 201 Created, 204 No Content
- 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 422 Unprocessable Entity
- 500 Internal Server Error

### Error Handling

**Problem Details RFC 7807:**
```csharp
// Validation error
{
  "type": "https://axon.dev/errors/validation",
  "title": "Validation Error",
  "status": 422,
  "errors": {
    "email": ["Email is required"]
  }
}

// Business rule violation
{
  "type": "https://axon.dev/errors/business-rule",
  "title": "Business Rule Violation",
  "status": 422,
  "detail": "Cannot exceed 10 wallet ownerships per principal"
}
```

**Result<T> Mapping:**
```csharp
Error.Validation() → 422 Unprocessable Entity
Error.NotFound() → 404 Not Found
Error.Conflict() → 409 Conflict
Error.BusinessRule() → 422 Unprocessable Entity
Error.Unauthorized() → 401 Unauthorized
Error.Forbidden() → 403 Forbidden
```

---

## ✅ Success Criteria

**Inherited from story-implementation (5 base gates):**
1. Pattern Compliance ≥ 95%
2. Test Coverage ≥ 90%
3. AC Coverage = 100%
4. Build Success = 100%
5. Doc Sync = Zero Drift

**API-Specific Gates (6 additional):**
1. **REST Conventions**: 100% (naming, verbs, status codes)
2. **REPR Pattern**: 100% (sealed records, Endpoint<TRequest, TResponse>, Validator<T>)
3. **OpenAPI Documentation**: 100% (summaries, tags, examples, auth)
4. **Error Handling**: 100% (Problem Details RFC 7807)
5. **Authentication**: 100% (JWT Bearer, claims, roles)
6. **API Test Scenarios**: 6/6 passing

---

## 🧪 Testing Strategy

**3-Layer API Testing:**

1. **Validator Tests** (Unit)
   - FluentValidation rules enforced
   - Edge cases (empty, null, invalid formats)
   - Custom validation logic

2. **Integration Tests** (WebApplicationFactory)
   - Endpoint routing correct
   - Request/response contracts validated
   - Authentication/authorization enforced
   - Result<T> → HTTP status mapping correct

3. **Contract Tests** (OpenAPI)
   - Swagger schema generation correct
   - Request/response examples valid
   - Authentication schemes documented

**Test Scenarios:**
- Request validation (422 for invalid input)
- Authentication (401 for missing token)
- Authorization (403 for insufficient permissions)
- Success responses (200/201 with correct DTOs)
- Error responses (Problem Details format)
- HTTP verb semantics (idempotency, safety)

---

## 📊 Metrics

**Estimated Duration:** +30 minutes on top of story-implementation (40-70 min)

**Deliverables:**
- 1-3 FastEndpoints endpoints
- 1-3 FluentValidation validators
- 1-3 Request/Response DTOs
- Integration tests (WebApplicationFactory)
- OpenAPI documentation (auto-generated)

**Quality Targets:**
- REPR Pattern Compliance: 100%
- REST Conventions: 100%
- Test Coverage: 90%+
- OpenAPI Completeness: 100%

---

## 🚀 Usage Example

**Story:** "Create endpoint for listing user conversations with pagination"

**Routing Decision:**
```yaml
Story Type: Feature
Module Context: API (touches Chat module)
Workflow Selected: api-workflow + chat-workflow
```

**Workflow Execution:**
1. Load API docs (3 files) + Chat docs (5 files)
2. Classify: REST Endpoint Development (primary), Conversation Management (secondary)
3. Discover: Existing chat endpoints, pagination patterns
4. Validate: FastEndpoints, FluentValidation, pagination conventions
5. Implement: GetConversationsEndpoint with pagination
6. Test: Integration tests, contract tests, pagination scenarios

**Output:**
- `GetConversationsEndpoint.cs` (REPR pattern)
- `GetConversationsValidator.cs` (page, pageSize validation)
- `GetConversationsResponse.cs` (pagination metadata)
- Integration tests (3 scenarios)
- OpenAPI documentation (auto-generated)

---

## 🔗 Related Workflows

**Tier 1 (Master):**
- `story-orchestrator` - Routes to this workflow

**Tier 2 (Base):**
- `story-implementation` - Extended by this workflow

**Tier 3 (Siblings):**
- `identity-workflow` - For Identity module endpoints
- `chat-workflow` - For Chat module endpoints

**Tier 4 (Support):**
- `pre-flight-validation` - Reusable validation logic
- `doc-sync` - Documentation maintenance

---

## 📖 Configuration

**Location:** `bmad/axon/workflows/api-workflow/workflow.yaml`

**Key Variables:**
- `api_docs`: 2 API documentation files
- `api_library_docs`: FastEndpoints guide
- `api_subdomains`: 4 subdomain classifications
- `rest_conventions`: Resource naming, HTTP verbs, status codes
- `fastendpoints_patterns`: REPR pattern, auth, OpenAPI

**Invocation:**
```bash
# Automatic (via story-orchestrator)
@axon-story-orchestrator implement-story {story-id}

# Manual (for testing)
Load: bmad/axon/workflows/api-workflow/instructions.md
```

---

## 🛠️ Troubleshooting

**Issue:** Endpoint not appearing in Swagger UI
- Check: `Summary()` configured, not `ExcludeFromDescription`
- Check: Endpoint discovered by FastEndpoints (check startup logs)

**Issue:** Validation not enforced
- Check: Validator<TRequest> class exists and has rules
- Check: FluentValidation registered in DI container

**Issue:** 401 Unauthorized for authenticated requests
- Check: `Claims()` or `Roles()` configured in Configure()
- Check: JWT token valid (not expired, correct issuer)

**Issue:** Wrong status code returned
- Check: Result<T> error type mapping
- Check: SendErrorsAsync() vs SendOkAsync() usage

---

**Version:** 1.0.0  
**Last Updated:** 2025-09-30  
**Status:** ✅ Production-Ready (BMM Token-Efficient Pattern)