# Validate Usage Task

<task id="library-sage/validate-usage" name="Validate Library Best Practices">
  <llm critical="true">
    <i>Validate library usage against best practices (4 dimensions)</i>
    <i>Dimensions: Correct API usage, Error handling, Performance, Security</i>
    <i>Score each dimension: PASS (green), WARN (yellow), FAIL (red)</i>
  </llm>

  <flow>
    <step n="1" title="Parse Input & Read Code">
      <action>Extract target code (file:line range)</action>
      <action>Extract library being validated</action>
      <action>Read target code snippet</action>
      <action>Read library implementation guide for best practices</action>
    </step>

    <step n="2" title="Validate Dimension 1 - Correct API Usage">
      <action>Check: Using correct methods/interfaces</action>
      <action>Check: Proper initialization/configuration</action>
      <action>Check: Not using deprecated APIs</action>
      <action>Score: PASS / WARN / FAIL</action>
    </step>

    <step n="3" title="Validate Dimension 2 - Error Handling">
      <action>Check: Errors caught and handled</action>
      <action>Check: Result&lt;T> pattern used (not exceptions)</action>
      <action>Check: Meaningful error messages</action>
      <action>Score: PASS / WARN / FAIL</action>
    </step>

    <step n="4" title="Validate Dimension 3 - Performance">
      <action>Check: No N+1 queries (EF Core)</action>
      <action>Check: Async/await used properly</action>
      <action>Check: Proper resource disposal (using/IDisposable)</action>
      <action>Score: PASS / WARN / FAIL</action>
    </step>

    <step n="5" title="Validate Dimension 4 - Security">
      <action>Check: Input validation present</action>
      <action>Check: SQL injection prevention (parameterized queries)</action>
      <action>Check: Secrets not hardcoded</action>
      <action>Score: PASS / WARN / FAIL</action>
    </step>
  </flow>

  <validation>
    <i>All 4 dimensions scored</i>
    <i>Issues include specific file:line references</i>
    <i>Recommendations actionable</i>
  </validation>

  <output format="yaml">
usage_validation:
  library: "EF Core"
  file: "src/Modules/Identity/Infrastructure/Persistence/IdentityRepository.cs"

  dimensions:
    correct_api_usage:
      score: PASS
      notes: "Using AsNoTracking() for queries, proper DbContext usage"

    error_handling:
      score: WARN
      issues:
        - line: 45
          issue: "Exception thrown instead of Result&lt;T>"
          recommendation: "Return Result&lt;T>.Failure instead"

    performance:
      score: PASS
      notes: "Async/await used, Include() for eager loading, no N+1"

    security:
      score: PASS
      notes: "Parameterized queries, input validated in Application layer"

  overall: WARN
  critical_issues: 0
  warnings: 1
  </output>

  <halt-conditions>
    <i>FAIL score - halt implementation, fix issues first</i>
    <i>Library not found in code - cannot validate</i>
  </halt-conditions>

  <references>
    <i>Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md - Best practices section</i>
  </references>
</task>