# Debugging Guide

**Common issues, troubleshooting steps, and debugging techniques.**

---

**STATUS**: 🚧 Draft - AI Content Generation Ready
**PRIORITY**: Medium
**LAST_UPDATED**: 2025-09-29

---

## Overview

This guide covers common development issues, debugging techniques, and troubleshooting steps for Axon Backend.

---

## Build Errors

### Error: `.NET 10 SDK not found`

**Symptom:**
```bash
error MSB4236: The SDK 'Microsoft.NET.Sdk' specified could not be found.
```

**Solution:**
```bash
# Verify installed SDKs
dotnet --list-sdks

# Install .NET 10 preview
# Download from: https://dotnet.microsoft.com/download/dotnet/10.0

# Verify installation
dotnet --version  # Should show 10.0.0-preview.X

# If multiple SDKs, check global.json
cat global.json
```

### Error: `Package restore failed`

**Symptom:**
```bash
error NU1301: Unable to load the service index
error NU1101: Unable to find package
```

**Solution:**
```bash
# 1. Clear NuGet cache
dotnet nuget locals all --clear

# 2. Restore packages
dotnet restore

# 3. If still failing, check NuGet config
cat ~/.nuget/NuGet/NuGet.Config

# 4. Verify internet connection
ping nuget.org
```

### Error: `Analyzer errors`

**Symptom:**
```bash
error CA1822: Member 'Method' does not access instance data
warning CS8618: Non-nullable property is uninitialized
```

**Solution:**
```bash
# Option 1: Fix the warnings (preferred)
# Follow analyzer suggestions

# Option 2: Suppress specific warnings (temporary)
# Add to .editorconfig or Directory.Build.props
<NoWarn>$(NoWarn);CA1822;CS8618</NoWarn>

# Option 3: Disable analyzers temporarily (not recommended)
dotnet build /p:EnableNETAnalyzers=false
```

### Error: `Warnings treated as errors in Release`

**Symptom:**
```bash
error : warning CS8602: Dereference of a possibly null reference.
```

**Solution:**
```bash
# Warnings are errors in Release mode (by design)
# Fix nullable reference warnings

# Development: build in Debug mode
dotnet build

# Release: fix all warnings first
dotnet build --configuration Release
```

---

## Database Issues

### Error: `Connection refused to localhost:5432`

**Symptom:**
```bash
Npgsql.NpgsqlException: Connection refused
```

**Solution:**
```bash
# 1. Check if PostgreSQL is running
docker ps | grep postgres

# 2. If not running, start container
docker start axon-postgres

# 3. If container doesn't exist, create it
docker run --name axon-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=axon_dev \
  -p 5432:5432 \
  -d postgres:16

# 4. Verify connection
docker exec -it axon-postgres psql -U postgres -c "SELECT 1"
```

### Error: `Database does not exist`

**Symptom:**
```bash
Npgsql.PostgresException: 3D000: database "axon_dev" does not exist
```

**Solution:**
```bash
# Create database
docker exec -it axon-postgres psql -U postgres -c "CREATE DATABASE axon_dev;"

# Verify
docker exec -it axon-postgres psql -U postgres -c "\l" | grep axon_dev
```

### Error: `Migration already applied`

**Symptom:**
```bash
error: A migration with the name 'AddWalletVerification' already exists
```

**Solution:**
```bash
# Option 1: Remove migration (if not applied)
cd src/Modules/Identity/Infrastructure
dotnet ef migrations remove --startup-project ../../Api/

# Option 2: Create new migration with different name
dotnet ef migrations add AddWalletVerificationV2 --startup-project ../../Api/

# Option 3: Revert database, remove migration, recreate
dotnet ef database update 0 --startup-project ../../Api/
dotnet ef migrations remove --startup-project ../../Api/
dotnet ef migrations add AddWalletVerification --startup-project ../../Api/
```

### Error: `Concurrency conflict`

**Symptom:**
```bash
Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
Database operation expected to affect 1 row(s) but actually affected 0 row(s).
```

**Solution:**
```csharp
// Implement retry logic in application code
public async Task<Result<Unit, Error>> Handle(UpdateCommand cmd, CancellationToken ct)
{
    const int maxRetries = 3;
    int attempt = 0;

    while (attempt < maxRetries)
    {
        try
        {
            // ... update logic ...
            await _dbContext.SaveChangesAsync(ct);
            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            attempt++;
            if (attempt >= maxRetries)
                return Result.Failure<Unit, Error>(
                    Error.Conflict("Resource was modified by another user"));

            // Refresh entity and retry
            await ex.Entries.Single().ReloadAsync(ct);
        }
    }
}
```

---

## API Issues

### Error: `Port already in use`

**Symptom:**
```bash
System.IO.IOException: Failed to bind to address http://localhost:5000:
address already in use.
```

**Solution:**
```bash
# Option 1: Kill process using port 5000
lsof -ti:5000 | xargs kill -9

# Option 2: Change port in launchSettings.json
vim src/Api/Properties/launchSettings.json
# Change "applicationUrl": "http://localhost:5001;https://localhost:5002"

# Option 3: Stop all dotnet processes
killall dotnet
```

### Error: `Failed to load configuration`

**Symptom:**
```bash
Unhandled exception: System.InvalidOperationException:
Missing configuration 'ConnectionStrings:DefaultConnection'
```

**Solution:**
```bash
# 1. Verify appsettings.Development.json exists
ls src/Api/appsettings.Development.json

# 2. If missing, create it
cat > src/Api/appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=axon_dev;Username=postgres;Password=postgres"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
EOF

# 3. Verify JSON is valid
cat src/Api/appsettings.Development.json | jq .
```

### Error: `Swagger not loading`

**Symptom:**
Swagger UI shows blank page or error 500

**Solution:**
```bash
# 1. Check for XML documentation errors
dotnet build /p:GenerateDocumentationFile=true

# 2. Verify Swagger is enabled in Program.cs
grep -A5 "UseSwagger" src/Api/Program.cs

# 3. Check browser console for errors
# Open developer tools (F12) in browser

# 4. Verify endpoint annotations
# FastEndpoints should have proper Summary/Description attributes
```

---

## Test Failures

### Error: `TestContainers failing to start`

**Symptom:**
```bash
Docker.DotNet.DockerApiException: Docker API responded with status code=InternalServerError
```

**Solution:**
```bash
# 1. Verify Docker is running
docker ps

# 2. Start Docker Desktop

# 3. Clean up old containers
docker container prune -f

# 4. Check Docker resources (Memory/CPU)
# Docker Desktop → Settings → Resources
# Increase memory to 4GB+ if needed
```

### Error: `Integration test database conflicts`

**Symptom:**
```bash
Npgsql.PostgresException: 42P01: relation "users" does not exist
```

**Solution:**
```csharp
// Ensure test fixture creates database before tests
[SetUpFixture]
public class TestFixture
{
    [OneTimeSetUp]
    public async Task Setup()
    {
        // Start TestContainers
        await _postgresContainer.StartAsync();

        // Apply migrations
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        await _postgresContainer.StopAsync();
    }
}
```

### Error: `Test isolation issues`

**Symptom:**
Tests pass individually but fail when run together

**Solution:**
```csharp
// Ensure proper test isolation
[SetUp]
public async Task Setup()
{
    // Clear database before each test
    await _dbContext.Database.EnsureDeletedAsync();
    await _dbContext.Database.MigrateAsync();

    // Or use transactions
    _transaction = await _dbContext.Database.BeginTransactionAsync();
}

[TearDown]
public async Task Teardown()
{
    // Rollback transaction
    await _transaction?.RollbackAsync();
    await _transaction?.DisposeAsync();
}
```

---

## Hot Reload Issues

### Hot Reload Not Working

**Symptom:**
Code changes not reflected, need to restart

**Solution:**
```bash
# 1. Verify hot reload is enabled
dotnet watch --verbose --project src/Api

# 2. Check for unsupported changes
# Hot reload doesn't support:
# - Adding new files
# - Changing method signatures
# - Adding new types

# 3. For unsupported changes, restart manually
# Ctrl+C, then dotnet watch --project src/Api
```

---

## Performance Issues

### Slow API Response Times

**Debugging Steps:**
```bash
# 1. Enable query logging
# appsettings.Development.json
"Logging": {
  "LogLevel": {
    "Microsoft.EntityFrameworkCore.Database.Command": "Information"
  }
}

# 2. Look for N+1 queries in logs
# Bad: Multiple queries for related entities
SELECT * FROM users WHERE id = @p0
SELECT * FROM wallets WHERE user_id = @p0
SELECT * FROM wallets WHERE user_id = @p1
# ...

# 3. Fix with eager loading
var user = await _dbContext.Users
    .Include(u => u.Wallets)  // ✅ Single query
    .FirstOrDefaultAsync(u => u.Id == userId);

# 4. Use profiling tools
# - MiniProfiler
# - Application Insights
# - EF Core query tags
```

### High Memory Usage

**Debugging Steps:**
```bash
# 1. Profile memory usage
dotnet-counters monitor --process-id <pid>

# 2. Look for memory leaks
# - Unreleased event handlers
# - Static collections growing unbounded
# - Unclosed database connections

# 3. Use memory dump analysis
dotnet-dump collect --process-id <pid>
dotnet-dump analyze <dump-file>
> dumpheap -stat
```

---

## IDE-Specific Issues

### Rider Issues

**IntelliSense not working:**
```bash
# 1. Invalidate caches
# File → Invalidate Caches / Restart

# 2. Rebuild solution
# Build → Rebuild Solution

# 3. Restart Rider
```

**Debugger not hitting breakpoints:**
```bash
# 1. Verify build configuration is Debug
# 2. Clean and rebuild
# 3. Delete bin/obj folders
find . -name "bin" -o -name "obj" | xargs rm -rf
dotnet build
```

### VS Code Issues

**C# extension not working:**
```bash
# 1. Restart OmniSharp
# Cmd+Shift+P → "OmniSharp: Restart OmniSharp"

# 2. Reinstall C# Dev Kit extension

# 3. Check .NET SDK path
# Cmd+Shift+P → "Preferences: Open Settings (JSON)"
# Add: "dotnet.dotnetPath": "/usr/local/share/dotnet/dotnet"
```

---

## Debugging Techniques

### Logging

```csharp
// Use structured logging
_logger.LogInformation(
    "Processing command {CommandType} for user {UserId}",
    command.GetType().Name,
    userId);

// Log exceptions with context
_logger.LogError(ex,
    "Failed to verify wallet {WalletAddress} for principal {PrincipalId}",
    walletAddress, principalId);

// View logs in console or Application Insights
```

### Breakpoint Debugging

**Conditional Breakpoints:**
```csharp
// Rider/VS: Right-click breakpoint → Condition
// Break when: userId == "specific-guid"

// VS Code: Right-click breakpoint → Edit Breakpoint → Expression
```

**Logpoint (Breakpoint without pausing):**
```csharp
// Rider: Right-click breakpoint → More → Evaluate and log
// Expression: $"UserId: {userId}, Status: {status}"
```

### Watch Expressions

```csharp
// In debugger, add watch expressions:
// - entity.IsValid()
// - result.IsFailure ? result.Error.Message : "Success"
// - _dbContext.ChangeTracker.Entries().Count()
```

---

## Related Documentation

- **Development Workflow** → [development-workflow.md](./development-workflow.md)
- **Testing Workflow** → [testing-workflow.md](./testing-workflow.md)
- **Getting Started** → [getting-started.md](./getting-started.md)
- **Git Workflow** → [git-workflow.md](./git-workflow.md)

---

**Last Updated**: 2025-09-29