# Development Workflow

**Daily development cycle and best practices for feature implementation.**

---

**STATUS**: 🚧 Draft - AI Content Generation Ready
**PRIORITY**: Medium
**LAST_UPDATED**: 2025-09-29

---

## Overview

This guide covers the day-to-day development workflow: from picking up a task to merging code. It focuses on the practical steps developers take while building features.

---

## Daily Development Cycle

### 1. Start of Day
```bash
# Pull latest changes from dev
git checkout dev
git pull origin dev

# Check for any dependency updates
dotnet restore

# Verify build still works
dotnet build

# Run tests to ensure everything is green
dotnet test --filter Category!=E2E
```

### 2. Pick Up a Task
- Check project board (GitHub Projects / BMAD stories)
- Assign task to yourself
- Read acceptance criteria
- Review relevant module documentation

### 3. Create Feature Branch
```bash
# Branch naming: feature/module-name-short-description
git checkout -b feature/identity-wallet-verification

# For bugs: fix/module-name-bug-description
git checkout -b fix/chat-message-timestamp

# For refactoring: refactor/area-description
git checkout -b refactor/identity-repository-pattern
```

**See [Git Workflow](./git-workflow.md) for branch naming conventions**

---

## Feature Implementation Workflow

### Phase 1: Research & Design

**Before writing code:**

1. **Review Related Documentation**
   - Module docs: `Docs/ENGINEERING/modules/{module}/`
   - Pattern guides: `Docs/ENGINEERING/guides/patterns/`
   - Library docs: `Docs/Libraries/{library}/`

2. **Identify Patterns to Use**
   - CQRS command or query?
   - New aggregate or extending existing?
   - Domain events needed?
   - External API integration?

3. **Check Existing Implementations**
   ```bash
   # Find similar features
   grep -r "CommandHandler" src/Modules/Identity/Application/

   # Review existing aggregates
   ls src/Modules/Identity/Domain/Aggregates/
   ```

### Phase 2: Implementation (TDD Approach)

**1. Write Failing Tests**
```bash
# Create test file first
touch tests/Modules/Identity/Domain/AxonPrincipalTests.cs

# Write domain tests
# Run and watch them fail
dotnet test tests/Modules/Identity/Domain/ --filter ClassName~AxonPrincipal
```

**2. Implement Domain Logic**
```bash
# Add/modify domain entity
vim src/Modules/Identity/Domain/Aggregates/AxonPrincipal/AxonPrincipal.cs

# Run tests - see them pass
dotnet test tests/Modules/Identity/Domain/ --filter ClassName~AxonPrincipal
```

**3. Implement Application Layer**
```bash
# Create command/query
vim src/Modules/Identity/Application/Commands/VerifyWallet/VerifyWalletCommand.cs

# Create handler
vim src/Modules/Identity/Application/Commands/VerifyWallet/VerifyWalletCommandHandler.cs

# Create validator
vim src/Modules/Identity/Application/Commands/VerifyWallet/VerifyWalletCommandValidator.cs

# Test application layer
dotnet test tests/Modules/Identity/Application/ --filter ClassName~VerifyWallet
```

**4. Implement Infrastructure**
```bash
# Repository changes (if needed)
vim src/Modules/Identity/Infrastructure/Persistence/Repositories/AxonPrincipalRepository.cs

# External service integration (if needed)
vim src/Modules/Identity/Infrastructure/Services/WalletVerificationService.cs
```

**5. Add API Endpoint**
```bash
# Create endpoint
vim src/Api/Endpoints/V1/Identity/VerifyWallet/VerifyWalletEndpoint.cs

# Create contracts
vim src/Api/Contracts/V1/Identity/VerifyWalletRequest.cs
vim src/Api/Contracts/V1/Identity/VerifyWalletResponse.cs
```

### Phase 3: Testing

**Run All Test Levels:**
```bash
# 1. Unit tests (fast)
dotnet test tests/Modules/Identity/Domain/

# 2. Application tests (with mocks)
dotnet test tests/Modules/Identity/Application/

# 3. Integration tests (with TestContainers)
dotnet test tests/Modules/Identity/Integration/

# 4. E2E tests (full stack)
dotnet test tests/Modules/Identity/E2E/

# 5. Run all tests
dotnet test
```

**Test Coverage:**
```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# View coverage (if reportgenerator installed)
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage
open coverage/index.html
```

---

## Hot Reload / Watch Mode

### API Hot Reload
```bash
# Run with hot reload enabled
dotnet watch --project src/Api

# Make code changes
# API automatically restarts
# Refresh Swagger UI to see changes
```

### Test Watch Mode
```bash
# Watch domain tests
dotnet watch test tests/Modules/Identity/Domain/

# Make code changes
# Tests automatically re-run
```

---

## Database Migrations

### When to Create a Migration

- Adding/modifying domain entities
- Changing entity configurations
- Adding/removing owned entities
- Index changes

### Create Migration (Identity Module)
```bash
cd src/Modules/Identity/Infrastructure

# Create migration with descriptive name
dotnet ef migrations add AddWalletVerificationStatus \
  --startup-project ../../Api/Axon.Api.csproj \
  --context IdentityWriteDbContext

# Review generated migration
ls Migrations/

# Apply migration locally
dotnet ef database update --startup-project ../../Api/Axon.Api.csproj
```

### Create Migration (Chat Module)
```bash
cd src/Modules/Chat/Infrastructure

dotnet ef migrations add AddMessageReactions \
  --startup-project ../../Api/Axon.Api.csproj \
  --context ChatWriteDbContext

dotnet ef database update --startup-project ../../Api/Axon.Api.csproj
```

### Migration Best Practices

✅ **DO:**
- Use descriptive migration names
- Review SQL before applying
- Test rollback capability
- Include seed data if needed

❌ **DON'T:**
- Modify existing migrations (already applied)
- Create migrations for every tiny change
- Skip testing migrations locally

---

## Code Quality Checks

### Before Committing

```bash
# 1. Format code
dotnet format

# 2. Build in Release mode (warnings as errors)
dotnet build --configuration Release

# 3. Run all tests
dotnet test

# 4. Check for security vulnerabilities (optional)
dotnet list package --vulnerable
```

### Linting & Analysis
```bash
# Run code analyzers
dotnet build /p:EnforceCodeStyleInBuild=true

# Check for nullable reference warnings
dotnet build /p:TreatWarningsAsErrors=true
```

---

## Debugging Techniques

### Debug in IDE

**Rider:**
1. Set breakpoint (Cmd+F8)
2. Debug configuration → Api
3. Start debugging (Ctrl+D)
4. Make API request via Swagger/Postman
5. Inspect variables

**VS Code:**
1. Set breakpoint (click line number)
2. Run & Debug → .NET Core Launch (Api)
3. F5 to start debugging

### Debug Tests

```bash
# Run single test with verbose output
dotnet test --filter FullyQualifiedName~VerifyWalletCommandHandlerTests.Handle_ValidSignature_ReturnsSuccess --logger:"console;verbosity=detailed"

# Debug test in IDE
# Right-click test → Debug
```

### Logging

```csharp
// Use structured logging
_logger.LogInformation(
    "Verifying wallet signature for {WalletAddress} and principal {PrincipalId}",
    walletAddress, principalId);

// View logs in console or Application Insights
```

---

## Common Workflows

### Adding a New Command

1. Create command record: `Commands/{Feature}/{Feature}Command.cs`
2. Create handler: `Commands/{Feature}/{Feature}CommandHandler.cs`
3. Create validator: `Commands/{Feature}/{Feature}CommandValidator.cs`
4. Create domain tests: `tests/.../Domain/`
5. Create application tests: `tests/.../Application/Commands/{Feature}/`
6. Create endpoint: `src/Api/Endpoints/V1/{Module}/{Feature}/`
7. Create contracts: `src/Api/Contracts/V1/{Module}/`
8. Test via Swagger UI

**See**: [CQRS Patterns](../patterns/cqrs.md) for detailed examples

### Adding a New Query

1. Create query record: `Queries/{Feature}/{Feature}Query.cs`
2. Create handler (read-only): `Queries/{Feature}/{Feature}QueryHandler.cs`
3. Create validator: `Queries/{Feature}/{Feature}QueryValidator.cs`
4. Create application tests: `tests/.../Application/Queries/{Feature}/`
5. Add caching (if needed): Use `IMemoryCache` in handler
6. Create endpoint: `src/Api/Endpoints/V1/{Module}/{Feature}/`
7. Test via Swagger UI

### Extending an Aggregate

1. Add method to aggregate: `Domain/Aggregates/{Aggregate}/{Aggregate}.cs`
2. Write domain tests: Verify invariants, business rules
3. Update handler to use new method
4. Test end-to-end

---

## Performance Optimization

### Database Query Optimization

```bash
# Enable query logging
# appsettings.Development.json
"Logging": {
  "LogLevel": {
    "Microsoft.EntityFrameworkCore.Database.Command": "Information"
  }
}

# Review generated SQL in logs
# Look for N+1 queries
# Add .Include() for related entities
```

### Caching Strategy

```csharp
// Add caching to query handler
public class GetUserQueryHandler : IQueryHandler<GetUserQuery, Result<UserDto, Error>>
{
    private readonly IMemoryCache _cache;

    public async Task<Result<UserDto, Error>> Handle(GetUserQuery query, CancellationToken ct)
    {
        var cacheKey = $"user:{query.UserId}";

        if (_cache.TryGetValue(cacheKey, out UserDto cachedUser))
            return Result.Success<UserDto, Error>(cachedUser);

        // ... fetch from database ...

        _cache.Set(cacheKey, user, TimeSpan.FromMinutes(5));
        return Result.Success<UserDto, Error>(user);
    }
}
```

---

## End of Day Checklist

- [ ] All tests passing locally
- [ ] Code formatted (`dotnet format`)
- [ ] Build succeeds in Release mode
- [ ] Migrations applied and tested
- [ ] Changes committed with good messages
- [ ] Branch pushed to remote
- [ ] PR created (if feature complete)
- [ ] Documentation updated (if needed)

---

## Related Documentation

- **Git Workflow** → [git-workflow.md](./git-workflow.md) - Commits, PRs, code review
- **Testing Workflow** → [testing-workflow.md](./testing-workflow.md) - Test organization, debugging
- **Debugging Guide** → [debugging.md](./debugging.md) - Common issues, troubleshooting
- **CQRS Patterns** → [../patterns/cqrs.md](../patterns/cqrs.md) - Command/query implementation
- **Domain Modeling** → [../patterns/domain-modeling.md](../patterns/domain-modeling.md) - Aggregates, Result<T>

---

**Last Updated**: 2025-09-29