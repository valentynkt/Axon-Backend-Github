# Database Operations Guide

## 🚀 Quick Reference - Most Common Commands

**Always run from project root: `/Users/valentynkit/Repos/Axon-Backend`**

```bash
# 📋 CREATE NEW MIGRATION (most common)
dotnet ef migrations add <MigrationName> --project src/Modules/Chat/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations

# 📋 APPLY MIGRATIONS 
dotnet ef database update --project src/Modules/Chat/Infrastructure --startup-project src/Api

# 📋 LIST MIGRATIONS
dotnet ef migrations list --project src/Modules/Chat/Infrastructure --startup-project src/Api

# 📋 REMOVE LAST MIGRATION (if not applied)
dotnet ef migrations remove --project src/Modules/Chat/Infrastructure --startup-project src/Api
```

---

## Overview

This guide covers database migration commands and connection verification for the Axon Backend system. The project uses Entity Framework Core with PostgreSQL and implements automatic database updates.

## Database Configuration

### Connection String Setup

Ensure your `appsettings.json` or `appsettings.Development.json` has the connection string configured:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=axon_chat;Username=your_username;Password=your_password;Port=5432"
  }
}
```

### Automatic Database Updates

The system is configured with **automatic database updates** through the `EnsureChatDatabaseAsync()` method in `ServiceCollectionExtensions.cs`. This means:

- Database is automatically created if it doesn't exist
- Pending migrations are automatically applied on application startup
- No manual migration commands are required for standard operations

## Migration Commands

### 🚀 Ready-to-Use Migration Commands

**Step 1: Navigate to Project Root**
```bash
cd /Users/valentynkit/Repos/Axon-Backend
```

**Step 2: Create New Migration (Copy-Paste Ready)**
```bash
# Template - replace <MigrationName> with your actual migration name
dotnet ef migrations add <MigrationName> --project src/Modules/Chat/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations

# 📋 COPY-PASTE EXAMPLES:
dotnet ef migrations add AddUserPreferences --project src/Modules/Chat/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations

dotnet ef migrations add UpdateConversationSchema --project src/Modules/Chat/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations

dotnet ef migrations add AddMessageIndex --project src/Modules/Chat/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations

dotnet ef migrations add AddFullTextSearch --project src/Modules/Chat/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations
```

**Step 3: Apply Migration (Copy-Paste Ready)**
```bash
# Apply all pending migrations
dotnet ef database update --project src/Modules/Chat/Infrastructure --startup-project src/Api

# Apply specific migration
dotnet ef database update <MigrationName> --project src/Modules/Chat/Infrastructure --startup-project src/Api
```

### 🛠️ Advanced Migration Operations

**From Project Root (`/Users/valentynkit/Repos/Axon-Backend`)**

```bash
# ✅ List all migrations
dotnet ef migrations list --project src/Modules/Chat/Infrastructure --startup-project src/Api

# ✅ Remove last migration (if NOT applied to database)
dotnet ef migrations remove --project src/Modules/Chat/Infrastructure --startup-project src/Api

# ✅ Rollback to previous migration
dotnet ef database update <PreviousMigrationName> --project src/Modules/Chat/Infrastructure --startup-project src/Api

# ✅ Check migration status
dotnet ef migrations has-pending-model-changes --project src/Modules/Chat/Infrastructure --startup-project src/Api
```

### 📄 Generate SQL Scripts (Copy-Paste Ready)

**From Project Root (`/Users/valentynkit/Repos/Axon-Backend`)**

```bash
# ✅ Generate script for all migrations
dotnet ef migrations script --project src/Modules/Chat/Infrastructure --startup-project src/Api --output migration-script.sql

# ✅ Generate script for pending migrations only (idempotent)
dotnet ef migrations script --project src/Modules/Chat/Infrastructure --startup-project src/Api --idempotent --output pending-migrations.sql

# ✅ Generate script from specific migration to latest
dotnet ef migrations script <FromMigration> --project src/Modules/Chat/Infrastructure --startup-project src/Api --output partial-script.sql

# ✅ Generate script between two specific migrations
dotnet ef migrations script <FromMigration> <ToMigration> --project src/Modules/Chat/Infrastructure --startup-project src/Api --output range-script.sql

# 📋 EXAMPLE:
dotnet ef migrations script InitialCreate --project src/Modules/Chat/Infrastructure --startup-project src/Api --output from-initial.sql
```

## Database Connection Verification

### 🔍 Quick Connection Test (Copy-Paste Ready)

**From Project Root (`/Users/valentynkit/Repos/Axon-Backend`)**

```bash
# ✅ 1. Test basic PostgreSQL connection (requires psql client)
psql -h localhost -U your_username -d axon_chat -c "SELECT version();"

# ✅ 2. Test EF Core connection through application
dotnet run --project src/Api -- --verify-database

# ✅ 3. Check migration status
dotnet ef migrations list --project src/Modules/Chat/Infrastructure --startup-project src/Api

# ✅ 4. Check if database can connect
dotnet ef database connection-string --project src/Modules/Chat/Infrastructure --startup-project src/Api

# ✅ 5. Test database exists
dotnet ef database drop --dry-run --project src/Modules/Chat/Infrastructure --startup-project src/Api
```

### Detailed Database Verification

```bash
# Check if database exists
psql -h localhost -U your_username -d postgres -c "SELECT datname FROM pg_database WHERE datname = 'axon_chat';"

# Check applied migrations
psql -h localhost -U your_username -d axon_chat -c "SELECT \"MigrationId\", \"ProductVersion\" FROM chat.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"

# Check table structures
psql -h localhost -U your_username -d axon_chat -c "\dt chat.*"

# Verify specific tables exist
psql -h localhost -U your_username -d axon_chat -c "SELECT table_name FROM information_schema.tables WHERE table_schema = 'chat';"
```

### Application-Level Verification

Create a simple verification endpoint or console command:

```csharp
// Example verification code (can be added to Program.cs for testing)
public static async Task VerifyDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Test basic connection
        await context.Database.CanConnectAsync();
        logger.LogInformation("✅ Database connection successful");

        // Check pending migrations
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogWarning("⚠️ Pending migrations found: {Migrations}", 
                string.Join(", ", pendingMigrations));
        }
        else
        {
            logger.LogInformation("✅ All migrations applied");
        }

        // Test basic query
        var count = await context.Conversations.CountAsync();
        logger.LogInformation("✅ Database query successful - Conversations count: {Count}", count);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Database verification failed");
        throw;
    }
}
```

## Environment-Specific Considerations

### Development Environment

- Automatic migrations are enabled
- Database is created automatically on first run
- Development connection string should point to local PostgreSQL instance

### Production Environment

- Consider disabling automatic migrations for better control
- Use scripted migrations with proper review process
- Implement proper backup procedures before applying migrations
- Use connection pooling and retry policies (already configured)

### Docker Environment

```bash
# Start PostgreSQL container for development
docker run --name axon-postgres \
  -e POSTGRES_DB=axon_chat \
  -e POSTGRES_USER=axon_user \
  -e POSTGRES_PASSWORD=axon_password \
  -p 5432:5432 \
  -d postgres:16-alpine

# Connection string for Docker
"DefaultConnection": "Host=localhost;Database=axon_chat;Username=axon_user;Password=axon_password;Port=5432"
```

## Troubleshooting

### ⚠️ Common Path & Command Issues

1. **"Project not found" Error**
   ```bash
   # ❌ Wrong - relative paths don't work from random directories
   dotnet ef migrations add Test --project ../Chat/Infrastructure
   
   # ✅ Correct - always use from project root with full paths
   cd /Users/valentynkit/Repos/Axon-Backend
   dotnet ef migrations add Test --project src/Modules/Chat/Infrastructure --startup-project src/Host
   ```

2. **"Startup project not found" Error**
   ```bash
   # ❌ Wrong - missing startup project
   dotnet ef migrations add Test --project src/Modules/Chat/Infrastructure
   
   # ✅ Correct - always specify startup project
   dotnet ef migrations add Test --project src/Modules/Chat/Infrastructure --startup-project src/Host
   ```

3. **"Output directory not found" Error**
   ```bash
   # ❌ Wrong - wrong output directory
   dotnet ef migrations add Test --project src/Modules/Chat/Infrastructure --startup-project src/Host --output-dir Migrations
   
   # ✅ Correct - use existing directory structure
   dotnet ef migrations add Test --project src/Modules/Chat/Infrastructure --startup-project src/Host --output-dir Persistence/Migrations
   ```

### 🔧 Database Connection Issues

1. **Connection Refused**
   - Verify PostgreSQL is running: `sudo systemctl status postgresql` (Linux) or `brew services list | grep postgresql` (macOS)
   - Check connection string parameters
   - Verify firewall settings

2. **Migration Failures**
   - Check for conflicting changes in multiple environments
   - Verify database user has sufficient permissions
   - Review migration script for potential issues

3. **Performance Issues**
   - The ChatDbContext is configured with performance optimizations
   - Connection pooling is enabled by default
   - Consider adding indexes for specific query patterns

### Log Analysis

Enable detailed logging for EF Core operations:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "Microsoft.EntityFrameworkCore.Infrastructure": "Information"
    }
  }
}
```

## Architecture Features

### Event Sourcing Integration

The database setup includes:
- **Outbox Pattern**: Transactional event persistence
- **Domain Event Interceptors**: Automatic event capture
- **Audit Trail**: Comprehensive change tracking
- **CQRS Read Models**: Optimized query projections

### Performance Optimizations

- **Compiled Queries**: Pre-compiled for high-performance scenarios
- **Connection Pooling**: Automatic connection management
- **Retry Policies**: Resilience against transient failures
- **PostgreSQL Extensions**: Full-text search, UUID generation, performance monitoring

### Security Features

- **Connection Security**: SSL/TLS support
- **Audit Logging**: All changes tracked with user context
- **Sensitive Data Protection**: Logging protection in production
- **Optimistic Concurrency**: Row-level versioning to prevent conflicts