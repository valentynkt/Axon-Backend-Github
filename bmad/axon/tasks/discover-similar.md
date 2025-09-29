# Discover Similar Implementations Task

**Agent**: Axon Archaeologist
**Purpose**: Semantic similarity search for related code

---

## Task Instructions

### Input Requirements
- Target concept (e.g., "auto-revoke credentials after expiry")
- Domain context (Identity, Chat, etc.)

### Process

1. **Extract Key Terms**
   ```
   Concept: "auto-revoke credentials after expiry"
   Key terms: [auto, revoke, credential, expiry, automatic, timeout]
   ```

2. **Semantic Search Strategy**
   - **Exact matches**: Search for exact key terms
   - **Synonyms**: revoke → delete, remove, invalidate
   - **Related concepts**: expiry → TTL, timeout, duration, validity
   - **Domain-specific**: credential → wallet, token, session

3. **Search Scope**
   ```bash
   # Primary search
   Grep: "revoke" in src/Modules/Identity/
   Grep: "expir" in src/Modules/Identity/
   Grep: "TTL|timeout|duration" in src/Modules/Identity/

   # Extended search (other modules)
   Grep: "auto.*delete|remove" in src/Modules/
   Grep: "scheduled.*cleanup" in src/Modules/
   ```

4. **Calculate Similarity Score**
   ```
   Score = (Matching terms / Total terms) × 100
   + Context bonus (same module: +20%)
   + Pattern bonus (same pattern: +10%)
   ```

5. **Rank by Relevance**

### Output Format
```yaml
similarity_search:
  concept: "auto-revoke credentials after expiry"
  key_terms: [auto, revoke, credential, expiry, automatic, timeout]
  matches_found: 4

  results:
    - similarity: 85%
      file: "src/Modules/Identity/Domain/Entities/IdentityCredential.cs"
      line: 67
      match_reason: "Has TTL and revocation logic"
      snippet: |
        public Result<Unit> CheckExpiration()
        {
            if (ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow)
            {
                return Revoke(RevokedBy.System);
            }
            return Result<Unit>.Success(Unit.Value);
        }
      key_terms_matched: [revoke, expiry, automatic]
      reuse_potential: HIGH
      adaptation_notes: "Similar pattern - credential expires and auto-revokes. Adapt for WalletOwnership."

    - similarity: 72%
      file: "src/Modules/Chat/Domain/Entities/Message.cs"
      line: 45
      match_reason: "Has auto-cleanup after period"
      snippet: |
        private DateTimeOffset? DeletedAt { get; set; }

        public Result<Unit> ScheduleAutoDeletion(TimeSpan after)
        {
            DeletedAt = DateTimeOffset.UtcNow.Add(after);
            return Result<Unit>.Success(Unit.Value);
        }
      key_terms_matched: [auto, delete, timeout]
      reuse_potential: MEDIUM
      adaptation_notes: "Different domain but similar time-based auto-action pattern."

    - similarity: 60%
      file: "src/BuildingBlocks/Infrastructure/BackgroundJobs/CleanupExpiredItemsJob.cs"
      line: 23
      match_reason: "Background job for cleanup"
      snippet: |
        public async Task Execute(IJobExecutionContext context)
        {
            var expired = await _repository.GetExpiredItemsAsync();
            foreach (var item in expired)
            {
                await item.MarkAsExpired();
            }
        }
      key_terms_matched: [expired, cleanup, automatic]
      reuse_potential: LOW
      adaptation_notes: "Infrastructure pattern for scheduled cleanup. Could use for batch wallet revocation."

  reuse_recommendations:
    primary: "Adapt IdentityCredential.CheckExpiration() pattern to WalletOwnership"
    secondary: "Consider background job for batch processing expired wallets"
    libraries: "Check if any library provides TTL/expiration out-of-box"
```

---

## TODO: Full Implementation
Implement semantic search, synonym expansion, and similarity scoring.