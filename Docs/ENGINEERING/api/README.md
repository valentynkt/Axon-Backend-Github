# API Documentation

**REST API standards, conventions, and OpenAPI specification.**

---

**STATUS**: 🚧 Scaffold - Needs Content
**PRIORITY**: Medium
**LAST_UPDATED**: 2025-01-29

---

## 🎯 API Design Principles

### Core Principles
- **Resource-oriented design** - Endpoints represent resources, not actions
- **RESTful conventions** - Standard HTTP verbs with consistent semantics
- **Stateless** - No server-side sessions; bearer token for authentication
- **Idempotent operations** - Safe retry semantics where appropriate
- **Versioned** - URL-based versioning (`/api/v1/`)

---

## 📋 REST Conventions

### Resource Naming
- **Plural nouns** - `/users`, `/conversations`, `/messages` (not `/user`, `/conversation`)
- **Lowercase with hyphens** - `/wallet-ownerships` (not `/WalletOwnerships`)
- **Nested resources** - `/conversations/{id}/messages` for sub-resources
- **No verbs in URLs** - Use HTTP verbs instead (not `/createUser`, `/deleteMessage`)

### HTTP Verb Usage

| Verb | Purpose | Idempotent | Safe | Example |
|------|---------|------------|------|---------|
| **GET** | Read resource(s) | ✅ | ✅ | `GET /users/{id}` |
| **POST** | Create resource | ❌ | ❌ | `POST /users` |
| **PUT** | Replace resource | ✅ | ❌ | `PUT /users/{id}` |
| **PATCH** | Update resource partially | ❌ | ❌ | `PATCH /users/{id}` |
| **DELETE** | Delete resource | ✅ | ❌ | `DELETE /users/{id}` |

### Status Code Conventions

**Success (2xx)**
- **200 OK** - Successful GET, PUT, PATCH, DELETE (with body)
- **201 Created** - Successful POST that creates a resource
- **204 No Content** - Successful operation with no response body (DELETE)
- **304 Not Modified** - Conditional GET with matching ETag

**Client Errors (4xx)**
- **400 Bad Request** - Malformed request, invalid JSON
- **401 Unauthorized** - Missing or invalid authentication token
- **403 Forbidden** - Authenticated but lacks permission
- **404 Not Found** - Resource doesn't exist
- **409 Conflict** - Concurrency conflict, duplicate resource
- **422 Unprocessable Entity** - Validation errors, business rule violations
- **429 Too Many Requests** - Rate limit exceeded

**Server Errors (5xx)**
- **500 Internal Server Error** - Unexpected server error
- **502 Bad Gateway** - External service unavailable
- **503 Service Unavailable** - Temporary overload or maintenance

### Pagination

**Query Parameters:**
```
GET /api/v1/conversations?page=2&pageSize=20
```

**Response Format:**
```json
{
  "data": [...],
  "pagination": {
    "page": 2,
    "pageSize": 20,
    "totalPages": 15,
    "totalCount": 285
  }
}
```

**Conventions:**
- Default `pageSize`: 20
- Max `pageSize`: 100
- `page` is 1-indexed (first page = 1)

### Filtering & Sorting

**Filtering:**
```
GET /api/v1/messages?conversationId={guid}&status=active
```

**Sorting:**
```
GET /api/v1/users?sort=createdAt:desc,email:asc
```

**Search:**
```
GET /api/v1/conversations?search=term
```

---

## ⚠️ Error Responses

### Problem Details Format (RFC 7807)

**Standard Error Response:**
```json
{
  "type": "https://axon.dev/errors/validation",
  "title": "Validation Error",
  "status": 422,
  "detail": "One or more validation errors occurred",
  "instance": "/api/v1/users",
  "traceId": "00-abc123-xyz789-00",
  "errors": {
    "email": ["Email is required", "Email must be valid"]
  }
}
```

### Error Type Catalog

| Error Type | HTTP Status | Use Case |
|------------|-------------|----------|
| **validation** | 422 | Invalid input, failed validation rules |
| **not-found** | 404 | Resource doesn't exist |
| **business-rule** | 422 | Domain invariant violation |
| **conflict** | 409 | Concurrency conflict, duplicate key |
| **unauthorized** | 401 | Invalid/expired token |
| **forbidden** | 403 | Insufficient permissions |
| **rate-limit** | 429 | Too many requests |
| **external-service** | 502 | External API failure |
| **internal** | 500 | Unexpected server error |

### Validation Error Structure

**Single Field Error:**
```json
{
  "type": "https://axon.dev/errors/validation",
  "title": "Validation Error",
  "status": 422,
  "errors": {
    "email": ["Email is required"]
  }
}
```

**Multiple Field Errors:**
```json
{
  "type": "https://axon.dev/errors/validation",
  "title": "Validation Error",
  "status": 422,
  "errors": {
    "email": ["Email is required", "Email must be valid"],
    "walletAddress": ["Invalid Solana address format"]
  }
}
```

### Error Mapping (Domain → HTTP)

**In Code:**
```csharp
// Domain Error → HTTP Status
Error.Validation("...") → 422 Unprocessable Entity
Error.NotFound("User", id) → 404 Not Found
Error.BusinessRule("...") → 422 Unprocessable Entity
Error.Conflict("...") → 409 Conflict
Error.External("...") → 502 Bad Gateway
Error.Internal("...") → 500 Internal Server Error
```

---

## 🔐 Authentication & Authorization

### Authentication Header
```
Authorization: Bearer <jwt-token>
```

**Token Sources:**
- Dynamic JWT (from Dynamic.xyz)
- Axon Access Token (wallet signature-based)

**Validation:**
- JWT signature validated via JWKS (Dynamic)
- Token expiration checked
- Issuer and audience validated

### Authorization Policies
```csharp
// In endpoints
[Authorize(Policy = "UserPolicy")]
[Authorize(Policy = "WalletOwnerPolicy")]
```

**See:** [Identity Authentication](../modules/identity/03-authentication.md) for detailed auth flows

---

## 📖 OpenAPI Specification

### Swagger UI

**Local Development:**
```
https://localhost:5001/swagger
```

**Endpoints:**
- `/swagger` - Swagger UI
- `/swagger/v1/swagger.json` - OpenAPI spec

### OpenAPI Generation with FastEndpoints

**Automatic Generation:**
FastEndpoints automatically generates OpenAPI specification from endpoint definitions.

**Configuration:**
```csharp
// In Program.cs
app.UseSwaggerGen(
    config =>
    {
        config.Title = "Axon API";
        config.Version = "v1";
    }
);
```

### Authentication in Swagger UI

**JWT Bearer Setup:**
1. Click "Authorize" button in Swagger UI
2. Enter: `Bearer <your-jwt-token>`
3. Click "Authorize"
4. All requests will include token

**Example:**
```
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Request/Response Examples

**Endpoint Definition:**
```csharp
public class CreateUserEndpoint : Endpoint<CreateUserRequest, CreateUserResponse>
{
    public override void Configure()
    {
        Post("/api/v1/users");
        Summary(s =>
        {
            s.Summary = "Create a new user";
            s.Description = "Creates a new user with email and optional profile";
            s.ExampleRequest = new CreateUserRequest { Email = "user@example.com" };
            s.Response<CreateUserResponse>(201, "User created successfully");
            s.Response(422, "Validation error");
        });
    }
}
```

### Swagger Customization

**Exclude Endpoints:**
```csharp
public override void Configure()
{
    Post("/internal/admin");
    Options(x => x.ExcludeFromDescription = true);
}
```

**Add Tags:**
```csharp
public override void Configure()
{
    Post("/api/v1/users");
    Tags("Users", "Identity");
}
```

---

## 🔄 Versioning Strategy

### URL-Based Versioning
```
/api/v1/users
/api/v1/conversations
/api/v1/messages
```

**Principles:**
- **v1 is stable** - Breaking changes require v2
- **Additive changes OK** - New fields in responses are non-breaking
- **Deprecation strategy** - Announce deprecation 6 months before removal

**Migration Path:**
```
v1 active → v2 introduced → v1 deprecated → v1 removed (6mo later)
```

---

## 🎯 API Design Checklist

**Before Creating New Endpoint:**
- [ ] Resource is properly named (plural noun)
- [ ] HTTP verb matches operation semantics
- [ ] Status codes are correct
- [ ] Error responses use Problem Details format
- [ ] Authentication/authorization configured
- [ ] Request/response DTOs are records
- [ ] FluentValidation rules added
- [ ] OpenAPI summary and examples provided
- [ ] Integration tests written

---

## 📚 Related Documentation

- **Identity Endpoints** → [Identity API Contracts](../modules/identity/05-api-contracts.md)
- **Chat Endpoints** → [Chat API Contracts](../modules/chat/05-api-contracts.md)
- **Error Handling** → [Error Handling Strategy](../shared/error-handling-strategy.md)
- **Validation** → [Validation Framework](../shared/validation-framework.md)

---

## Content still to be filled:
- Complete OpenAPI configuration examples
- Rate limiting header conventions
- CORS policy documentation
- Request/response compression
- Content negotiation (Accept header handling)
- Hypermedia controls (HATEOAS) if applicable
- Webhook endpoint conventions