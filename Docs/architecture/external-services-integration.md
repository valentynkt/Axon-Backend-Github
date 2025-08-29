# External Services Integration Guidelines

## Overview
Axon follows the 80/20 rule for external service integration - leverage existing services for 80% of the value with 20% of the effort.

## Dynamic.xyz Authentication Service

### Integration Approach
- **Primary Auth Provider**: All JWT validation delegated to Dynamic.xyz
- **No Custom JWT Logic**: Pure passthrough pattern
- **Service Leverage**: Use their JWKS endpoint, rate limiting, and security monitoring
- **Minimal Wrapper**: Simple service class with single validation method

### Implementation Pattern
```csharp
// Simple service wrapper - no custom validation
public class DynamicAuthService
{
    private readonly HttpClient _httpClient;
    
    public async Task<Result<DynamicUser, Error>> ValidateTokenAsync(string jwt)
    {
        // Pure passthrough to Dynamic.xyz
        var response = await _httpClient.PostAsJsonAsync("/validate", new { token = jwt });
        return response.IsSuccessStatusCode 
            ? await response.Content.ReadFromJsonAsync<DynamicUser>()
            : Error.Unauthorized();
    }
}
```

### Benefits
- **Security**: Leverage battle-tested Dynamic.xyz validation
- **Performance**: Their caching and optimization
- **Maintenance**: No JWT library updates or security patches
- **Simplicity**: <150 lines total integration code

## General External Service Principles

1. **Delegate Complexity**: Let specialized services handle their domain
2. **Thin Wrappers**: Minimal abstraction over external APIs
3. **Fail Fast**: Simple error handling, no complex retry logic beyond HTTP client
4. **Documentation**: Clear service dependencies in appsettings
5. **Health Checks**: Simple availability checks for external services

## Anti-Patterns to Avoid

- ❌ Building custom JWT validation when service provides it
- ❌ Complex abstraction layers over simple HTTP APIs  
- ❌ Reimplementing service features locally
- ❌ Over-engineering for unlikely failure scenarios
- ❌ Custom caching when service handles it

## Foundation Phase Focus

During foundation building:
- Maximum service leverage
- Minimum custom code
- Simple integration patterns
- Clear service boundaries
- Fast time to market