# Identity Module EF Core Entity Configurations

This directory contains Entity Framework Core configurations for the Identity Module.

## Configuration Files

### Core Entity Configurations
- **`AxonPrincipalConfiguration.cs`** - Primary aggregate configuration with owned profile entity
- **`WalletConfiguration.cs`** - Wallet aggregate with tags collection stored as JSON
- **`IdentityCredentialConfiguration.cs`** - Separate entity with unique constraints
- **`WalletOwnershipConfiguration.cs`** - Separate entity with performance indexes

### Utility Classes  
- **`JsonSerializationOptions.cs`** - Standardized JSON options for database storage

## Key Features

### Database Constraints (🚨 Critical)
- **Credential Uniqueness**: `(ProviderType, Issuer, Subject)` unique constraint prevents duplicate credentials
- **Wallet Uniqueness**: `(Chain, Address)` unique constraint prevents duplicate wallets

### Performance Optimizations
- Strategic indexes on foreign keys, timestamps, and query patterns
- Proper value object conversions for type safety
- JSON storage for complex metadata and collections

### Security & Compliance
- Secure JSON serialization options preventing RCE attacks  
- Soft deletion query filters
- Audit columns for compliance tracking

### Architectural Patterns
- Clean separation between aggregates using separate entity tables
- Value object conversions maintain domain integrity
- Owned entities for tightly-coupled data (Profile)
- Separate entities for loosely-coupled collections (Credentials, Ownerships)

## Usage Notes

These configurations are automatically applied during `DbContext` creation via:
```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
```

The configuration classes follow EF Core best practices and the project's Clean Architecture patterns.