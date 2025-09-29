# Check Library Task

**Agent**: Axon Library Sage
**Purpose**: Check if library solves requirement out-of-box

---

## Task Instructions

### 11 Core Libraries
1. MediatR - CQRS commands/queries
2. FastEndpoints - API endpoints
3. FluentValidation - Request validation
4. EF Core - Persistence/ORM
5. Dynamic Auth - Web3 authentication
6. OpenAI API - AI integration
7. MCP - Model Context Protocol
8. Refit - HTTP client
9. Serilog - Logging
10. Shouldly - Test assertions
11. NUnit - Testing framework

### Process
1. Parse requirement
2. Check library capabilities from `Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md`
3. Determine if library provides solution
4. Return recommendation: LIBRARY / MANUAL / HYBRID

### Output
```yaml
library_check:
  requirement: "Validate wallet signature"
  library_checked: "Dynamic Auth"
  capability_found: true
  recommendation: LIBRARY
  confidence: HIGH
  usage_example: "See Docs/Libraries/dynamic_auth/IMPLEMENTATION_GUIDE.md#wallet-verification"
```

---

## TODO: Full implementation with library capability lookup
