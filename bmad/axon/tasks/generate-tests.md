# Generate Tests Task

**Agent**: Axon Quality Guardian
**Purpose**: Generate comprehensive tests (Domain, Application, API) with 90%+ coverage and 100% AC coverage

---

```xml
<task id="bmad/axon/tasks/generate-tests.md" name="Generate Tests">
  <llm critical="true">
    <i>MANDATORY: Execute ALL steps in flow section IN EXACT ORDER</i>
    <i>DO NOT skip steps or change sequence</i>
    <i>Each &lt;action&gt; is REQUIRED to complete that step</i>
  </llm>

  <flow>
    <step n="1" title="Analyze Generated Code">
      <action>Use Grep to find all commands: "class.*Command.*IRequest" in src/Modules/{module}/Application/Commands/</action>
      <action>Use Grep to find all queries: "class.*Query.*IRequest" in src/Modules/{module}/Application/Queries/</action>
      <action>Use Grep to find all entities: "class.*Entity" in src/Modules/{module}/Domain/Entities/</action>
      <action>Use Grep to find all endpoints: "class.*Endpoint" in src/Api/Endpoints/{module}/</action>
      <action>List all testable units (entities, commands, queries, endpoints)</action>
    </step>

    <step n="2" title="Generate Domain Unit Tests">
      <action>For each entity, create test file: tests/Modules/{module}/Domain/Entities/{Entity}Tests.cs</action>
      <action>Test pattern: AAA (Arrange-Act-Assert)</action>
      <action>Test Result&lt;T&gt; success/failure paths</action>
      <action>Test StrongId&lt;T&gt; usage</action>
      <action>Test domain events raised</action>
      <action>Use Shouldly assertions, NUnit framework</action>
    </step>

    <step n="3" title="Generate Application Integration Tests">
      <action>For each command/query, create test file: tests/Modules/{module}/Application/Commands/{Command}HandlerTests.cs</action>
      <action>Extend IntegrationTestBase&lt;DbContext&gt; (Testcontainers)</action>
      <action>Test MediatR handler execution (Sender.Send)</action>
      <action>Test database state changes</action>
      <action>Test Result&lt;T&gt; error handling</action>
    </step>

    <step n="4" title="Generate API E2E Tests">
      <action>For each endpoint, create test file: tests/Modules/{module}/E2E/{Feature}E2ETests.cs</action>
      <action>Extend E2ETestBase (WebApplicationFactory)</action>
      <action>Test HTTP status codes (200, 400, 404, etc.)</action>
      <action>Test request/response contracts</action>
      <action>Use full HTTP stack (Client.PostAsJsonAsync, etc.)</action>
    </step>

    <step n="5" title="Generate AC Coverage Tests">
      <action>For each acceptance criterion, create test: Given_When_Then naming</action>
      <action>Test observable behavior (not implementation)</action>
      <action>Ensure 100% AC coverage (one test per AC)</action>
    </step>

    <step n="6" title="Run Tests and Calculate Coverage">
      <action>Run: dotnet test --collect:"XPlat Code Coverage"</action>
      <action>Verify: Overall ≥90%, Domain ≥95%, Application ≥90%</action>
      <action>Generate coverage report</action>
    </step>

    <step n="7" title="Output Test Generation Result">
      <action>Append test generation summary to: {implementation_log}</action>
      <output format="yaml">
test_generation_result:
  story_id: {story-id}
  module: {module}
  tests_generated:
    domain_unit_tests: {count}
    application_integration_tests: {count}
    api_e2e_tests: {count}
    acceptance_criteria_tests: {count}
  coverage_metrics:
    overall: {percentage}%
    domain: {percentage}%
    application: {percentage}%
    status: PASS/FAIL
  quality_gates:
    - gate: "90%+ coverage"
      status: PASS/FAIL
    - gate: "100% AC coverage"
      status: PASS/FAIL
    - gate: "All tests pass"
      status: PASS/FAIL
      </output>
    </step>
  </flow>

  <validation>
    <i>Domain tests: Pure unit tests, no dependencies</i>
    <i>Application tests: Use Testcontainers (PostgreSQL)</i>
    <i>API tests: Use WebApplicationFactory (full stack)</i>
    <i>AC tests: Given-When-Then naming, one per AC</i>
    <i>Coverage: ≥90% overall, ≥95% domain</i>
    <i>All tests pass: dotnet test exits with code 0</i>
  </validation>

  <patterns>
    <i>Test Result&lt;T&gt; success/failure paths</i>
    <i>Test StrongId&lt;T&gt; type-safety</i>
    <i>Test CQRS (MediatR Sender.Send)</i>
    <i>Test domain events raised</i>
    <i>Use Shouldly assertions (fluent)</i>
  </patterns>

  <references>
    <i>Test examples: tests/Modules/Identity/, tests/Modules/Chat/</i>
    <i>Test base classes: tests/BuildingBlocks/Infrastructure/IntegrationTestBase.cs</i>
    <i>Test docs: Docs/ENGINEERING/testing/00-INDEX.md</i>
  </references>
</task>
```