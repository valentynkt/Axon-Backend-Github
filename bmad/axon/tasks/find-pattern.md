# Find Pattern Task

<task id="archaeologist/find-pattern" name="Find Architectural Pattern Examples">
  <llm critical="true">
    <i>Find 3-5 BEST examples of specified architectural pattern in codebase</i>
    <i>CRITICAL: Extract actual code snippets with file:line citations, not summaries</i>
    <i>Patterns: Result&lt;T>, StrongId&lt;T>, CQRS, Domain Events, Owned Entities</i>
    <i>HALT if pattern type is invalid or not in supported list</i>
  </llm>

  <flow>
    <step n="1" title="Parse Input & Validate Pattern Type">
      <action>Extract pattern type from input (e.g., "Result&lt;T>", "StrongId", "CQRS")</action>
      <action>Validate pattern is one of 5 supported: Result, StrongId, CQRS, DomainEvents, OwnedEntities</action>
      <action>HALT if invalid pattern with supported list</action>
    </step>

    <step n="2" title="Search for Pattern Usage">
      <action>Result&lt;T>: Grep "Result&lt;.*>" in src/ (Domain methods, Application handlers)</action>
      <action>StrongId&lt;T>: Grep "record.*Id.*StrongId&lt;" in src/ (Value objects)</action>
      <action>CQRS: Grep "IRequest&lt;Result&lt;" + "IRequestHandler&lt;" in Application/</action>
      <action>Domain Events: Grep "record.*Event" + "DomainEvent" in Domain/Events/</action>
      <action>Owned Entities: Grep "OwnsOne|OwnsMany" in Infrastructure/Persistence/Configurations/</action>
      <action>Count total usage across codebase</action>
    </step>

    <step n="3" title="Find 3-5 Best Examples">
      <action>Select examples that show: Standard usage, Edge cases, Advanced patterns</action>
      <action>Prioritize: Domain layer > Application > Infrastructure > API</action>
      <action>Read actual code snippets from files (not summaries)</action>
      <action>Extract: File path, line number, code snippet (5-10 lines), context</action>
    </step>

    <step n="4" title="Identify Pattern Variations">
      <action>Result&lt;Unit> vs Result&lt;T> (void vs value operations)</action>
      <action>StrongId&lt;Guid> vs StrongId&lt;int> (ID types)</action>
      <action>Commands vs Queries (write vs read CQRS)</action>
      <action>Count usage of each variation</action>
    </step>

    <step n="5" title="Detect Anti-Patterns">
      <action>Search for violations: throw exceptions instead of Result</action>
      <action>Search for: primitive IDs instead of StrongId</action>
      <action>Search for: direct DB access instead of CQRS</action>
      <action>Record violations with file:line and recommendations</action>
    </step>
  </flow>

  <validation>
    <i>At least 3 examples found (or report pattern not used yet)</i>
    <i>All file paths valid and snippets accurate</i>
    <i>Pattern elements clearly identified</i>
    <i>Anti-patterns include concrete recommendations</i>
  </validation>

  <output format="yaml">
pattern_examples:
  pattern: "Result&lt;T>"
  examples_found: 5
  usage_count: 127

  best_examples:
    - file: "src/Modules/Identity/Domain/Entities/WalletOwnership.cs"
      line: 45
      context: "Domain method returning Result&lt;Unit> for revocation"
      snippet: |
        public Result&lt;Unit> Revoke(RevokedBy revokedBy)
        {
            if (IsRevoked)
                return Result&lt;Unit>.Failure(WalletOwnershipErrors.AlreadyRevoked);
            RevokedAt = DateTimeOffset.UtcNow;
            return Result&lt;Unit>.Success(Unit.Value);
        }
      pattern_elements:
        - "Returns Result&lt;Unit> for void operations"
        - "Failure with strongly-typed error"
        - "Success with Unit.Value"

  pattern_variations:
    - type: "Result&lt;Unit>"
      usage: "Void operations (commands)"
      count: 42
    - type: "Result&lt;T>"
      usage: "Value operations (queries)"
      count: 85

  anti_patterns_found:
    - file: "src/OldCode/LegacyService.cs"
      line: 15
      issue: "Throws exception instead of returning Result"
      recommendation: "Refactor to return Result&lt;T>"
  </output>

  <halt-conditions>
    <i>Invalid pattern type - provide list of 5 supported patterns</i>
    <i>Zero pattern usage found - report pattern not yet adopted, suggest introduction</i>
    <i>Grep failures - report tool error, suggest manual search</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md - All 5 patterns documented</i>
    <i>Docs/ENGINEERING/guides/architecture/adrs/003-result-pattern.md - Result&lt;T> ADR</i>
    <i>Docs/ENGINEERING/guides/architecture/adrs/004-strong-ids.md - StrongId&lt;T> ADR</i>
    <i>Example: WalletOwnership, Message, Conversation for pattern references</i>
  </references>
</task>