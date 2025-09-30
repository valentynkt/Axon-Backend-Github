# Reuse Report Task

<task id="archaeologist/reuse-report" name="Generate Comprehensive Reuse Guidance">
  <llm critical="true">
    <i>Consolidate ALL search results into comprehensive reuse guidance for story</i>
    <i>CRITICAL: Classify opportunities as REUSE_DIRECTLY/EXTEND/ADAPT/CREATE_NEW</i>
    <i>Calculate reuse score: (Reusable items / Total items) × 100, Target: 60%+</i>
    <i>Generate implementation order with dependencies and effort estimates</i>
  </llm>

  <flow>
    <step n="1" title="Consolidate Search Results">
      <action>Gather results from: search-existing, discover-similar, map-apis tasks</action>
      <action>Deduplicate findings (same file mentioned multiple times)</action>
      <action>Cross-reference: APIs found + Similar implementations + Pattern matches</action>
      <action>Total items to implement (from story requirements)</action>
    </step>

    <step n="2" title="Classify Reuse Opportunities (4 Categories)">
      <action>REUSE_DIRECTLY: Use as-is, no/trivial changes (add optional parameter)</action>
      <action>EXTEND: Add to existing code, minimal changes (new property, new method)</action>
      <action>ADAPT: Copy pattern, modify for new context (similar but different domain)</action>
      <action>CREATE_NEW: No suitable existing code, build from scratch</action>
      <action>For each item: File, action, effort (LOW/MEDIUM/HIGH), confidence (LOW/MEDIUM/HIGH)</action>
    </step>

    <step n="3" title="Calculate Reuse Score">
      <action>Count total items to implement</action>
      <action>Count reusable items (REUSE + EXTEND + ADAPT)</action>
      <action>Reuse Score = (Reusable / Total) × 100</action>
      <action>Compare to target: 60%+ excellent, 40-60% good, &lt;40% poor (greenfield)</action>
    </step>

    <step n="4" title="Estimate Effort Breakdown">
      <action>REUSE_DIRECTLY: 5-10% effort per item (trivial changes)</action>
      <action>EXTEND: 20-40% effort per item (small additions)</action>
      <action>ADAPT: 40-60% effort per item (pattern copy)</action>
      <action>CREATE_NEW: 80-100% effort per item (from scratch)</action>
      <action>Calculate total effort: Sum weighted by category</action>
    </step>

    <step n="5" title="Generate Implementation Order">
      <action>Analyze dependencies: Entity changes before commands, commands before endpoints</action>
      <action>Order by: Domain → Application → Infrastructure → API</action>
      <action>Number each step sequentially with dependencies noted</action>
      <action>Mark optional items (can be skipped for MVP)</action>
    </step>

    <step n="6" title="Identify Anti-Patterns Avoided">
      <action>List what would have been reinvented if discovery hadn't found existing code</action>
      <action>Highlight reuse wins (expiration logic, verification service, etc.)</action>
      <action>Celebrate brownfield efficiency (87% reuse vs 20% in greenfield)</action>
    </step>
  </flow>

  <validation>
    <i>All 4 categories populated (even if CREATE_NEW is empty)</i>
    <i>Reuse score between 0-100%</i>
    <i>Implementation order respects dependencies</i>
    <i>Effort estimates realistic and total to 100%</i>
    <i>Recommendations actionable and specific</i>
  </validation>

  <output format="yaml">
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

    extend:
      count: 4
      items:
        - item: "WalletOwnership entity"
          file: "src/Modules/Identity/Domain/Entities/WalletOwnership.cs"
          action: "Add AutoRevokeAt property (DateTimeOffset?)"
          effort: LOW
          confidence: HIGH

    adapt:
      count: 1
      items:
        - item: "IdentityCredential.CheckExpiration() pattern"
          file: "src/Modules/Identity/Domain/Entities/IdentityCredential.cs:67"
          action: "Copy expiration check pattern, adapt for WalletOwnership"
          effort: MEDIUM
          confidence: HIGH

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
    reusable_items: 7
    score: 87%
    target: 60%
    status: EXCELLENT

  effort_estimate:
    total_effort: MEDIUM
    breakdown:
      reuse_directly: "10% of effort (2 items)"
      extend: "60% of effort (4 items)"
      adapt: "20% of effort (1 item)"
      create_new: "10% of effort (optional)"

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

  anti_patterns_avoided:
    - "Did not reinvent expiration logic - adapted from IdentityCredential"
    - "Did not create new verification service - reused existing"
    - "Did not duplicate revocation logic - extended existing Revoke() method"

  recommendations:
    primary: "Focus on extending existing code (87% reuse rate is excellent)"
    secondary: "Skip optional background job for MVP, add later if needed"
    pattern_compliance: "All changes follow existing patterns (Result&lt;T>, StrongId&lt;T>)"
    testing: "Reuse existing test patterns from WalletOwnershipTests"
  </output>

  <halt-conditions>
    <i>Reuse score &lt;20% - warn this looks like greenfield, verify story requirements</i>
    <i>Missing search results - cannot consolidate without prior searches</i>
    <i>Circular dependencies detected - warn user, suggest refactoring approach</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/guides/codebase/reuse-patterns.md - Reuse strategies</i>
    <i>Example: WalletOwnership story for realistic reuse report</i>
  </references>
</task>