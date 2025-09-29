# Map APIs Task

**Agent**: Axon Archaeologist
**Purpose**: Map available APIs/methods in a domain area

---

## Task Instructions

### Input Requirements
- Domain area (e.g., "Identity", "Chat", "WalletOwnership")
- API scope (Domain methods, Application operations, Infrastructure services, HTTP endpoints)

### Process

1. **Domain API Mapping** (Public methods on aggregates/entities)
   ```csharp
   Grep: "public.*Result<" in src/Modules/{Module}/Domain/
   Extract: method signatures from aggregates/entities
   ```

2. **Application API Mapping** (Commands/Queries)
   ```csharp
   Grep: "IRequest<Result<" in src/Modules/{Module}/Application/
   Extract: command/query definitions
   ```

3. **Infrastructure API Mapping** (Repositories, services)
   ```csharp
   Grep: "interface I.*Repository" in src/Modules/{Module}/Infrastructure/
   Grep: "interface I.*Service" in src/Modules/{Module}/Infrastructure/
   Extract: interface methods
   ```

4. **HTTP API Mapping** (REST endpoints)
   ```csharp
   Grep: "Endpoint<" in src/Api/Endpoints/{Module}/
   Extract: FastEndpoints definitions with routes
   ```

5. **Cross-Reference**
   - Map HTTP endpoints → Application commands/queries
   - Map Application handlers → Domain methods
   - Map Infrastructure services → External libraries

### Output Format
```yaml
api_map:
  domain: "WalletOwnership"
  module: "Identity"
  layers_mapped: 4

  domain_api:
    aggregate: "WalletOwnership"
    file: "src/Modules/Identity/Domain/Entities/WalletOwnership.cs"
    methods:
      - signature: "Result<Unit> Revoke(RevokedBy revokedBy)"
        line: 45
        visibility: public
        returns: "Result<Unit>"

      - signature: "bool IsValid()"
        line: 62
        visibility: public
        returns: bool

  application_api:
    commands:
      - name: "VerifyWalletCommand"
        file: "src/Modules/Identity/Application/Commands/VerifyWalletCommand.cs"
        returns: "Result<WalletVerificationResult>"
        handler: "VerifyWalletCommandHandler"

    queries:
      - name: "GetWalletOwnershipsQuery"
        file: "src/Modules/Identity/Application/Queries/GetWalletOwnershipsQuery.cs"
        returns: "Result<List<WalletOwnershipDto>>"
        handler: "GetWalletOwnershipsQueryHandler"

  infrastructure_api:
    repositories:
      - interface: "IIdentityRepository"
        file: "src/Modules/Identity/Infrastructure/Persistence/IIdentityRepository.cs"
        methods:
          - "Task<WalletOwnership?> GetByWalletAddressAsync(...)"

    services:
      - interface: "IWalletVerificationService"
        file: "src/Modules/Identity/Infrastructure/Services/IWalletVerificationService.cs"
        methods:
          - "Task<Result<bool>> VerifySignatureAsync(...)"

  http_api:
    endpoints:
      - route: "POST /api/identity/wallets/verify"
        file: "src/Api/Endpoints/Identity/WalletVerificationEndpoint.cs"
        command: "VerifyWalletCommand"
        request: "WalletVerificationRequest"
        response: "WalletVerificationResponse"
```

---

## TODO: Full Implementation
Implement multi-layer API discovery and cross-reference mapping.