# Dynamic.xyz Authentication Protocols & Security Guide

## Overview

This guide covers Dynamic.xyz's authentication mechanisms, security protocols, and best practices for implementing secure authentication in production applications.

Dynamic.xyz provides multiple authentication options including wallet-based authentication, social logins, email authentication, and enterprise-grade external authentication providers.

## Core Authentication Concepts

### 1. JWT-Based Authentication

Dynamic.xyz uses **JSON Web Tokens (JWT)** with the **RS256** algorithm for secure authentication:

- **Signing**: Each environment has a unique private key for signing JWTs
- **Verification**: Public keys are available via JWKS endpoints for token validation
- **Algorithm**: RS256 (RSA Signature with SHA-256)
- **Token Format**: Standard JWT with Dynamic-specific claims

### 2. Token Lifecycle

```
1. User Authentication → 2. JWT Generation → 3. Token Validation → 4. Session Management
```

**Token Properties**:
- **Expiration**: Configurable token lifetime
- **Refresh**: Automatic token refresh capabilities
- **Revocation**: Session-based token invalidation
- **Scope-based**: Permission-based access control

---

## API Token Management

### 1. Creating API Tokens

API tokens are required for server-side operations and programmatic access to Dynamic.xyz APIs.

**Step-by-Step Process**:

1. **Access Dashboard**: Navigate to [Developer Tab](https://app.dynamic.xyz/dashboard/developer/api)
2. **Create Token**: Click "Create Token" in the API Token section
3. **Name Token**: Provide a meaningful name for the token:
   - `Production-Backend-Service`
   - `Staging-Environment-API`
   - `UserManagement-Service`
   - `Analytics-Dashboard`
4. **Copy Token**: **Immediately copy and securely store the token**
5. **Token Format**: `dyn_` + 56 alphanumeric characters

**Security Requirements**:
- ✅ Store tokens in secure environment variables
- ✅ Use different tokens for different environments
- ✅ Rotate tokens regularly (quarterly recommended)
- ✅ Implement proper token storage and access controls
- ❌ Never commit tokens to version control
- ❌ Never log tokens in application logs
- ❌ Never share tokens via unsecured channels

### 2. Token Usage

```http
Authorization: Bearer dyn_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
Content-Type: application/json
```

**Best Practices**:
- Always use HTTPS for API calls
- Implement proper error handling for token validation failures
- Monitor API usage for unusual patterns
- Set appropriate timeout values for API calls

---

## JWT Validation & Verification

### 1. Server-Side JWT Validation

Dynamic.xyz provides multiple approaches for JWT validation:

#### Option A: JWKS-Based Validation (Recommended)

```typescript
// Example implementation pattern
const jwksUrl = `https://app.dynamic.xyz/api/v0/sdk/${ENVIRONMENT_ID}/.well-known/jwks`;

// Key steps:
1. Fetch public keys from JWKS endpoint
2. Cache keys for performance (10-minute TTL recommended)
3. Validate JWT signature using RS256 algorithm
4. Verify token claims (iss, exp, aud)
5. Check for additional verification scopes
```

#### Option B: NextAuth Integration

For Next.js applications, Dynamic.xyz provides seamless NextAuth integration:

```javascript
import NextAuth from 'next-auth'
import { DynamicProvider } from '@dynamic-labs/nextauth-provider'

export default NextAuth({
  providers: [
    DynamicProvider({
      environmentId: process.env.DYNAMIC_ENVIRONMENT_ID,
      // Additional configuration options
    })
  ],
})
```

#### Option C: Passport.js Integration

For Express.js and Node.js applications:

```javascript
const passport = require('passport');
const DynamicStrategy = require('passport-dynamic').Strategy;

passport.use(new DynamicStrategy({
    environmentId: process.env.DYNAMIC_ENVIRONMENT_ID
  },
  function(payload, done) {
    // Handle user authentication logic
    return done(null, payload);
  }
));
```

### 2. JWT Claims Structure

Dynamic.xyz JWTs contain standard and custom claims:

**Standard Claims**:
- `iss` (Issuer): Dynamic.xyz environment identifier
- `sub` (Subject): Unique user identifier
- `aud` (Audience): Intended token recipient
- `exp` (Expiration): Token expiration timestamp
- `iat` (Issued At): Token creation timestamp
- `nbf` (Not Before): Token validity start time

**Dynamic-Specific Claims**:
- `scopes`: Array of permission scopes
- `environment_id`: Dynamic.xyz environment identifier
- `lists`: User's access list memberships
- `verified_credentials`: Array of user's verified authentication methods

**Example JWT Payload**:
```json
{
  "iss": "https://app.dynamic.xyz/api/v0/sdk/95b11417-f18f-457f-8804-68e361f9164f",
  "sub": "95b11417-f18f-457f-8804-68e361f9164f",
  "aud": "your-application",
  "exp": 1703980800,
  "iat": 1703894400,
  "environment_id": "95b11417-f18f-457f-8804-68e361f9164f",
  "scopes": ["read:profile", "write:wallets"],
  "lists": ["premium", "beta"],
  "verified_credentials": [
    {
      "type": "email",
      "value": "user@example.com"
    },
    {
      "type": "wallet",
      "value": "0x742d35Cc6bF4532a35b8c5F9D4476D6a6B8C4aF6"
    }
  ]
}
```

### 3. Scope-Based Authorization

Dynamic.xyz implements scope-based access control:

**Common Scopes**:
- `read:profile`: Read user profile information
- `write:profile`: Update user profile
- `read:wallets`: Access user's wallet information
- `write:wallets`: Modify user's wallets
- `admin`: Administrative privileges
- `requiresAdditionalAuth`: Requires additional verification (MFA)

**Implementation Example**:
```csharp
public async Task<IActionResult> GetUserProfile()
{
    var tokenResult = await _jwtService.ValidateTokenAsync(Request.Headers.Authorization);
    
    if (tokenResult.IsFailure)
        return Unauthorized();
        
    if (!tokenResult.Value.Scopes.Contains("read:profile"))
        return Forbid();
        
    if (tokenResult.Value.Scopes.Contains("requiresAdditionalAuth"))
        return BadRequest("Additional authentication required");
    
    // Process request...
}
```

---

## External Authentication Integration

### 1. Bring Your Own Authentication (Enterprise)

Dynamic.xyz supports integration with existing authentication systems through external JWT providers.

**Requirements**:
- Enterprise plan subscription
- JWKS endpoint for public key distribution
- JWT compliance with Dynamic.xyz specifications

**Integration Process**:

1. **Configure Provider**: Set up external authentication provider in Dynamic.xyz dashboard
2. **JWKS Endpoint**: Provide public JWKS endpoint URL for token verification
3. **Issuer Validation**: Configure exact `iss` claim value for validation
4. **Subject Mapping**: Map external user IDs to Dynamic.xyz user records
5. **Claim Mapping**: Define additional claims to import (email, roles, etc.)

**JWT Requirements for External Providers**:
```json
{
  "iss": "https://your-auth-provider.com",     // Required: Exact issuer value
  "sub": "external-user-id-123",               // Required: External user ID
  "exp": 1703980800,                           // Required: Token expiration
  "iat": 1703894400,                           // Recommended: Issued at time
  "email": "user@example.com",                 // Optional: User email
  "name": "John Doe",                          // Optional: User display name
  "roles": ["admin", "user"]                   // Optional: User roles
}
```

### 2. Integration Workflow

```mermaid
sequenceDiagram
    participant Client
    participant YourAuth as Your Auth System
    participant Dynamic
    participant Backend
    
    Client->>YourAuth: Authenticate
    YourAuth->>Client: Return JWT
    Client->>Dynamic: Exchange JWT
    Dynamic->>YourAuth: Verify JWT via JWKS
    YourAuth->>Dynamic: Public Key
    Dynamic->>Dynamic: Validate & Map User
    Dynamic->>Client: Return Dynamic JWT
    Client->>Backend: Use Dynamic JWT
```

**Benefits**:
- Maintain existing user management systems
- Leverage Dynamic.xyz's web3 functionality
- Seamless user experience
- Unified authentication flow

---

## Multi-Factor Authentication (MFA)

### 1. MFA Support

Dynamic.xyz supports multiple MFA methods:

**Supported Methods**:
- **TOTP (Time-based OTP)**: Google Authenticator, Authy, etc.
- **WebAuthn/Passkeys**: Hardware security keys, biometric authentication
- **SMS**: Phone number verification (region-specific)
- **Email**: Email-based verification codes

### 2. MFA Implementation

**Scope-Based MFA Detection**:
```csharp
public async Task<IActionResult> SecureOperation()
{
    var tokenResult = await _jwtService.ValidateTokenAsync(authToken);
    
    if (tokenResult.Value.Scopes.Contains("requiresAdditionalAuth"))
    {
        return BadRequest(new 
        { 
            error = "mfa_required",
            message = "Multi-factor authentication required",
            mfa_methods = new[] { "totp", "webauthn", "sms" }
        });
    }
    
    // Proceed with secure operation...
}
```

**MFA Configuration Options**:
- **Mandatory MFA**: Require MFA for all users
- **Conditional MFA**: MFA based on risk assessment
- **Grace Period**: Allow MFA setup time for new users
- **Backup Codes**: Provide recovery options

---

## Security Best Practices

### 1. Token Security

**Storage & Handling**:
- ✅ Store tokens in httpOnly cookies for web applications
- ✅ Use secure, encrypted storage for mobile applications
- ✅ Implement token refresh mechanisms
- ✅ Clear tokens on logout/session end
- ❌ Never store tokens in localStorage or sessionStorage
- ❌ Never transmit tokens via GET parameters
- ❌ Never log tokens in application logs

**Network Security**:
- ✅ Always use HTTPS/TLS 1.2+ for token transmission
- ✅ Implement proper CORS policies
- ✅ Use Content Security Policy (CSP) headers
- ✅ Implement request rate limiting

### 2. Environment Security

**Production Checklist**:
- [ ] Separate API tokens for each environment (dev/staging/prod)
- [ ] Regular token rotation schedule (quarterly)
- [ ] Monitoring and alerting for unusual API activity
- [ ] Proper secret management (Azure Key Vault, AWS Secrets Manager, etc.)
- [ ] Network security controls (firewalls, VPNs)
- [ ] Regular security audits and vulnerability assessments

**Development Security**:
- [ ] Never commit secrets to version control
- [ ] Use environment variables for all configuration
- [ ] Implement proper .gitignore rules
- [ ] Regular dependency updates and security patches
- [ ] Code review processes for authentication changes

### 3. Monitoring & Alerting

**Key Metrics to Monitor**:
- Authentication failure rates
- Token validation errors
- API rate limit breaches
- Unusual access patterns
- Failed MFA attempts
- Token refresh rates

**Alerting Thresholds**:
```yaml
authentication_failures:
  threshold: 100 failures/hour
  action: alert_security_team

invalid_tokens:
  threshold: 50 invalid_tokens/hour
  action: alert_development_team

rate_limit_breaches:
  threshold: 10 breaches/hour
  action: alert_operations_team
```

---

## Rate Limiting

### 1. API Rate Limits

Dynamic.xyz implements rate limiting to ensure service stability and prevent abuse.

**Default Limits** (may vary by plan):
- **Authentication API**: 100 requests/minute per IP
- **User Management API**: 1000 requests/hour per API token
- **Wallet Operations**: 500 requests/hour per user
- **JWKS Endpoint**: 60 requests/minute (cacheable)

### 2. Rate Limit Handling

**HTTP Headers**:
```http
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 999
X-RateLimit-Reset: 1640995200
Retry-After: 3600
```

**Implementation Example**:
```csharp
public async Task<Result<T>> HandleApiCallAsync<T>(Func<Task<T>> apiCall)
{
    try
    {
        return await apiCall();
    }
    catch (HttpRequestException ex) when (ex.Message.Contains("429"))
    {
        // Parse retry-after header
        var retryAfter = ParseRetryAfterHeader(ex);
        
        // Implement exponential backoff
        await Task.Delay(TimeSpan.FromSeconds(retryAfter));
        
        // Retry request
        return await apiCall();
    }
}
```

**Best Practices**:
- Implement exponential backoff for rate-limited requests
- Cache responses when possible to reduce API calls
- Monitor rate limit headers in responses
- Distribute API calls across time periods
- Consider upgrading plan if consistently hitting limits

---

## Troubleshooting Common Issues

### 1. JWT Validation Failures

**Common Causes & Solutions**:

| Error | Cause | Solution |
|-------|--------|----------|
| `Invalid signature` | Wrong public key or algorithm | Verify JWKS endpoint and RS256 usage |
| `Token expired` | Token past expiration time | Implement token refresh logic |
| `Invalid issuer` | Incorrect `iss` claim | Verify environment ID in issuer URL |
| `Invalid audience` | Mismatched `aud` claim | Configure correct audience value |
| `Clock skew` | Time synchronization issues | Implement clock skew tolerance (5 minutes) |

### 2. API Authentication Issues

**Debugging Steps**:

1. **Verify Token Format**:
   ```bash
   # Token should start with 'dyn_' and be 59 characters total
   echo $DYNAMIC_API_TOKEN | wc -c
   ```

2. **Test Token Validity**:
   ```bash
   curl -H "Authorization: Bearer $DYNAMIC_API_TOKEN" \
        https://app.dynamic.xyz/api/v0/environments/$ENVIRONMENT_ID
   ```

3. **Check Environment Configuration**:
   - Verify environment ID matches token
   - Confirm API access is enabled
   - Check rate limiting status

### 3. Common Error Responses

```json
// Unauthorized (401)
{
  "error": "unauthorized",
  "message": "Invalid or missing authorization token"
}

// Forbidden (403)
{
  "error": "forbidden", 
  "message": "Insufficient permissions for requested resource"
}

// Rate Limited (429)
{
  "error": "rate_limited",
  "message": "Too many requests",
  "retry_after": 3600
}

// Invalid Request (400)
{
  "error": "bad_request",
  "message": "Invalid request parameters",
  "details": {
    "field": "userId",
    "issue": "Invalid UUID format"
  }
}
```

---

## Compliance & Standards

### 1. Security Standards

**Supported Standards**:
- **OAuth 2.0**: Industry-standard authorization framework
- **OpenID Connect**: Identity layer on top of OAuth 2.0
- **JWT (RFC 7519)**: JSON Web Token standard
- **JWS (RFC 7515)**: JSON Web Signature standard
- **JWK (RFC 7517)**: JSON Web Key standard
- **WebAuthn**: Web Authentication standard for passkeys

### 2. Compliance Features

**SOC 2 Type II**: Security, availability, and confidentiality controls
**GDPR**: European data protection regulation compliance
**CCPA**: California Consumer Privacy Act compliance
**Data Residency**: Geographic data storage options
**Encryption**: End-to-end encryption for sensitive data

### 3. Audit & Logging

**Audit Trail Features**:
- User authentication events
- API access logs
- Configuration changes
- Token creation/rotation
- Failed authentication attempts
- Suspicious activity detection

**Log Retention**:
- Security logs: 2 years minimum
- API access logs: 90 days default
- Authentication events: 1 year minimum
- Configuration changes: Indefinite

---

## Migration Guide

### 1. Migrating from V3 to V4

Dynamic.xyz SDK V4 introduces breaking changes requiring migration:

**Key Changes**:
- New authentication flow
- Updated JWT claim structure
- Enhanced security features
- Improved error handling

**Migration Steps**:
1. Update SDK dependencies to V4
2. Update authentication logic
3. Test JWT validation changes
4. Update error handling
5. Verify all integrations work correctly

### 2. API Version Migration

When migrating between API versions:

1. **Review Changelog**: Check for breaking changes
2. **Update Base URLs**: Use versioned endpoints
3. **Test in Staging**: Validate all functionality
4. **Gradual Rollout**: Deploy incrementally
5. **Monitor Metrics**: Watch for issues

## Support & Resources

### 1. Getting Help

**Community Support**:
- [Slack Community](https://dynamic.xyz/slack)
- [GitHub Discussions](https://github.com/dynamic-labs/)
- Documentation and guides

**Enterprise Support**:
- Dedicated support channels
- Technical account managers
- Priority issue resolution
- Custom integration assistance

### 2. Additional Resources

- [Dynamic.xyz Security Whitepaper](https://dynamic.xyz/security)
- [API Reference Documentation](https://docs.dynamic.xyz/api-reference)
- [SDK Documentation](https://docs.dynamic.xyz/sdk)
- [Integration Examples](https://github.com/dynamic-labs/examples)

**Contact Information**:
- **General Inquiries**: [hello@dynamic.xyz](mailto:hello@dynamic.xyz)
- **Security Issues**: [security@dynamic.xyz](mailto:security@dynamic.xyz)
- **Enterprise Sales**: [enterprise@dynamic.xyz](mailto:enterprise@dynamic.xyz)
- **Founders**: [founders@dynamic.xyz](mailto:founders@dynamic.xyz)