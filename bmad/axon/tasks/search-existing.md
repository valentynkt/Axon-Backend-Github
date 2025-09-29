# Search Existing Implementations Task

**Agent**: Axon Archaeologist
**Purpose**: Multi-layer search across codebase to find existing implementations

---

## Task Instructions

### Multi-Layer Search Strategy

**5 Search Layers**:
1. **Domain Layer** - Aggregates, entities, value objects, domain services
2. **Application Layer** - Commands, queries, handlers, services
3. **Infrastructure Layer** - Repositories, external service clients
4. **API Layer** - Endpoints, request/response models
5. **Cross-Module** - BuildingBlocks, shared code

### Process

1. **Parse Search Query**
   - Extract domain concept (e.g., "wallet verification")
   - Identify likely entities (e.g., "Wallet", "WalletOwnership")
   - List related operations (e.g., "verify", "revoke", "authenticate")

2. **Execute Layered Search**
   ```bash
   # Layer 1: Domain
   Grep: "class Wallet" in src/Modules/*/Domain/
   Grep: "VerifyWallet" in src/Modules/*/Domain/

   # Layer 2: Application
   Grep: "WalletVerificationCommand" in src/Modules/*/Application/
   Grep: "IRequest<Result" + "Wallet" in src/Modules/*/Application/

   # Layer 3: Infrastructure
   Grep: "WalletRepository" in src/Modules/*/Infrastructure/
   Grep: "IWalletVerificationService" in src/Modules/*/Infrastructure/

   # Layer 4: API
   Grep: "/wallets" in src/Api/Endpoints/
   Grep: "WalletEndpoint" in src/Api/

   # Layer 5: Cross-Module
   Grep: concept in src/BuildingBlocks/
   ```

3. **Categorize Findings**
   - **Direct Match**: Exact feature exists
   - **Partial Match**: Similar feature, needs extension
   - **Related**: Different but relevant
   - **Not Found**: No existing implementation

4. **Generate Reuse Guidance**

### Output Format
```yaml
search_results:
  query: "wallet verification"
  layers_searched: 5
  total_matches: 8

  findings:
    domain_layer:
      - file: "src/Modules/Identity/Domain/Entities/WalletOwnership.cs"
        line: 12
        match_type: DIRECT
        snippet: "public class WalletOwnership : Entity<WalletOwnershipId>"
        reuse_guidance: "EXTEND - Add auto-revocation logic to existing entity"

    application_layer:
      - file: "src/Modules/Identity/Application/Commands/VerifyWalletCommand.cs"
        line: 8
        match_type: DIRECT
        snippet: "public record VerifyWalletCommand : IRequest<Result<WalletVerificationResult>>"
        reuse_guidance: "REUSE DIRECTLY - Command exists, add new field for auto-revoke"

    infrastructure_layer:
      - file: "src/Modules/Identity/Infrastructure/Services/WalletVerificationService.cs"
        line: 45
        match_type: PARTIAL
        snippet: "public async Task<Result<bool>> VerifyWalletSignature(...)"
        reuse_guidance: "EXTEND - Add auto-revocation check to existing verification logic"

  reuse_summary:
    reuse_directly: 2
    extend: 4
    adapt: 1
    create_new: 1
```

---

## TODO: Full Implementation
Implement layered Grep searches, match categorization, and reuse analysis.