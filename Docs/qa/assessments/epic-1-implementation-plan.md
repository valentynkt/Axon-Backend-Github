# Epic 1: Dynamic.xyz Authentication API Contracts - Implementation Plan

## Executive Summary

**Epic**: Dynamic.xyz Authentication Integration (S1 - API Contracts)  
**Duration**: 2-3 weeks  
**Risk Level**: MEDIUM (manageable with proper sequencing)  
**Success Criteria**: All 5 stories implemented with CONCERNS resolved to PASS status

## Phase-Based Implementation Sequence

### 🏗️ **PHASE 1: FOUNDATION (Week 1, Days 1-3)**
**Parallel Development Strategy**

#### **Story 1.5: Common Validation Framework** 
**Status**: PASS → **START IMMEDIATELY**
- **Timeline**: 1-2 days
- **Dependencies**: None
- **Risk**: LOW - No blocking issues
- **Parallel Work**: Can be developed alongside Story 1.1

**Implementation Tasks**:
1. Create shared DTO infrastructure
2. Establish BaseValidator patterns  
3. Set up JSON serialization configuration
4. Implement primitive type strategy
5. Create comprehensive unit tests

**Success Criteria**:
- All shared components available for other stories
- JSON serialization working for complex Dictionary<string, object> types
- Validation framework ready for endpoint validators

#### **Story 1.1: Base Identity Endpoints** 
**Status**: CONCERNS → **RESOLVE PATTERN, THEN IMPLEMENT**
- **Timeline**: 2-3 days (including pattern resolution)
- **Dependencies**: Pattern consistency resolution required
- **Risk**: MEDIUM - Dependency injection pattern decision needed

**Risk Mitigation Strategy**:

```csharp
// RECOMMENDED RESOLUTION: Abstract Method Pattern (Consistent with Chat Command)
public abstract class BaseIdentityCommandEndpoint<TRequest, TResponse, TCommand, TDomainResult>
    : BaseMappedEndpoint<TRequest, TResponse>
{
    protected BaseIdentityCommandEndpoint(ILogger logger) : base(logger) { }
    
    protected abstract Task<Result<TDomainResult, Error>> ExecuteCommand(TCommand command, CancellationToken ct);
    // Concrete implementations inject IMediator as needed
}

public abstract class BaseIdentityQueryEndpoint<TRequest, TResponse, TQuery, TDomainResult>
    : BaseMappedEndpoint<TRequest, TResponse>  
{
    protected BaseIdentityQueryEndpoint(ILogger logger) : base(logger) { }
    
    protected abstract Task<Result<TDomainResult, Error>> ExecuteQuery(TQuery query, CancellationToken ct);
    // Consistent with Command pattern - concrete implementations handle MediatR
}
```

**Implementation Tasks**:
1. **Day 1**: Resolve dependency injection pattern with team
2. **Day 2**: Implement both base endpoint classes
3. **Day 3**: Create specialized testing approach and validation

**Success Criteria**:
- Consistent pattern across Command and Query base classes
- Perfect inheritance from BaseMappedEndpoint
- All abstract methods properly defined
- Pattern decision documented for future endpoints

### 🔐 **PHASE 2: SECURE ENDPOINTS (Week 1-2, Days 4-8)**
**Sequential Development with Security Planning**

#### **Story 1.3: Get Current User Endpoint**
**Status**: PASS → **IMPLEMENT FIRST (Lower Risk)**
- **Timeline**: 1-2 days
- **Dependencies**: Stories 1.1 (base classes), 1.5 (DTOs)  
- **Risk**: LOW - No security concerns

**Why First**: 
- PASS status with minimal issues
- Establishes GET endpoint patterns
- Validates base class inheritance
- Provides confidence before tackling authentication endpoints

**Implementation Tasks**:
1. Create CurrentUserResponseDto and UserProfileDto
2. Implement GetCurrentUserEndpoint with proper inheritance
3. Set up EmptyRequest pattern
4. Create comprehensive tests including metadata serialization
5. Validate JWT header extraction (stub implementation)

#### **Story 1.2: Exchange Token Endpoint**
**Status**: CONCERNS → **IMPLEMENT WITH S2 PLANNING**
- **Timeline**: 2-3 days (including security documentation)
- **Dependencies**: Stories 1.1, 1.5, 1.3 patterns established
- **Risk**: HIGH - Critical authentication endpoint

**Risk Mitigation Strategy**:

**S1 Implementation Focus**:
```csharp
// S1: Format validation only with comprehensive TODO markers
private async Task<Result<string, Error>> ExtractAndValidateJwt(HttpContext context)
{
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        return Result<string>.Failure(Error.Unauthorized("Missing or invalid Authorization header"));
    
    var jwt = authHeader.Substring("Bearer ".Length).Trim();
    var segments = jwt.Split('.');
    
    if (segments.Length != 3)
        return Result<string>.Failure(Error.Validation("Invalid JWT format"));
    
    // TODO S2: Implement full JWKS validation
    // TODO S2: Add signature verification  
    // TODO S2: Validate expiry and issuer claims
    // TODO S2: Add rate limiting
    
    return Result<string>.Success(jwt);
}
```

**Parallel S2 Planning Tasks**:
1. **Day 1**: Research Dynamic.xyz JWKS endpoint and token structure
2. **Day 2**: Document complete JWT validation requirements 
3. **Day 3**: Design rate limiting strategy and configuration
4. **Throughout**: Create security testing scenarios

**Implementation Tasks**:
1. Create ExchangeToken DTOs with primitive types
2. Implement ExchangeTokenEndpoint with comprehensive security TODOs
3. Set up JWT header extraction and format validation
4. Create mock response with FRD-compliant data
5. Document S2 security requirements in detail
6. Create security test scenarios for future penetration testing

#### **Story 1.4: Dynamic Webhook Endpoint**
**Status**: CONCERNS → **IMPLEMENT WITH ARCHITECTURE PLANNING**  
- **Timeline**: 2-3 days (including infrastructure planning)
- **Dependencies**: Stories 1.1, 1.5 established
- **Risk**: HIGH - Anonymous endpoint with async processing needs

**Risk Mitigation Strategy**:

**S1 Implementation Focus**:
```csharp
// S1: Event validation with signature header requirement (no validation)
[HttpPost("/api/v1/webhooks/dynamic")]
public async Task<IActionResult> ProcessDynamicWebhook([FromBody] DynamicWebhookEventDto eventDto)
{
    // Require signature header (validation in S2)
    if (!Request.Headers.ContainsKey("X-Dynamic-Signature"))
        return BadRequest("X-Dynamic-Signature header required");
    
    // TODO S2: Implement HMAC-SHA256 signature validation
    // TODO S3: Queue for background processing
    // TODO S3: Add event deduplication
    
    // S1: Immediate response with acknowledgment
    return Accepted(new DynamicWebhookResponseDto
    {
        Received = true,
        ProcessedAt = DateTime.UtcNow,
        SyncScheduled = eventDto.EventName.Contains("users.") || eventDto.EventName.Contains("wallets."),
        Acknowledgment = new WebhookAcknowledgmentDto { /* ... */ }
    });
}
```

**Parallel Infrastructure Planning**:
1. **Day 1**: Design webhook signature validation approach
2. **Day 2**: Plan background processing architecture (hosted services)
3. **Day 3**: Design event storage and reliability strategy

**Implementation Tasks**:
1. Create webhook DTOs with comprehensive validation
2. Implement ProcessDynamicWebhookEndpoint with security headers
3. Set up event type whitelist validation
4. Create 202 Accepted response pattern
5. Document S2 signature validation requirements
6. Plan S3 background processing infrastructure

### 🧪 **PHASE 3: INTEGRATION & VALIDATION (Week 2-3, Days 9-12)**

#### **Integration Testing & Quality Gates**
- **Timeline**: 2-3 days
- **Focus**: End-to-end validation and CONCERNS resolution

**Integration Tasks**:
1. **Cross-Story Integration**:
   - Validate DTO reuse across endpoints
   - Test base class inheritance patterns
   - Verify consistent error handling

2. **Security Documentation**:
   - Complete S2 security requirements document
   - Create penetration testing scenarios
   - Document rate limiting and caching strategies

3. **Quality Gate Resolution**:
   - Re-run QA reviews for CONCERNS-gated stories
   - Validate all acceptance criteria met
   - Ensure build passes with no warnings

4. **OpenAPI Documentation**:
   - Generate complete API documentation
   - Validate example requests/responses
   - Ensure proper authentication header documentation

## Risk Mitigation Matrix

| Story | Original Risk | Mitigation Strategy | Timeline Impact |
|-------|---------------|-------------------|-----------------|
| **1.1** | Pattern inconsistency | Abstract method standardization | +1 day |
| **1.2** | Security gaps | S2 planning parallel to implementation | +1 day |
| **1.4** | Webhook security | Infrastructure planning + signature header | +1 day |
| **1.3** | Minor performance | Caching documentation only | +0 days |
| **1.5** | None | Proceed as planned | +0 days |

## Parallel Development Opportunities

### **Week 1 Parallel Tracks**:
- **Track A**: Story 1.5 (Common Framework) - Developer 1
- **Track B**: Story 1.1 (Base Endpoints) - Developer 2  
- **Track C**: S2 Security Research - Security specialist

### **Week 2 Parallel Tracks**:
- **Track A**: Story 1.3 (Get User) - Developer 1
- **Track B**: Story 1.2 (Exchange Token) - Developer 2
- **Track C**: Story 1.4 (Webhook) - Developer 3
- **Track D**: S2 Architecture Planning - Architect

## Success Metrics & Quality Gates

### **Phase 1 Exit Criteria**:
- ✅ Story 1.5: PASS status maintained - All shared components ready
- ✅ Story 1.1: CONCERNS → PASS - Pattern consistency resolved
- ✅ Foundation ready for endpoint implementation

### **Phase 2 Exit Criteria**:  
- ✅ Story 1.3: PASS status maintained - GET pattern established
- ✅ Story 1.2: CONCERNS → PASS - S2 security requirements documented
- ✅ Story 1.4: CONCERNS → PASS - Infrastructure architecture planned
- ✅ All endpoints compile and pass basic integration tests

### **Phase 3 Exit Criteria**:
- ✅ All 5 stories achieve PASS quality gate status
- ✅ Complete S2 security planning documentation
- ✅ OpenAPI documentation generated and validated
- ✅ Integration tests passing across all endpoints
- ✅ Build pipeline success with no warnings

## Resource Allocation

### **Team Composition** (Recommended):
- **2-3 Developers**: Endpoint implementation
- **1 Security Specialist**: S2 requirements and research  
- **1 Test Engineer**: Integration testing and quality validation
- **1 Technical Lead**: Architecture decisions and code review

### **Timeline Flexibility**:
- **Minimum**: 10 days (aggressive, single developer)
- **Optimal**: 12-15 days (parallel development) 
- **Conservative**: 18-21 days (thorough documentation and testing)

## Next Steps

1. **Immediate** (Today): Resolve Story 1.1 dependency injection pattern
2. **Day 1**: Begin Stories 1.5 and 1.1 in parallel
3. **Day 4**: Start security research for Stories 1.2 and 1.4
4. **Week 2**: Sequential endpoint implementation with parallel S2 planning
5. **Week 3**: Integration, documentation, and quality gate resolution

## Risk Monitoring

### **Weekly Checkpoints**:
- **Week 1**: Foundation stories completion and pattern consistency
- **Week 2**: Security endpoint implementation and S2 planning progress  
- **Week 3**: Quality gate resolution and documentation completion

### **Escalation Triggers**:
- Any story blocked for >2 days
- Security research revealing major architecture changes needed
- Quality gates not resolving to PASS status
- Build pipeline failures not resolved within 24 hours