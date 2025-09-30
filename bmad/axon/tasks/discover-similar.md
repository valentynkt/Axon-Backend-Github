# Discover Similar Implementations Task

<task id="archaeologist/discover-similar" name="Semantic Similarity Search">
  <llm critical="true">
    <i>Perform semantic similarity search for related code implementations</i>
    <i>CRITICAL: Extract key terms, search with synonyms, calculate similarity scores (0-100%)</i>
    <i>GOAL: Find reusable code even if named differently (semantic, not just keyword match)</i>
    <i>Rank by relevance + reuse potential (HIGH/MEDIUM/LOW)</i>
  </llm>

  <flow>
    <step n="1" title="Parse Input & Extract Key Terms">
      <action>Parse target concept (e.g., "auto-revoke credentials after expiry")</action>
      <action>Extract domain context (Identity/Chat/etc.)</action>
      <action>Extract key terms: [auto, revoke, credential, expiry, automatic, timeout]</action>
      <action>Generate synonyms: revoke → [delete, remove, invalidate], expiry → [TTL, timeout, duration]</action>
    </step>

    <step n="2" title="Primary Semantic Search (Exact + Synonyms)">
      <action>Search exact key terms: Grep "revoke|expir" in src/Modules/{Domain}/</action>
      <action>Search synonyms: Grep "delete|remove|invalidate" + "TTL|timeout|duration"</action>
      <action>Search domain-specific terms: credential → [wallet, token, session]</action>
      <action>Collect all matching files with line numbers</action>
    </step>

    <step n="3" title="Extended Search (Cross-Module)">
      <action>If primary search yields &lt; 3 results, expand to all modules</action>
      <action>Grep "auto.*delete|remove" in src/Modules/</action>
      <action>Grep "scheduled.*cleanup" in src/</action>
      <action>Grep "background.*job.*expir" in src/BuildingBlocks/</action>
    </step>

    <step n="4" title="Calculate Similarity Scores">
      <action>For each match, calculate: (Matching terms / Total terms) × 100</action>
      <action>Add context bonus: Same module +20%, Same pattern +10%</action>
      <action>Read code snippets to verify semantic similarity (not just keyword match)</action>
      <action>Rank results by score (highest first)</action>
    </step>

    <step n="5" title="Assess Reuse Potential">
      <action>HIGH: Same domain + same pattern + 80%+ similarity</action>
      <action>MEDIUM: Different domain but similar pattern + 60-79% similarity</action>
      <action>LOW: Different domain + different pattern but related concept + &lt;60%</action>
      <action>Generate adaptation notes for each result</action>
    </step>
  </flow>

  <validation>
    <i>At least 1 result found (or report no similar implementations exist)</i>
    <i>Similarity scores between 0-100%</i>
    <i>Code snippets accurate (file:line verified)</i>
    <i>Reuse recommendations actionable and specific</i>
  </validation>

  <output format="yaml">
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
        public Result&lt;Unit> CheckExpiration()
        {
            if (ExpiresAt.HasValue && ExpiresAt.Value &lt; DateTimeOffset.UtcNow)
                return Revoke(RevokedBy.System);
            return Result&lt;Unit>.Success(Unit.Value);
        }
      key_terms_matched: [revoke, expiry, automatic]
      reuse_potential: HIGH
      adaptation_notes: "Similar pattern - credential expires and auto-revokes. Adapt for WalletOwnership."

    - similarity: 72%
      file: "src/Modules/Chat/Domain/Entities/Message.cs"
      line: 45
      match_reason: "Has auto-cleanup after period"
      snippet: |
        public Result&lt;Unit> ScheduleAutoDeletion(TimeSpan after)
        {
            DeletedAt = DateTimeOffset.UtcNow.Add(after);
            return Result&lt;Unit>.Success(Unit.Value);
        }
      key_terms_matched: [auto, delete, timeout]
      reuse_potential: MEDIUM
      adaptation_notes: "Different domain but similar time-based auto-action pattern."

  reuse_recommendations:
    primary: "Adapt IdentityCredential.CheckExpiration() pattern to WalletOwnership"
    secondary: "Consider background job for batch processing expired wallets"
    libraries: "Check if any library provides TTL/expiration out-of-box"
  </output>

  <halt-conditions>
    <i>Zero matches found - report no similar code exists, suggest creating new</i>
    <i>All matches &lt;30% similarity - warn low relevance, ask if search criteria should change</i>
    <i>Grep failures - report tool error</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/guides/patterns/domain-modeling.md - Entity lifecycle patterns</i>
    <i>Example: IdentityCredential for expiration patterns, Message for auto-deletion</i>
  </references>
</task>