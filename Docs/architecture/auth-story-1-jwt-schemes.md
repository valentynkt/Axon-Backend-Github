# Story 1: JWT Authentication Schemes Configuration

## Overview
Configure dual JWT authentication schemes to support both Dynamic.xyz tokens and internally-issued Axon tokens using ASP.NET Core's built-in JwtBearer authentication.

## Success Criteria
- Two named authentication schemes configured: "DynamicJwt" and "AxonJwt"
- Exchange endpoint can manually authenticate against DynamicJwt scheme
- Protected endpoints use AxonJwt scheme by default
- Proper clock skew and validation parameters configured

## Tasks

### Task 1.1: Configure Authentication Services
**File:** `src/Api/Program.cs` or Authentication configuration

- [ ] Add `Microsoft.AspNetCore.Authentication.JwtBearer` package if not present
- [ ] Prevent implicit claim remapping globally
- [ ] Configure "DynamicJwt" scheme with JWKS endpoint
- [ ] Configure "AxonJwt" scheme with symmetric key validation
- [ ] Set default scheme to "AxonJwt" for protected endpoints
- [ ] Configure clock skew (60 seconds) for both schemes

**Implementation:**
```csharp
// Prevent implicit claim remapping globally
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "AxonJwt";
    options.DefaultChallengeScheme = "AxonJwt";
})
.AddJwtBearer("DynamicJwt", options =>
{
    options.MapInboundClaims = false;
    options.RequireHttpsMetadata = true;

    // If Dynamic supports OIDC discovery:
    options.Authority = configuration["Authentication:Dynamic:Authority"];
    options.Audience = configuration["Authentication:Dynamic:Audience"];

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = !string.IsNullOrEmpty(configuration["Authentication:Dynamic:Audience"]),
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(configuration.GetValue("Authentication:ClockSkewSeconds", 60)),
        NameClaimType = "sub",
        RoleClaimType = "role"
    };

    // Alternative: If Dynamic doesn't provide OIDC discovery, use:
    // options.MetadataAddress = configuration["Authentication:Dynamic:JwksUri"];
    // And add to TokenValidationParameters:
    // ValidIssuer = configuration["Authentication:Dynamic:Issuer"]
})
.AddJwtBearer("AxonJwt", options =>
{
    options.MapInboundClaims = false;
    options.RequireHttpsMetadata = true;

    var issuer = configuration["Authentication:Issuer"];
    var audience = configuration["Authentication:Audience"];
    var keyB64 = configuration["Authentication:SigningKey"];
    var keyBytes = Convert.FromBase64String(keyB64); // Base64 required
    var key = new SymmetricSecurityKey(keyBytes);

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = issuer,
        ValidateIssuer = true,

        ValidateAudience = !string.IsNullOrEmpty(audience),
        ValidAudience = audience,

        IssuerSigningKey = key,
        ValidateIssuerSigningKey = true,

        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(configuration.GetValue("Authentication:ClockSkewSeconds", 60)),
        NameClaimType = "sub",
        RoleClaimType = "role"
    };
});
```

### Task 1.2: Update Authentication Configuration
**File:** `src/Api/appsettings.json`

- [ ] Add HmacKeys section with versioned keys (Base64 encoded)
- [ ] Configure Dynamic authority and audience
- [ ] Set token expiry parameters
- [ ] Add clock skew configuration
- [ ] Ensure SigningKey is Base64 encoded

**Configuration Structure:**
```json
{
  "Authentication": {
    "Issuer": "axon-api",
    "Audience": "axon-platform",
    "SigningKey": "BASE64-ENCODED-HS256-KEY",  // e.g., 32+ random bytes base64
    "HmacKeys": {
      "v1": "BASE64-HMAC",
      "Current": "v1"
    },
    "Dynamic": {
      "Authority": "https://auth.dynamic.xyz",  // or provide JwksUri + Issuer
      "Audience": "your-dynamic-audience"
    },
    "MaxTtlSeconds": 300,
    "AccessTokenExpirySeconds": 1800,
    "ClockSkewSeconds": 60
  }
}
```

### Task 1.3: Update Exchange Endpoint Authentication
**File:** `src/Api/Endpoints/V1/Auth/Commands/ExchangeEndpoint.cs`

- [ ] Keep endpoint as `[AllowAnonymous]`
- [ ] Add manual authentication check using `HttpContext.AuthenticateAsync("DynamicJwt")`
- [ ] Map authentication failures to proper 401 errors (distinguish between None and Failure)
- [ ] Remove custom bearer token extraction logic

**Code Changes:**
```csharp
protected override async Task<Result<ExchangeCredentialCommand, Error>> ExecuteCommand(
    ExchangeTokenRequestDto request,
    CancellationToken ct)
{
    // Manual authentication against DynamicJwt scheme
    var authResult = await HttpContext.AuthenticateAsync("DynamicJwt");

    if (!authResult.Succeeded)
    {
        var reason = authResult.None
            ? "Missing or invalid Authorization header"
            : authResult.Failure?.Message;

        Logger.LogWarning("Dynamic JWT authentication failed: {Reason}", reason);

        return Result.Failure<ExchangeCredentialCommand, Error>(
            Error.Unauthorized(
                reason ?? "Invalid Dynamic JWT",
                "AUTH.INVALID_TOKEN"));
    }

    var principal = authResult.Principal!;
    // Extract claims and continue with exchange logic...
}
```

### Task 1.4: Update Protected Endpoints
**File:** `src/Api/Endpoints/V1/Auth/Queries/MeEndpoint.cs` and others

- [ ] Add `[Authorize(AuthenticationSchemes = "AxonJwt")]` attribute
- [ ] Remove any custom authentication logic
- [ ] Ensure HttpContext.User is properly populated

### Task 1.5: Create Authentication Options Class
**File:** `src/Modules/Identity/Application/Configuration/AuthenticationOptions.cs`

- [ ] Create strongly-typed options class
- [ ] Add validation attributes
- [ ] Support HMAC key versioning
- [ ] Include all authentication parameters

```csharp
public sealed class AuthenticationOptions
{
    [Required]
    public string Issuer { get; set; } = null!;

    [Required]
    public string Audience { get; set; } = null!;

    [Required]
    public string SigningKey { get; set; } = null!;

    [Required]
    public Dictionary<string, string> HmacKeys { get; set; } = new();

    [Required]
    public string CurrentKeyVersion { get; set; } = "v1";

    [Range(60, 600)]
    public int MaxTtlSeconds { get; set; } = 300;

    [Range(300, 7200)]
    public int AccessTokenExpirySeconds { get; set; } = 1800;

    [Range(0, 300)]
    public int ClockSkewSeconds { get; set; } = 60;
}
```

## Testing Requirements

### Unit Tests
- Verify JWT schemes are properly configured
- Test manual authentication in Exchange endpoint
- Validate clock skew handling
- Verify `MapInboundClaims = false` (claims retain original names)

### Integration Tests
- Test Dynamic JWT authentication flow
- Test Axon JWT authentication flow
- Verify scheme isolation (Dynamic tokens don't work on Axon endpoints)
- Test `/auth/exchange` endpoint:
  - Missing header → `authResult.None` path → 401
  - Bad token → `authResult.Failure` path → 401
  - Good Dynamic token → proceeds
- Verify protected endpoint rejects Dynamic token but accepts Axon token

## Acceptance Criteria
- [ ] Dynamic JWT tokens authenticate successfully via Exchange endpoint
- [ ] Axon JWT tokens work on protected endpoints
- [ ] Invalid tokens return proper 401 errors with clear messages
- [ ] Clock skew prevents minor time synchronization issues
- [ ] Configuration is externalized and environment-specific