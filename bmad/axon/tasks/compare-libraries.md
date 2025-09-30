# Compare Libraries Task

<task id="library-sage/compare-libraries" name="Compare Competing Libraries">
  <llm critical="true">
    <i>Compare 2-3 competing libraries for same requirement</i>
    <i>CRITICAL: Data-driven comparison using actual documentation, not opinions</i>
    <i>Compare: Features, Performance, Complexity, Community, Existing usage in codebase</i>
  </llm>

  <flow>
    <step n="1" title="Parse Input & Identify Libraries">
      <action>Extract requirement (e.g., "HTTP client", "Validation", "Testing")</action>
      <action>Identify 2-3 competing libraries</action>
      <action>Example: HTTP → Refit vs HttpClient vs RestSharp</action>
      <action>Example: Validation → FluentValidation vs DataAnnotations</action>
    </step>

    <step n="2" title="Compare Features">
      <action>List features of each library</action>
      <action>Calculate feature parity: Which has most required features?</action>
      <action>Identify unique features (what one has that others don't)</action>
    </step>

    <step n="3" title="Compare Performance & Complexity">
      <action>Performance: Benchmarks if available (throughput, latency)</action>
      <action>Complexity: LOC to implement, API surface area</action>
      <action>Learning curve: Simple / Medium / Complex</action>
    </step>

    <step n="4" title="Compare Community & Maintenance">
      <action>GitHub stars, forks, contributors</action>
      <action>Last update date, release frequency</action>
      <action>Issue response time, PR merge rate</action>
      <action>Documentation quality</action>
    </step>

    <step n="5" title="Check Existing Codebase Usage">
      <action>Grep: Is any library already in use?</action>
      <action>Consistency: Prefer library already used elsewhere</action>
      <action>Migration cost: Cost to switch from current to new library</action>
    </step>

    <step n="6" title="Generate Recommendation">
      <action>Score each library across dimensions</action>
      <action>Recommend: WINNER (best fit), RUNNER-UP (acceptable alternative)</action>
      <action>Rationale: Why winner chosen</action>
    </step>
  </flow>

  <validation>
    <i>At least 2 libraries compared</i>
    <i>All dimensions scored</i>
    <i>Clear winner identified with rationale</i>
    <i>Existing usage checked</i>
  </validation>

  <output format="yaml">
library_comparison:
  requirement: "HTTP client for external API calls"
  libraries_compared: 3

  comparison:
    - library: "Refit"
      features: "Type-safe, interface-based, auto serialization"
      performance: "HIGH"
      complexity: "LOW"
      community: "HIGH (12K stars, active)"
      existing_usage: true
      score: 92%

    - library: "HttpClient (raw)"
      features: "Basic HTTP, manual serialization"
      performance: "HIGH"
      complexity: "MEDIUM"
      community: "N/A (built-in)"
      existing_usage: true
      score: 70%

    - library: "RestSharp"
      features: "Feature-rich, but heavy"
      performance: "MEDIUM"
      complexity: "MEDIUM"
      community: "MEDIUM (9K stars, slower updates)"
      existing_usage: false
      score: 65%

  recommendation:
    winner: "Refit"
    runner_up: "HttpClient"
    rationale: "Refit already in use (Identity module), type-safe, low complexity. RestSharp too heavy for our needs. HttpClient acceptable but more boilerplate."
    migration_cost: "ZERO (Refit already used)"
  </output>

  <halt-conditions>
    <i>Only 1 library found - no comparison needed, just evaluate</i>
    <i>Tie score - present both options, user decides</i>
  </halt-conditions>

  <references>
    <i>Docs/Libraries/00-INDEX.md - All available libraries</i>
    <i>Docs/ENGINEERING/guides/architecture/tech-stack.md - Technology decisions</i>
  </references>
</task>