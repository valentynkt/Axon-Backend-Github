# Story 1.1: Fix FastEndpoints Authorization for Invalid JWT Tokens in E2E Tests

Status: InProgress - Attempt 1 Failed (FallbackPolicy insufficient)

## Story

As a **QA Engineer / DevOps Engineer**,
I want **E2E tests to return proper 401 Unauthorized responses when invalid or expired JWT tokens are provided**,
so that **authorization failures are correctly detected and security requirements are properly validated in automated tests**.

## Acceptance Criteria

1. **AC1**: Test `AuthMe_InvalidJWT_ShouldReturn401` passes - returns `401 Unauthorized` (not `204 No Content`) when expired JWT is provided
2. **AC2**: FastEndpoints authorization middleware properly enforces `Policies("DynamicOrAxon")` declared in endpoints
3. **AC3**: ASP.NET Core `UseAuthentication()` and `UseAuthorization()` middleware correctly block unauthenticated requests BEFORE FastEndpoints executes endpoint handlers
4. **AC4**: `AuthenticationErrorMiddleware` properly formats 401 responses as JSON with error details
5. **AC5**: All existing Identity E2E tests continue to pass (no regressions)
6. **AC6**: Solution follows FastEndpoints best practices and maintains "secure by default" principle

## Tasks / Subtasks

- [ ] **Task 1**: Investigate FastEndpoints authorization integration (AC: #1, #2, #3)
  - [x] Research FastEndpoints security documentation
  - [x] Analyze middleware ordering in Program.cs
  - [x] Identify root cause: FastEndpoints "secure by default" implementation vs ASP.NET Core authorization middleware
  - [ ] Verify how `Policies()` method integrates with ASP.NET Core authorization

- [ ] **Task 2**: Implement proper authorization enforcement (AC: #2, #3, #6)
  - [ ] Configure FastEndpoints to properly integrate with ASP.NET Core authorization
  - [ ] Ensure `Policies()` declarations are enforced at middleware level
  - [ ] Verify authorization happens BEFORE endpoint handler execution
  - [ ] Add proper authorization checks if FastEndpoints requires explicit configuration

- [ ] **Task 3**: Fix middleware configuration if needed (AC: #3, #4)
  - [ ] Review middleware ordering: UseRouting → UseAuthentication → UseAuthorization → UseFastEndpoints
  - [ ] Ensure `AuthenticationErrorMiddleware` runs after authorization checks
  - [ ] Verify 401 responses are properly formatted with JSON error bodies

- [ ] **Task 4**: Update endpoint configuration (AC: #2, #6)
  - [ ] Fix misleading comment in `BaseIdentityQueryEndpoint.Configure()` about "secure by default"
  - [ ] Verify all Identity endpoints properly declare authorization policies
  - [ ] Ensure no endpoints accidentally allow anonymous access

- [ ] **Task 5**: Testing and validation (AC: #1, #5)
  - [ ] Run `AuthMe_InvalidJWT_ShouldReturn401` test - verify 401 response
  - [ ] Run full Identity E2E test suite - verify no regressions
  - [ ] Test with multiple JWT failure scenarios (expired, invalid signature, malformed)
  - [ ] Verify proper error response format and status codes

## Dev Notes

### Technical Context

**Problem Identified**: E2E test `AuthMe_InvalidJWT_ShouldReturn401` fails because `/auth/me` endpoint returns `204 No Content` instead of `401 Unauthorized` when an expired JWT is provided.

**Root Cause Analysis**:
1. FastEndpoints claims "secure by default" but this applies to FastEndpoints' OWN processing, not ASP.NET Core middleware level
2. When authentication fails (expired JWT), `HttpContext.User.Identity.IsAuthenticated = false` is set
3. However, FastEndpoints endpoint handler still executes (not blocked by ASP.NET Core authorization middleware)
4. Endpoint returns empty response → 204 No Content

**Middleware Flow** (src/Api/Program.cs:191-206):
```
UseRouting()
→ UseAuthentication()
→ UseAuthorization()
→ UseAuthenticationErrorFormatting()
→ UseFastEndpoints()
```

**Key Files**:
- `src/Api/Endpoints/V1/Auth/Queries/MeEndpoint.cs:32` - Uses `Policies("DynamicOrAxon")`
- `src/Api/Modules/BaseIdentityQueryEndpoint.cs:28` - MISLEADING comment: "Endpoints are secure by default"
- `src/Api/Configuration/ServiceRegistration.cs:45` - FastEndpoints registration
- `src/Api/Modules/IdentityApiModule.cs:287-330` - Authorization policies configuration
- `src/Api/Middleware/AuthenticationErrorMiddleware.cs:39` - Only formats 401 if body is empty
- `tests/Modules/Identity/E2E/AuthMeE2ETests.cs:304` - Failing test

### Architecture Patterns & Constraints

**Framework**: FastEndpoints (https://fast-endpoints.com/docs/security)
- FastEndpoints integrates with ASP.NET Core authentication/authorization middleware
- `Policies()` method should enforce authorization policies
- Default behavior: endpoints require authentication unless `AllowAnonymous()` is called

**Current Configuration**:
- Policy "DynamicOrAxon" defined in `IdentityApiModule.cs:292-294`
- Policy requires authenticated user with either "DynamicJwt" OR "AxonJwt" schemes
- MeEndpoint calls `Policies("DynamicOrAxon")` in Configure() method

**Testing Requirements** [Source: Docs/ENGINEERING/guides/architecture/tech-stack.md]:
- NUnit test framework
- Shouldly for assertions
- Testcontainers for integration tests

**Error Handling** [Source: Docs/ENGINEERING/guides/codebase/coding-standards.md]:
- Use `Result<T, Error>` pattern
- `Error.Unauthorized()` for authentication failures
- Never throw exceptions for business logic

### Project Structure Notes

**Test File Location**:
- `tests/Modules/Identity/E2E/AuthMeE2ETests.cs`
- `tests/Modules/Identity/E2E/Infrastructure/E2ETestBase.cs` - Base test setup
- `tests/Modules/Identity/E2E/Infrastructure/JwtTestTokenFactory.cs` - JWT generation
- `tests/Modules/Identity/E2E/Infrastructure/TestJwksService.cs` - Test JWKS provider

**API Structure**:
- API endpoints in `src/Api/Endpoints/V1/{Module}/{Type}/`
- Base endpoint classes in `src/Api/Modules/`
- Middleware in `src/Api/Middleware/`

**Clean Architecture Alignment**:
- API layer handles HTTP concerns (endpoints, middleware)
- Application layer handles business logic (not touched by this story)
- Infrastructure layer handles external services (not touched by this story)

### References

- [FastEndpoints Security Docs](https://fast-endpoints.com/docs/security)
- [ASP.NET Core Authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies)
- [Source: Docs/ENGINEERING/guides/architecture/tech-stack.md#API Layer] - FastEndpoints configuration
- [Source: Docs/ENGINEERING/guides/codebase/coding-standards.md#Error Handling Standards] - Result pattern usage
- [Source: src/Api/Program.cs:191-206] - Middleware ordering
- [Source: src/Api/Modules/IdentityApiModule.cs:287-330] - Authorization policy configuration

## Change Log

| Date     | Version | Description   | Author |
| -------- | ------- | ------------- | ------ |
| 2025-09-30 | 0.1     | Initial draft | Valik  |

## Dev Agent Record

### Context Reference

- [Story Context XML](../../research/story-context-1.1.xml) - Generated 2025-09-30

### Agent Model Used

Claude Sonnet 4.5 (claude-sonnet-4-5-20250929)

### Debug Log References

### Completion Notes List

### File List

## Implementation Attempts

### Attempt 1: FallbackPolicy Configuration (2025-09-30 19:40)

**Hypothesis:** Missing FallbackPolicy in ASP.NET Core authorization configuration

**Changes:**
- Added `options.FallbackPolicy = options.DefaultPolicy;` to `IdentityApiModule.cs`
- Updated comment in `BaseIdentityQueryEndpoint.cs`

**Result:** ❌ FAILED
- E2E test `AuthMe_InvalidJWT_ShouldReturn401` still returns `204 No Content` instead of `401 Unauthorized`
- Build passes, unit tests pass, but E2E test proves fix is insufficient

**Learning:** FallbackPolicy alone does not enforce authorization in FastEndpoints. Need to investigate:
1. FastEndpoints-specific authorization configuration
2. Whether `Policies()` method requires additional setup
3. Middleware execution order with expired JWT
4. Explicit authorization requirements on endpoints