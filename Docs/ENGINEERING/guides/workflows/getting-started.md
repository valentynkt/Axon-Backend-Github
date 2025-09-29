# Getting Started with Axon Backend

**Complete onboarding guide: from zero to running locally in 30 minutes.**

---

**STATUS**: 🚧 Draft - AI Content Generation Ready
**PRIORITY**: High
**LAST_UPDATED**: 2025-09-29

---

## Overview

This guide walks you through setting up your local development environment for Axon Backend. By the end, you'll have:
- ✅ Required tools installed
- ✅ Repository cloned and built successfully
- ✅ Database running and migrated
- ✅ API running locally with Swagger UI
- ✅ Test suite passing

**Estimated time**: 30-45 minutes

---

## Prerequisites

### Required Tools

#### .NET 10 SDK (Preview)
```bash
# Verify .NET 10 is installed
dotnet --version
# Expected: 10.0.0-preview.X

# If not installed, download from:
# https://dotnet.microsoft.com/download/dotnet/10.0
```

#### Docker Desktop
Required for PostgreSQL database and integration tests.

```bash
# Verify Docker is running
docker --version
docker ps  # Should not error

# Download from: https://www.docker.com/products/docker-desktop
```

#### Git
```bash
git --version
# Expected: 2.x or higher
```

#### IDE (Choose One)
- **JetBrains Rider** (Recommended) - Best .NET experience
- **Visual Studio Code** - With C# Dev Kit extension
- **Visual Studio 2022** (17.12+) - Full IDE

---

## Step 1: Clone Repository

```bash
# Clone via HTTPS
git clone https://github.com/valentynkt/Axon-Backend.git
cd Axon-Backend

# Or via SSH (if you have SSH keys configured)
git clone git@github.com:valentynkt/Axon-Backend.git
cd Axon-Backend

# Verify you're on dev branch
git branch
# Should show: * dev
```

---

## Step 2: Environment Setup

### PostgreSQL Database (Docker)

```bash
# Start PostgreSQL container
docker run --name axon-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=axon_dev \
  -p 5432:5432 \
  -d postgres:16

# Verify it's running
docker ps | grep axon-postgres
```

### Configuration Files

```bash
# Copy example configuration (if exists)
cp src/Api/appsettings.Development.example.json src/Api/appsettings.Development.json

# Or create appsettings.Development.json manually
# (See Configuration section below)
```

**appsettings.Development.json** (minimal):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=axon_dev;Username=postgres;Password=postgres"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## Step 3: First Build

```bash
# Restore dependencies
dotnet restore

# Build solution (warnings as errors in Release)
dotnet build

# Expected output: Build succeeded. 0 Warning(s)
```

**Common Build Issues:**
- ❌ `.NET 10 SDK not found` → Install .NET 10 preview
- ❌ `Package restore failed` → Check internet connection, clear NuGet cache
- ❌ `Analyzer errors` → Ensure you have latest .NET 10 preview

---

## Step 4: Database Migrations

```bash
# Apply Identity module migrations
cd src/Modules/Identity/Infrastructure
dotnet ef database update

# Apply Chat module migrations
cd ../../../Chat/Infrastructure
dotnet ef database update

# Verify tables created
docker exec -it axon-postgres psql -U postgres -d axon_dev -c "\dt"
```

---

## Step 5: Run API Locally

```bash
# From solution root
cd /path/to/Axon-Backend

# Run API project
dotnet run --project src/Api

# Expected output:
# Now listening on: http://localhost:5000
# Now listening on: https://localhost:5001
```

### Verify API is Running

```bash
# Test health endpoint
curl http://localhost:5000/health
# Expected: {"status": "Healthy"}

# Open Swagger UI in browser
# https://localhost:5001/swagger
```

**Swagger UI should show:**
- ✅ All endpoint groups (Chat, Identity)
- ✅ OpenAPI schema loaded
- ✅ Try-it-out functionality working

---

## Step 6: Run Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Modules/Identity/Domain/

# Run with coverage (if coverage tools installed)
dotnet test --collect:"XPlat Code Coverage"
```

**Expected Results:**
- ✅ 100+ tests passing
- ✅ 0 failures
- ✅ Test execution < 2 minutes

---

## Step 7: Verify Development Environment

### Checklist

- [ ] .NET 10 SDK installed and verified
- [ ] Docker running with PostgreSQL container
- [ ] Repository cloned, `dev` branch active
- [ ] `dotnet build` succeeds with 0 warnings
- [ ] Database migrations applied (Identity, Chat)
- [ ] API runs locally (ports 5000/5001)
- [ ] Swagger UI accessible at https://localhost:5001/swagger
- [ ] Health check returns "Healthy"
- [ ] Test suite passes (100+ tests)

**If all checkboxes are ✅, you're ready to develop!**

---

## Next Steps

### Explore the Codebase
1. Read [System Overview](../architecture/system-overview.md) - Understand architecture
2. Read [Quick Reference](../patterns/00-QUICK-REFERENCE.md) 🔥 - Common patterns
3. Explore [Identity Module](../../modules/identity/00-INDEX.md) - Authentication flows

### Start Developing
1. Read [Development Workflow](./development-workflow.md) - Daily dev cycle
2. Read [Git Workflow](./git-workflow.md) - Branch naming, commits, PRs
3. Pick a task from backlog and start coding!

### Need Help?
- [Debugging Guide](./debugging.md) - Common issues and solutions
- [Testing Workflow](./testing-workflow.md) - Running tests, debugging
- **Slack**: #axon-backend channel
- **Email**: engineering@axon.com

---

## Troubleshooting

### Build Errors

**Problem**: `.NET 10 SDK not found`
```bash
# Solution: Install .NET 10 preview
# Download: https://dotnet.microsoft.com/download/dotnet/10.0
```

**Problem**: `Package restore failed`
```bash
# Solution: Clear NuGet cache and retry
dotnet nuget locals all --clear
dotnet restore
```

### Database Issues

**Problem**: `Connection refused to localhost:5432`
```bash
# Solution: Verify PostgreSQL container is running
docker ps | grep axon-postgres

# If not running, start it
docker start axon-postgres
```

**Problem**: `Database 'axon_dev' does not exist`
```bash
# Solution: Create database
docker exec -it axon-postgres psql -U postgres -c "CREATE DATABASE axon_dev;"
```

### API Startup Issues

**Problem**: `Port 5000 already in use`
```bash
# Solution: Kill process using port 5000
lsof -ti:5000 | xargs kill -9

# Or change port in launchSettings.json
```

**Problem**: `Failed to load configuration`
```bash
# Solution: Verify appsettings.Development.json exists and is valid JSON
cat src/Api/appsettings.Development.json | jq .
```

---

## IDE Setup

### JetBrains Rider (Recommended)

1. Open `Axon-Backend.sln`
2. **Settings** → **Build, Execution, Deployment** → **Toolset and Build**
   - Use .NET SDK: 10.0.x
3. **Settings** → **Editor** → **Code Style** → **C#**
   - Use .editorconfig settings
4. **Run Configuration**: Api (launchSettings.json)
5. **Install Plugins**:
   - .NET Core User Secrets
   - Entity Framework Core UI

### VS Code

1. Install extensions:
   - C# Dev Kit
   - .NET Install Tool
   - REST Client (for testing endpoints)
2. Open folder: `Axon-Backend`
3. Trust workspace
4. Terminal → Run Task → `build`
5. Debug → Select `.NET Core Launch (Api)`

---

## Related Documentation

- **Development Workflow** → [development-workflow.md](./development-workflow.md)
- **Git Workflow** → [git-workflow.md](./git-workflow.md)
- **Coding Standards** → [../codebase/coding-standards.md](../codebase/coding-standards.md)
- **Architecture Overview** → [../architecture/system-overview.md](../architecture/system-overview.md)

---

**Welcome to Axon Backend! 🎉**

**Last Updated**: 2025-09-29