# Root Cause Analysis - Story 1.1

**Bug:** Fix FastEndpoints Authorization for Invalid JWT Tokens in E2E Tests
**File:** `src/Api/Modules/IdentityApiModule.cs` (Authorization configuration)
**Date:** 2025-09-30

## Root Cause

**Category:** ☑️ Configuration Error (Authorization not enforced)

**Explanation:**

ASP.NET Core authorization middleware does NOT enforce authorization policies by default unless either:
1. A **FallbackPolicy** is configured to require authentication for all endpoints, OR
2. Individual endpoints have `[Authorize]` attributes/metadata

The codebase has:
- ✅ DefaultPolicy set to "DynamicOrAxon" (line 325 in IdentityApiModule.cs)
- ✅ Authentication schemes configured correctly (DynamicJwt, AxonJwt)
- ✅ Middleware order correct: UseAuthentication() → UseAuthorization() (Program.cs:194-195)
- ❌ **NO FallbackPolicy configured** - this is the root cause

**Result:** When an expired/invalid JWT is provided:
1. Authentication middleware validates token → fails → sets `HttpContext.User.IsAuthenticated = false`
2. Authorization middleware runs but has NO policy to enforce (no FallbackPolicy)
3. Request continues to FastEndpoints handler
4. MeEndpoint.ExecuteQuery() tries to extract claims from unauthenticated user
5. Returns empty response → 204 No Content (instead of 401 from middleware)

## Faulty Configuration

**File:** `src/Api/Modules/IdentityApiModule.cs:287-330`

**Current Code (Missing FallbackPolicy):**
```csharp
private static void ConfigureAuthorizationPolicies(IServiceCollection services)
{
    services.AddAuthorization(options =>
    {
        // Policy for endpoints that accept either Dynamic or Axon tokens
        options.AddPolicy("DynamicOrAxon", policy =>
            policy.AddAuthenticationSchemes("DynamicJwt", "AxonJwt")
                  .RequireAuthenticatedUser());

        // ... other policies ...

        // Default policy - accepts any authenticated user
        options.DefaultPolicy = options.GetPolicy("DynamicOrAxon") ??
            new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

        // ❌ MISSING: FallbackPolicy configuration
    });
}
```

**What's Wrong:**

`DefaultPolicy` is used when an endpoint explicitly requires authorization but doesn't specify which policy. However, it does **NOT** automatically apply to all endpoints.

Without `FallbackPolicy`, endpoints that don't explicitly declare authorization (via `[Authorize]` attribute or FastEndpoints `Policies()` method) are **NOT** protected at the middleware level.

FastEndpoints' `Policies("DynamicOrAxon")` method declares which policy to **evaluate**, but without `FallbackPolicy`, the authorization middleware doesn't enforce it before the handler executes.

## How It Should Work

When authentication fails (expired JWT), ASP.NET Core authorization middleware should:
1. Detect that FallbackPolicy requires authenticated user
2. User is NOT authenticated (token expired)
3. **Short-circuit request** → Return 401 Unauthorized
4. FastEndpoints handler **never executes**
5. AuthenticationErrorMiddleware formats response as JSON

## Impact

- **Severity:** Medium (security configuration issue)
- **Affected users:** All API clients using expired/invalid JWT tokens
- **Data loss risk:** No
- **Security risk:** Partial - endpoints still check authorization internally, but:
  - Inconsistent 401 responses (204 vs 401)
  - Authorization logic runs in endpoint handler instead of middleware
  - Not following "fail fast" principle
  - Misleading behavior for developers

## Secondary Issue

**File:** `src/Api/Modules/BaseIdentityQueryEndpoint.cs:29-30`

**Misleading Comment:**
```csharp
// IMPORTANT: Derived classes MUST call Policies() to enforce authorization
// FastEndpoints does NOT secure endpoints by default - authorization must be explicit
```

This comment is **incorrect** - FastEndpoints documentation claims endpoints are "secure by default", but the real issue is:
- ASP.NET Core middleware doesn't enforce authorization without FallbackPolicy
- Calling `Policies()` alone is not sufficient without FallbackPolicy

This comment should be updated to reflect the actual requirement for FallbackPolicy.

## Evidence

**Test Failure:** `tests/Modules/Identity/E2E/AuthMeE2ETests.cs:305-322`
```csharp
[Test]
public async Task AuthMe_InvalidJWT_ShouldReturn401()
{
    // Arrange: Use expired JWT
    var expiredJwt = JwtTestTokenFactory.CreateExpiredJwt();

    // Act: Call /auth/me with invalid token
    SetAuthorizationHeader(expiredJwt);
    var response = await HttpClient.GetAsync("/api/v1/auth/me");

    // Assert: Should return 401
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);  // ❌ FAILS: Gets 204 instead
}
```

**Endpoint Implementation:** `src/Api/Endpoints/V1/Auth/Queries/MeEndpoint.cs:32`
```csharp
public override void Configure()
{
    base.Configure();

    // Require authorization with DynamicOrAxon policy
    Policies("DynamicOrAxon");  // ⚠️ Not enforced without FallbackPolicy
}
```

## References

- [ASP.NET Core Authorization Policies](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies)
- [FastEndpoints Security Documentation](https://fast-endpoints.com/docs/security)
- Similar issue: [Stack Overflow - FallbackPolicy](https://stackoverflow.com/questions/59387914/allow-anonymouos-access-to-healthcheck-endpoint-when-authentication-fallback-pol)