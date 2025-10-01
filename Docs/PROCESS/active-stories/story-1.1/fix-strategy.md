# Fix Strategy - Story 1.1

**Bug:** Fix FastEndpoints Authorization for Invalid JWT Tokens in E2E Tests
**Approach:** Minimal Configuration Fix
**Date:** 2025-09-30

## Fix Approach: Minimal Configuration Change

**Strategy:** Add FallbackPolicy to enforce authentication at ASP.NET Core middleware level

**Why Minimal:**
- Single line addition to existing authorization configuration
- No changes to endpoint code needed
- No changes to middleware ordering
- No refactoring of existing patterns
- Fixes root cause directly at configuration level

## Changes Required

### 1. File: `src/Api/Modules/IdentityApiModule.cs`
**Change:** Add FallbackPolicy configuration
**Lines affected:** After line 329 (inside AddAuthorization block)
**Reason:** Enforce authentication requirement for all endpoints by default

**Before:**
```csharp
private static void ConfigureAuthorizationPolicies(IServiceCollection services)
{
    services.AddAuthorization(options =>
    {
        // ... existing policies ...

        // Default policy - accepts any authenticated user
        options.DefaultPolicy = options.GetPolicy("DynamicOrAxon") ??
            new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
    });
}
```

**After:**
```csharp
private static void ConfigureAuthorizationPolicies(IServiceCollection services)
{
    services.AddAuthorization(options =>
    {
        // ... existing policies ...

        // Default policy - accepts any authenticated user
        options.DefaultPolicy = options.GetPolicy("DynamicOrAxon") ??
            new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

        // FallbackPolicy - require authentication for all endpoints (secure by default)
        // Endpoints can opt-out using AllowAnonymous() (e.g., health checks, metrics)
        options.FallbackPolicy = options.DefaultPolicy;
    });
}
```

### 2. File: `src/Api/Modules/BaseIdentityQueryEndpoint.cs`
**Change:** Update misleading comment
**Lines affected:** Lines 29-30
**Reason:** Correct documentation to reflect FallbackPolicy requirement

**Before:**
```csharp
// IMPORTANT: Derived classes MUST call Policies() to enforce authorization
// FastEndpoints does NOT secure endpoints by default - authorization must be explicit
```

**After:**
```csharp
// Authorization is enforced globally via FallbackPolicy (configured in IdentityApiModule)
// Derived classes can call Policies() to use specific authorization policies beyond the default
```

## Minimal Fix Rationale

**Why This Is The Smallest Fix:**

1. **Single Configuration Line:** Adding `FallbackPolicy = DefaultPolicy` is a one-line change
2. **No Code Changes:** Existing endpoint implementations don't need modification
3. **No Middleware Changes:** Middleware order stays the same
4. **No Pattern Changes:** Result<T, Error> pattern, endpoint structure unchanged
5. **Leverages Existing Infrastructure:** Uses already-configured DefaultPolicy
6. **ASP.NET Core Best Practice:** FallbackPolicy is the standard way to enforce authentication globally

**Alternative Approaches Considered (Rejected as More Complex):**

❌ **Add `[Authorize]` to every endpoint** - Requires touching many files, repetitive
❌ **Add authorization check in base endpoint class** - Moves responsibility from middleware to application code
❌ **Configure FastEndpoints global security** - FastEndpoints relies on ASP.NET Core middleware anyway
❌ **Remove AllowAnonymous from BaseIdentityCommandEndpoint** - Would break exchange endpoint functionality

## Edge Cases Handled

### Edge Case 1: Exchange Endpoint (Already AllowAnonymous)
**Scenario:** `/api/v1/auth/exchange` needs to accept unauthenticated requests (validates JWT internally)

**Handling:**
- `BaseIdentityCommandEndpoint` already calls `AllowAnonymous()` (line 15)
- FallbackPolicy respects `AllowAnonymous()` declarations
- **No changes needed** ✅

### Edge Case 2: Health Check Endpoints
**Scenario:** `/health/live` and `/health/ready` should be accessible without auth

**Handling:**
- Health checks are MVC MapHealthChecks, not FastEndpoints
- ASP.NET Core health checks are exempt from FallbackPolicy by default
- If needed, can explicitly add `.AllowAnonymous()` to health check configuration
- **No changes needed** ✅

### Edge Case 3: Swagger/OpenAPI Endpoints
**Scenario:** `/swagger` should be accessible without auth (development)

**Handling:**
- Swagger endpoints are registered separately, not FastEndpoints
- Already configured to work without auth
- **No changes needed** ✅

### Edge Case 4: MeEndpoint with Valid JWT
**Scenario:** Valid JWT should still work (no regression)

**Handling:**
- FallbackPolicy requires authentication, which will succeed with valid JWT
- Endpoint handler executes normally
- **No changes needed** ✅

## Expected Behavior After Fix

### When Expired/Invalid JWT Is Provided:

**Before Fix:**
1. Authentication fails → `User.IsAuthenticated = false`
2. Authorization middleware has no policy to enforce
3. Request continues to endpoint handler
4. Handler tries to extract claims, fails
5. Returns empty response → 204 No Content ❌

**After Fix:**
1. Authentication fails → `User.IsAuthenticated = false`
2. Authorization middleware evaluates **FallbackPolicy**
3. FallbackPolicy requires authenticated user → **FAILS**
4. Authorization middleware **short-circuits** request
5. Returns **401 Unauthorized** (before handler executes) ✅
6. AuthenticationErrorMiddleware formats response as JSON ✅

### When Valid JWT Is Provided:

**Before & After Fix (Same Behavior):**
1. Authentication succeeds → `User.IsAuthenticated = true`
2. Authorization middleware evaluates FallbackPolicy → **PASSES**
3. Request continues to endpoint handler
4. Handler extracts claims, creates query
5. Returns 200 OK with user data ✅

## Regression Test Strategy

**Test Name:** `AuthMe_InvalidJWT_ShouldReturn401` (existing test)

**Test Approach (Given-When-Then):**
- **Given:** E2E test environment with API running
- **When:** Call `/api/v1/auth/me` with expired JWT token
- **Then:**
  - Response status is `401 Unauthorized`
  - Response body contains JSON error with code "UNAUTHORIZED"

**Test Status:**
- **Before Fix:** ❌ FAIL (returns 204)
- **Expected After Fix:** ✅ PASS (returns 401)

**Additional Test Coverage:**
- Existing test `AuthMe_NoAuthorizationHeader_ShouldReturn401` should still pass
- Existing test `AuthMe_NonexistentPrincipal_ShouldReturn404` should still pass
- All other Identity E2E tests should still pass (no regressions)

## Implementation Steps

1. ✅ Analyze root cause → Configuration Error (no FallbackPolicy)
2. ✅ Design fix → Add FallbackPolicy = DefaultPolicy
3. ⏳ Add FallbackPolicy configuration to IdentityApiModule.cs
4. ⏳ Update misleading comment in BaseIdentityQueryEndpoint.cs
5. ⏳ Run failing test → Verify it now passes
6. ⏳ Run full Identity E2E test suite → Verify no regressions
7. ⏳ Build project → Verify no compilation errors
8. ⏳ Commit changes with descriptive message

## Files Changed Summary

- **1 file modified:** `src/Api/Modules/IdentityApiModule.cs` (+3 lines: FallbackPolicy + comment)
- **1 file modified:** `src/Api/Modules/BaseIdentityQueryEndpoint.cs` (-2/+2 lines: comment update)
- **Total:** 2 files, ~5 lines changed

## Success Criteria

- ✅ Test `AuthMe_InvalidJWT_ShouldReturn401` passes
- ✅ All existing Identity E2E tests pass
- ✅ All existing Auth tests pass
- ✅ Build succeeds with no warnings
- ✅ No new bugs introduced
- ✅ FastEndpoints still works for anonymous endpoints (exchange, health checks)