# Map Dependencies Task

**Agent**: Axon Archaeologist
**Purpose**: Map dependencies and calculate impact analysis (blast radius)

---

## Task Instructions

### Input Requirements
- Target code (class, method, entity to be changed)
- Change type (modify, delete, rename, move)

### Process

1. **Find All Usages**
   ```bash
   Target: "WalletOwnership" class

   # Direct usages
   Grep: "WalletOwnership" in src/

   # Type references
   Grep: "WalletOwnership" in method signatures
   Grep: "WalletOwnership" in constructors

   # Property access
   Grep: "WalletOwnership\\..*" in src/
   ```

2. **Categorize Dependencies**
   - **Same Module**: Within Identity module
   - **Cross-Module**: From Chat/other modules
   - **API Layer**: HTTP endpoints
   - **Infrastructure**: Repositories, configurations
   - **Tests**: Test files

3. **Calculate Blast Radius**
   ```yaml
   blast_radius:
     small: 1-5 files affected
     medium: 6-15 files affected
     large: 16+ files affected
   ```

4. **Identify Breaking Changes**
   - Public API changes (HTTP endpoints)
   - Database schema changes
   - Cross-module contract changes
   - Event signature changes

### Output Format
```yaml
dependency_map:
  target: "WalletOwnership"
  target_file: "src/Modules/Identity/Domain/Entities/WalletOwnership.cs"
  change_type: "modify"  # add property AutoRevokeAt
  total_usages: 24

  blast_radius:
    size: MEDIUM
    files_affected: 12
    modules_affected: 2

  dependencies_by_category:
    same_module:
      count: 8
      files:
        - file: "src/Modules/Identity/Application/Commands/VerifyWalletCommandHandler.cs"
          line: 45
          usage: "Creates new WalletOwnership instance"
          impact: MEDIUM
          breaking: false
          note: "Constructor call - add new optional parameter"

        - file: "src/Modules/Identity/Infrastructure/Persistence/Configurations/WalletOwnershipConfiguration.cs"
          line: 22
          usage: "EF Core entity configuration"
          impact: HIGH
          breaking: true
          note: "Add new property configuration for AutoRevokeAt"

    cross_module:
      count: 2
      files:
        - file: "src/Modules/Chat/Application/Services/UserContextService.cs"
          line: 67
          usage: "Reads WalletOwnership.WalletAddress"
          impact: LOW
          breaking: false
          note: "No impact - only reads existing property"

    api_layer:
      count: 3
      files:
        - file: "src/Api/Endpoints/Identity/GetWalletsEndpoint.cs"
          line: 34
          usage: "Maps WalletOwnership to DTO"
          impact: MEDIUM
          breaking: false
          note: "Update DTO to include AutoRevokeAt (optional field)"

    infrastructure:
      count: 4
      files:
        - file: "src/Modules/Identity/Infrastructure/Persistence/IdentityDbContext.cs"
          line: 28
          usage: "DbSet<WalletOwnership>"
          impact: HIGH
          breaking: true
          note: "Database migration required for new column"

    tests:
      count: 7
      files:
        - file: "tests/Modules/Identity/Domain/WalletOwnershipTests.cs"
          line: 15
          usage: "Test fixture creates instances"
          impact: MEDIUM
          breaking: false
          note: "Update test builders with AutoRevokeAt"

  breaking_changes:
    - type: DATABASE_SCHEMA
      severity: HIGH
      description: "New column AutoRevokeAt requires migration"
      mitigation: "Create EF Core migration, nullable column for backward compatibility"

    - type: API_CONTRACT
      severity: LOW
      description: "Response DTOs gain new optional field"
      mitigation: "Add field as optional, existing clients ignore"

  impact_summary:
    low_impact: 10 files  # Read-only usages
    medium_impact: 8 files  # Constructor calls, DTOs
    high_impact: 4 files  # Configuration, schema, critical paths
    breaking: 2 files

  recommendations:
    - "Add AutoRevokeAt as nullable property for backward compatibility"
    - "Create database migration before deploying"
    - "Update all test builders to include AutoRevokeAt"
    - "Version API if response contract changes significantly"
    - "Run full regression test suite (identity + chat modules)"
```

---

## TODO: Full Implementation
Implement usage detection, impact categorization, and breaking change analysis.