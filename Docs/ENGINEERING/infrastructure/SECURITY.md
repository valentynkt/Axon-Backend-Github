# Security Guide

**Comprehensive guide to authentication, authorization, and security in Axon Backend.**

---

## Overview

**Authentication Strategy**: Dual JWT (Dynamic.xyz + Axon-issued)
**Authorization**: Policy-based with ClaimsPrincipal
**Secrets**: User Secrets (dev) + Environment Variables (prod)

---

## Authentication Architecture

### Dual JWT System

```
┌─────────────────┐
│  Dynamic.xyz    │  External Provider
│  JWT (Entry)    │  → Exchange Endpoint → Axon JWT
└─────────────────┘                         ↓
                                    ┌───────────────┐
                                    │  Axon-issued  │
                                    │  JWT (API)    │
                                    └───────────────┘
```

**Flow**:
1. Client authenticates with Dynamic.xyz → receives Dynamic JWT
2. Client calls `/api/v1/auth/exchange` with Dynamic JWT
3. Server validates Dynamic JWT, creates/updates principal
4. Server issues Axon JWT with principal claims
5. Client uses Axon JWT for all subsequent API calls

---

## JWT Configuration

### Startup Configuration (IdentityApiModule.cs)

```csharp
public sealed class IdentityApiModule : IApiModule
{
    private static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        // Prevent implicit claim remapping globally
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        // Get configuration sections
        var dynamicSection = configuration.GetSection("Dynamic");
        var axonSection = configuration.GetSection("Axon");
        var authSection = configuration.GetSection("Authentication");

        // Dynamic JWT configuration
        var dynamicAuthority = dynamicSection["Authority"];
        var dynamicAudience = dynamicSection["Audience"];
        var dynamicJwksUri = dynamicSection["JwksUri"];
        var dynamicIssuer = dynamicSection["Issuer"];

        // Axon JWT configuration
        var axonIssuer = axonSection["Issuer"] ?? "axon-api";
        var axonAudience = axonSection["Audience"] ?? "axon-api";
        var axonSigningKey = axonSection["SigningKey"]; // Required

        // Clock skew configuration (default: 60 seconds)
        var clockSkewSeconds = authSection.GetValue("ClockSkewSeconds", 60);

        services.AddAuthentication(options =>
        {
            // Use policy selector for multiple schemes
            options.DefaultScheme = "DynamicOrAxon";
            options.DefaultAuthenticateScheme = "DynamicOrAxon";
            options.DefaultChallengeScheme = "AxonJwt";
        })
        .AddPolicyScheme("DynamicOrAxon", "Dynamic or Axon JWT", options =>
        {
            // Automatic scheme selection based on JWT issuer
            options.ForwardDefaultSelector = context =>
            {
                var authorization = context.Request.Headers.Authorization.FirstOrDefault();
                if (string.IsNullOrEmpty(authorization))
                    return "AxonJwt";

                // Parse token to check issuer (without validation)
                if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = authorization["Bearer ".Length..];
                    var handler = new JwtSecurityTokenHandler();
                    if (handler.CanReadToken(token))
                    {
                        var jwtToken = handler.ReadJwtToken(token);
                        // Dynamic tokens have issuer starting with app.dynamicauth.com
                        if (jwtToken.Issuer?.StartsWith("app.dynamicauth.com") == true)
                            return "DynamicJwt";
                    }
                }

                return "AxonJwt";
            };
        })
        .AddJwtBearer("DynamicJwt", options =>
        {
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = true;

            // Use OIDC discovery or JWKS URI directly
            if (!string.IsNullOrEmpty(dynamicAuthority))
                options.Authority = dynamicAuthority;
            else if (!string.IsNullOrEmpty(dynamicJwksUri))
                options.MetadataAddress = dynamicJwksUri;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = dynamicIssuer ?? "https://app.dynamic.xyz",
                ValidateAudience = !string.IsNullOrEmpty(dynamicAudience),
                ValidAudience = dynamicAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(clockSkewSeconds),
                NameClaimType = "sub",
                RoleClaimType = "role"
            };
        })
        .AddJwtBearer("AxonJwt", options =>
        {
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = false; // Allow HTTP in dev

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(axonSigningKey));
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = axonIssuer,
                ValidateAudience = true,
                ValidAudience = axonAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(clockSkewSeconds),
                NameClaimType = "sub",
                RoleClaimType = "role"
            };
        });
    }
}
```

---

## JWT Token Generation

### Axon Token Service (JwtTokenService.cs)

```csharp
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly IDataProtector _keyProtector;

    public async Task<Result<AxonToken, Error>> GenerateAccessTokenAsync(
        AxonUserId userId,
        ProviderType providerType,
        string providerSubject,
        string issuer,
        int expiryMinutes = 15,
        CancellationToken ct = default)
    {
        // 1. Find or create user
        var user = await FindOrCreateUserAsync(userId, providerType, issuer, providerSubject, ct);
        if (user == null)
            return Error.NotFound("User not found", "AUTH.USER_NOT_FOUND");

        // 2. Update last authenticated
        user.UpdateLastAuthenticated(refreshSecurityStamp: false);
        await _userManager.UpdateAsync(user);

        // 3. Build claims
        var claims = await BuildAccessTokenClaimsAsync(user);

        // 4. Generate token
        var key = GetSigningKey(); // From protected config
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(expiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _configuration["Axon:Issuer"] ?? "axon-api",
            audience: _configuration["Axon:Audience"] ?? "axon-api",
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        var tokenString = _tokenHandler.WriteToken(token);

        return new AxonToken(
            AccessToken: tokenString,
            TokenType: "Bearer",
            ExpiresIn: (int)(expires - now).TotalSeconds);
    }

    private async Task<List<Claim>> BuildAccessTokenClaimsAsync(AxonUserAuth user)
    {
        return
        [
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), // User ID
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Token ID
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("axon_user_id", user.Id.ToString()),
            new Claim("provider", user.ProviderType.Value),
            new Claim("email", user.Email ?? ""),
            // Add role claims
            ..await GetRoleClaimsAsync(user)
        ];
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        // Retrieve and unprotect signing key
        var protectedKey = _configuration["Axon:SigningKey"]
            ?? throw new InvalidOperationException("Axon:SigningKey not configured");
        var unprotectedKey = _keyProtector.Unprotect(protectedKey);
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(unprotectedKey));
    }
}
```

---

## Authorization

### Policy-Based Authorization

```csharp
private static void ConfigureAuthorizationPolicies(IServiceCollection services)
{
    services.AddAuthorization(options =>
    {
        // Default policy - requires any authenticated user
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes("DynamicOrAxon")
            .Build();

        // Admin policy
        options.AddPolicy("AdminOnly", policy =>
            policy.RequireRole("Admin")
                  .AddAuthenticationSchemes("AxonJwt"));

        // Dynamic-authenticated users only
        options.AddPolicy("DynamicAuth", policy =>
            policy.RequireAuthenticatedUser()
                  .AddAuthenticationSchemes("DynamicJwt"));

        // Verified wallet requirement
        options.AddPolicy("VerifiedWallet", policy =>
            policy.RequireClaim("wallet_verified", "true")
                  .AddAuthenticationSchemes("AxonJwt"));
    });
}
```

### Using Policies in Endpoints

```csharp
// FastEndpoints example
public override void Configure()
{
    Get("/api/v1/admin/stats");
    Policies("AdminOnly"); // Requires admin role
}

// Alternative: Check in handler
public override async Task HandleAsync(CancellationToken ct)
{
    if (!User.IsInRole("Admin"))
        await SendUnauthorizedAsync(ct);

    // ... admin logic
}
```

---

## ClaimsPrincipal Extensions

### Extracting User Information

```csharp
public static class ClaimsPrincipalExtensions
{
    public static AxonUserId? GetPrincipalId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("axon_user_id")
                 ?? principal.FindFirst(ClaimTypes.NameIdentifier);

        if (claim == null || !Guid.TryParse(claim.Value, out var guid))
            return null;

        return new AxonUserId(guid);
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;
    }

    public static string? GetProviderType(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("provider")?.Value;
    }

    public static bool HasVerifiedWallet(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("wallet_verified");
        return claim?.Value == "true";
    }
}
```

### Usage in Handlers

```csharp
public sealed class GetMyPrincipalHandler : IRequestHandler<GetMyPrincipalQuery, Result<CurrentUserResult, Error>>
{
    public async Task<Result<CurrentUserResult, Error>> Handle(
        GetMyPrincipalQuery query, CancellationToken ct)
    {
        var principalId = User.GetPrincipalId();
        if (principalId == null)
            return Error.Unauthorized("Invalid token", "AUTH.INVALID_TOKEN");

        var principal = await _repository.GetByIdAsync(principalId.Value, ct);
        // ...
    }
}
```

---

## Secrets Management

### Development (User Secrets)

```bash
# Initialize user secrets
dotnet user-secrets init --project src/Api

# Set secrets
dotnet user-secrets set "Axon:SigningKey" "your-256-bit-secret-key" --project src/Api
dotnet user-secrets set "Dynamic:Issuer" "https://app.dynamic.xyz" --project src/Api
dotnet user-secrets set "ConnectionStrings:IdentityDb" "Host=localhost;..." --project src/Api

# List secrets
dotnet user-secrets list --project src/Api
```

### Production (Environment Variables)

```bash
# Docker/Kubernetes
export AXON__SIGNINGKEY="production-secret-key"
export DYNAMIC__ISSUER="https://app.dynamic.xyz"
export CONNECTIONSTRINGS__IDENTITYDB="Host=prod-db;..."

# Or in appsettings.Production.json (encrypted)
{
  "Axon": {
    "SigningKey": "${AXON_SIGNING_KEY}" // Read from environment
  }
}
```

### Data Protection

```csharp
// Protect sensitive configuration values
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("./keys"))
    .SetApplicationName("Axon");

// Usage
public class JwtTokenService
{
    private readonly IDataProtector _keyProtector;

    public JwtTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _keyProtector = dataProtectionProvider.CreateProtector("Axon.JWT.SigningKey");
    }

    private string GetSigningKey()
    {
        var protectedKey = _configuration["Axon:SigningKey"];
        return _keyProtector.Unprotect(protectedKey);
    }
}
```

---

## Security Headers

### Configuration (Program.cs)

```csharp
app.Use(async (context, next) =>
{
    // Security headers
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // HSTS (HTTPS only)
    if (context.Request.IsHttps)
    {
        context.Response.Headers["Strict-Transport-Security"] =
            "max-age=31536000; includeSubDomains";
    }

    // Content Security Policy
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline';";

    await next();
});
```

### CORS Configuration

```csharp
services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.WithOrigins(
                configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:3000"])
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetIsOriginAllowedToAllowWildcardSubdomains();
    });
});

// In middleware pipeline
app.UseCors();
```

---

## Rate Limiting

### Per-Endpoint Rate Limiting (Program.cs)

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) =>
    {
        // Add rate limit headers
        context.HttpContext.Response.Headers["X-RateLimit-Limit"] = "10";
        context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";
        context.HttpContext.Response.Headers["X-RateLimit-Reset"] =
            DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString();
        context.HttpContext.Response.Headers.RetryAfter = "60";
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').First()?.Trim()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        // Rate limit /api/v1/auth/exchange endpoint
        if (context.Request.Path.StartsWithSegments("/api/v1/auth/exchange"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                $"rate_limited_{ipAddress}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }

        return RateLimitPartition.GetNoLimiter($"no_limit_{ipAddress}");
    });
});

// In middleware pipeline
app.UseRateLimiter();
```

---

## Best Practices

### ✅ DO
- Use `JwtSecurityTokenHandler.DefaultMapInboundClaims = false` to prevent claim remapping
- Store signing keys in User Secrets (dev) or environment variables (prod)
- Use Data Protection API for sensitive config values
- Implement rate limiting on authentication endpoints
- Add security headers (X-Content-Type-Options, X-Frame-Options, etc.)
- Use policy-based authorization over role strings
- Validate JWT lifetime with clock skew tolerance
- Use token rotation for refresh tokens to prevent replay attacks

### ❌ DON'T
- Hard-code secrets in appsettings.json
- Use weak signing keys (<256 bits)
- Disable HTTPS in production
- Skip JWT audience validation
- Allow unlimited authentication attempts
- Store tokens in localStorage (use httpOnly cookies or memory)
- Use `ValidateIssuerSigningKey = false` (always validate)

---

## Testing Security

```csharp
[TestFixture]
public class AuthenticationTests : ApiTestBase
{
    [Test]
    public async Task ExchangeEndpoint_WithValidDynamicJwt_ShouldReturnAxonToken()
    {
        // Arrange
        var dynamicToken = GenerateValidDynamicJwt();

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/exchange",
            new { bearerToken = dynamicToken });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ExchangeResponse>();
        result.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturn401()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RateLimit_ExceedingLimit_ShouldReturn429()
    {
        // Act - Send 11 requests (limit is 10/minute)
        for (int i = 0; i < 11; i++)
        {
            var response = await _client.PostAsync("/api/v1/auth/exchange", null);
            if (i == 10)
                response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        }
    }
}
```

---

## Related Documentation

- [Authentication Flows](../../modules/identity/03-authentication.md) - Identity module auth patterns
- [API Contracts](../../modules/identity/05-api-contracts.md) - Authentication endpoints
- [Testing Guide](../../testing/TESTING-GUIDE.md) - Testing authenticated endpoints

---

**Last Updated**: 2025-09-30
**Maintained By**: Axon Engineering Team