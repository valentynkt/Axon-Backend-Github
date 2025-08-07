# PostgresOptions Compatibility Guide

## Overview

The PostgresOptions has been restored and enhanced with enterprise features while maintaining **full backward compatibility** with existing usage patterns.

## What's New ✅

### Enhanced PostgresOptions
- **Extends PersistenceConfigurationOptions** - Gets all enterprise features automatically
- **Named connection strings support** - Multiple database contexts (Flight, Identity, Passenger)
- **PostgreSQL-specific optimizations** - Performance tuning, pooling, prepared statements
- **Backward compatible** - Existing code continues to work without changes

### Enterprise Features Included
- ✅ **Performance Monitoring** - Automatic operation tracking
- ✅ **Health Checks** - Database connectivity monitoring
- ✅ **Transaction Behavior** - Configurable transaction patterns
- ✅ **Repository Caching** - Decorator-based caching
- ✅ **Advanced Configuration** - Fluent API with enterprise options

## Backward Compatibility ✅

### Existing Usage Still Works
```csharp
// This continues to work exactly as before
var postgresOptions = services.GetOptions<PostgresOptions>(nameof(PostgresOptions));
var connectionString = postgresOptions.ConnectionString;

// Health checks continue to work
healthChecksBuilder.AddNpgSql(postgresOptions.ConnectionString);

// Test containers continue to work
"PostgresOptions:ConnectionString": PostgresTestcontainer?.GetConnectionString()
```

### Configuration Binding Still Works
```json
{
  "PostgresOptions": {
    "ConnectionString": "Host=localhost;Database=mydb;Username=user;Password=pass",
    "MaxRetryCount": 3,
    "CommandTimeout": 30
  }
}
```

## New Enhanced Usage 🚀

### Named Connection Strings (New)
```json
{
  "PostgresOptions": {
    "ConnectionString": "Host=localhost;Database=main;Username=user;Password=pass",
    "ConnectionStrings": {
      "Flight": "Host=localhost;Database=flight;Username=user;Password=pass",
      "Identity": "Host=localhost;Database=identity;Username=user;Password=pass",
      "Passenger": "Host=localhost;Database=passenger;Username=user;Password=pass"
    }
  }
}
```

### Enterprise Features Configuration (New)
```json
{
  "PostgresOptions": {
    "ConnectionString": "Host=localhost;Database=mydb;Username=user;Password=pass",
    "EnablePerformanceMonitoring": true,
    "EnableHealthChecks": true,
    "EnableRepositoryCaching": true,
    "DefaultTransactionBehavior": "PerOperation",
    "CacheExpiration": "00:15:00",
    "Performance": {
      "EnableQueryPlanCaching": true,
      "EnablePreparedStatements": true,
      "StatementCacheSize": 1000
    },
    "Pooling": {
      "MinPoolSize": 5,
      "MaxPoolSize": 100,
      "ConnectionIdleLifetime": 300
    }
  }
}
```

### Enhanced Setup Methods (New)
```csharp
// New enhanced setup with enterprise features
builder.AddPostgresWithCleanArchitecture<MyDbContext>(options =>
{
    options.EnablePerformanceMonitoring = true;
    options.EnableHealthChecks = true;
    options.EnableRepositoryCaching = true;
    options.Performance.EnablePreparedStatements = true;
    options.Pooling.MaxPoolSize = 50;
});

// Or use existing patterns - they still work
builder.Services.AddPostgresDbContext<MyDbContext>(builder.Configuration);
```

## Migration Path

### Option 1: No Changes Required ✅
Continue using existing PostgresOptions exactly as before. All enterprise features are enabled by default with sensible defaults.

### Option 2: Gradual Enhancement 🔄
Gradually add new configuration options to take advantage of enterprise features:

```csharp
// Start with existing usage
var postgresOptions = services.GetOptions<PostgresOptions>(nameof(PostgresOptions));

// Add enterprise features incrementally
builder.AddPostgresWithCleanArchitecture<MyDbContext>(options =>
{
    // Keep existing configuration
    options.ConnectionString = postgresOptions.ConnectionString;
    options.MaxRetryCount = postgresOptions.MaxRetryCount;
    
    // Add new enterprise features
    options.EnablePerformanceMonitoring = true;
    options.EnableHealthChecks = true;
});
```

### Option 3: Full Enterprise Setup 🚀
Use the new enhanced configuration for maximum benefits:

```csharp
builder.AddPostgresWithCleanArchitecture<ApplicationDbContext>(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.EnablePerformanceMonitoring = true;
    options.EnableHealthChecks = true;
    options.EnableRepositoryCaching = true;
    options.DefaultTransactionBehavior = TransactionBehavior.PerOperation;
    options.Performance.EnablePreparedStatements = true;
    options.Pooling.MaxPoolSize = 100;
});
```

## Key Benefits

### For Existing Code ✅
- **Zero breaking changes** - All existing code continues to work
- **Automatic enterprise features** - Get performance monitoring and health checks for free
- **Enhanced connection string support** - Named connections work automatically

### For New Development 🚀
- **PostgreSQL-optimized defaults** - Best practices built-in
- **Enterprise monitoring** - Production-ready observability
- **Advanced configuration** - Fine-tune performance and behavior
- **Clean architecture** - Proper separation of concerns

## Examples

### Health Checks (Existing Pattern)
```csharp
// This continues to work exactly as before
var postgresOptions = services.GetOptions<PostgresOptions>(nameof(PostgresOptions));
if (!string.IsNullOrEmpty(postgresOptions.ConnectionString))
    healthChecksBuilder.AddNpgSql(postgresOptions.ConnectionString);
```

### Test Configuration (Existing Pattern)
```csharp
// This continues to work exactly as before
configuration.AddInMemoryCollection(new KeyValuePair<string, string?>[]
{
    new("PostgresOptions:ConnectionString", PostgresTestcontainer?.GetConnectionString()),
    new("PostgresOptions:ConnectionString:Flight", PostgresTestcontainer?.GetConnectionString()),
    new("PostgresOptions:ConnectionString:Identity", PostgresTestcontainer?.GetConnectionString())
});
```

### Service Usage (Existing Pattern)
```csharp
// This continues to work exactly as before
var postgresOptions = Fixture.ServiceProvider.GetService<PostgresOptions>();
if (!string.IsNullOrEmpty(postgresOptions?.ConnectionString))
{
    DefaultDbConnection = new NpgsqlConnection(postgresOptions.ConnectionString);
}
```

## Summary

✅ **Full Backward Compatibility** - No existing code needs to change  
✅ **Enterprise Features** - Performance monitoring, health checks, caching  
✅ **PostgreSQL Optimized** - Tuned for PostgreSQL best practices  
✅ **Gradual Migration** - Adopt new features at your own pace  
✅ **Clean Architecture** - Proper separation and testability  

The enhanced PostgresOptions provides all the enterprise features you need while ensuring existing code continues to work without any modifications.