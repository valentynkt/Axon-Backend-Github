# Reuse Report Task

**Agent**: Axon Archaeologist
**Purpose**: Generate comprehensive reuse guidance for a story

---

## Task Instructions

### Input Requirements
- Story requirements
- Search results from previous tasks
- Similarity matches
- API map

### Process

1. **Consolidate Findings**
   - Combine results from search-existing, discover-similar, map-apis
   - Deduplicate findings
   - Cross-reference related code

2. **Classify Reuse Opportunities**
   ```yaml
   categories:
     - REUSE_DIRECTLY: "Use as-is, no changes needed"
     - EXTEND: "Add to existing code, minimal changes"
     - ADAPT: "Copy pattern, modify for new context"
     - CREATE_NEW: "No suitable existing code, build from scratch"
   ```

3. **Calculate Reuse Score**
   ```
   Reuse Score = (Reusable items / Total items) × 100
   Target: 60%+ reuse (greenfield in brownfield)
   ```

4. **Generate Action Plan**

### Output Format
```yaml
reuse_report:
  story_id: "story-123"
  story_title: "Add wallet auto-revocation after 90 days"
  date: "2025-09-30"

  search_summary:
    files_searched: 342
    matches_found: 12
    relevant_matches: 8

  reuse_opportunities:
    reuse_directly:
      count: 2
      items:
        - item: "VerifyWalletCommand"
          file: "src/Modules/Identity/Application/Commands/VerifyWalletCommand.cs"
          action: "Add optional AutoRevokeAt field to command"
          effort: LOW
          confidence: HIGH

        - item: "WalletVerificationService.VerifySignatureAsync"
          file: "src/Modules/Identity/Infrastructure/Services/WalletVerificationService.cs"
          action: "Call existing method, add revocation check after"
          effort: LOW
          confidence: HIGH

    extend:
      count: 4
      items:
        - item: "WalletOwnership entity"
          file: "src/Modules/Identity/Domain/Entities/WalletOwnership.cs"
          action: "Add AutoRevokeAt property (DateTimeOffset?)"
          effort: LOW
          confidence: HIGH

        - item: "WalletOwnership.Revoke() method"
          file: "src/Modules/Identity/Domain/Entities/WalletOwnership.cs:45"
          action: "Add overload: RevokeIfExpired() → Result<Unit>"
          effort: LOW
          confidence: HIGH

        - item: "WalletOwnershipConfiguration"
          file: "src/Modules/Identity/Infrastructure/Persistence/Configurations/WalletOwnershipConfiguration.cs"
          action: "Add property configuration for AutoRevokeAt"
          effort: LOW
          confidence: HIGH

        - item: "GetWalletsEndpoint"
          file: "src/Api/Endpoints/Identity/GetWalletsEndpoint.cs"
          action: "Include AutoRevokeAt in response DTO"
          effort: LOW
          confidence: MEDIUM

    adapt:
      count: 1
      items:
        - item: "IdentityCredential.CheckExpiration() pattern"
          file: "src/Modules/Identity/Domain/Entities/IdentityCredential.cs:67"
          action: "Copy expiration check pattern, adapt for WalletOwnership"
          effort: MEDIUM
          confidence: HIGH
          notes: "Similar time-based auto-revocation logic already exists"

    create_new:
      count: 1
      items:
        - item: "Background job for batch wallet revocation"
          file: "N/A - new code"
          action: "Create CleanupExpiredWalletsJob (optional enhancement)"
          effort: HIGH
          confidence: MEDIUM
          notes: "Optional - can implement later if needed"

  reuse_score:
    total_items: 8
    reusable_items: 7  # All except create_new
    score: 87%
    target: 60%
    status: EXCELLENT

  effort_estimate:
    total_effort: MEDIUM
    breakdown:
      reuse_directly: "10% of effort (2 items, trivial changes)"
      extend: "60% of effort (4 items, small additions)"
      adapt: "20% of effort (1 item, pattern copy)"
      create_new: "10% of effort (optional background job)"

  implementation_order:
    1:
      action: "Extend WalletOwnership entity with AutoRevokeAt property"
      dependencies: []

    2:
      action: "Adapt IdentityCredential.CheckExpiration pattern"
      dependencies: ["WalletOwnership.AutoRevokeAt property"]

    3:
      action: "Extend VerifyWalletCommand with AutoRevokeAt field"
      dependencies: ["WalletOwnership changes"]

    4:
      action: "Reuse WalletVerificationService, add revocation check"
      dependencies: ["Command changes"]

    5:
      action: "Extend API endpoint DTO"
      dependencies: ["All domain/application changes"]

  anti-patterns_avoided:
    - "Did not reinvent expiration logic - adapted from IdentityCredential"
    - "Did not create new verification service - reused existing"
    - "Did not duplicate revocation logic - extended existing Revoke() method"

  recommendations:
    primary: "Focus on extending existing code (87% reuse rate is excellent)"
    secondary: "Skip optional background job for MVP, add later if needed"
    pattern_compliance: "All changes follow existing patterns (Result<T>, StrongId<T>)"
    testing: "Reuse existing test patterns from WalletOwnershipTests"
```

---

## TODO: Full Implementation
Implement result consolidation, reuse categorization, and action plan generation.