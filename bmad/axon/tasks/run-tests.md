# Run Tests Task

**Agent**: Axon Quality Guardian
**Purpose**: Execute all tests with coverage metrics and verify 90%+ coverage

---

```xml
<task id="bmad/axon/tasks/run-tests.md" name="Run Tests">
  <llm critical="true">
    <i>MANDATORY: All tests MUST pass (zero failures)</i>
    <i>Coverage MUST be ≥90% overall, ≥95% domain</i>
  </llm>

  <flow>
    <step n="1" title="Run Tests with Coverage">
      <action>Run: dotnet test --no-build --collect:"XPlat Code Coverage" --results-directory ./coverage</action>
      <action>Capture test output (passed, failed, skipped)</action>
      <action>Capture coverage data (coverage.cobertura.xml)</action>
    </step>

    <step n="2" title="Parse Test Results">
      <action>Count total tests executed</action>
      <action>Count passed tests</action>
      <action>Count failed tests (must be 0)</action>
      <action>Count skipped tests</action>
      <action>Calculate pass rate (%)</action>
    </step>

    <step n="3" title="Parse Coverage Results">
      <action>Read coverage.cobertura.xml</action>
      <action>Extract overall coverage %</action>
      <action>Extract coverage by layer (Domain, Application, Infrastructure, API)</action>
      <action>Validate: Overall ≥90%, Domain ≥95%</action>
    </step>

    <step n="4" title="Output Test Result">
      <output format="yaml">
test_result:
  status: PASS / FAIL

  tests:
    total: {count}
    passed: {count}
    failed: {count}
    skipped: {count}
    pass_rate: {percentage}%

  coverage:
    overall: {percentage}%
    domain: {percentage}%
    application: {percentage}%
    infrastructure: {percentage}%
    api: {percentage}%
    status: PASS / FAIL  # Based on ≥90% overall, ≥95% domain

  failures:  # If failed > 0
    - test: {test-name}
      message: {failure-message}
      stack_trace: {trace}
      </output>
    </step>
  </flow>

  <validation>
    <i>All tests must pass (failed = 0)</i>
    <i>Overall coverage ≥90%</i>
    <i>Domain coverage ≥95%</i>
    <i>No skipped tests in critical modules</i>
  </validation>

  <halt-conditions>
    <i>HALT if any test fails (failed > 0)</i>
    <i>HALT if coverage < 90% overall</i>
    <i>HALT if domain coverage < 95%</i>
    <i>Report failures clearly with test name + message</i>
  </halt-conditions>

  <references>
    <i>Test projects: tests/Modules/Identity/, tests/Modules/Chat/</i>
    <i>Coverage tool: XPlat Code Coverage (built-in)</i>
  </references>
</task>
```