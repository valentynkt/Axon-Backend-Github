# Compliance Score Task

**Agent**: Axon Doc Oracle
**Purpose**: Calculate weighted compliance score across 6 pattern dimensions

---

```xml
<task id="bmad/axon/tasks/compliance-score.md" name="Compliance Score">
  <llm critical="true">
    <i>6 dimensions: Result&lt;T&gt; (25%), StrongId&lt;T&gt; (20%), CQRS (20%), Layers (15%), Events (10%), Owned (10%)</i>
    <i>Target: 95% overall for PASS</i>
  </llm>

  <flow>
    <step n="1" title="Score Result&lt;T&gt; Pattern (25%)">
      <action>Grep: "Result&lt;" in src/Modules/{module}/Domain/</action>
      <action>Check: All domain methods return Result&lt;T&gt;</action>
      <action>Check: No try-catch in domain (exceptions avoided)</action>
      <action>Calculate: (Checks Passed / Total) × 100</action>
    </step>

    <step n="2" title="Score StrongId&lt;T&gt; Pattern (20%)">
      <action>Grep: "StrongId&lt;" vs "Guid" in entity IDs</action>
      <action>Check: All entity IDs are StrongId&lt;T&gt;</action>
      <action>Check: No primitive IDs (Guid, int, string)</action>
    </step>

    <step n="3" title="Score CQRS (20%)">
      <action>Grep: "IRequest&lt;Result" in Application/Commands/ and Queries/</action>
      <action>Check: Commands separate from queries</action>
      <action>Check: All operations use MediatR handlers</action>
    </step>

    <step n="4" title="Score Clean Architecture (15%)">
      <action>Check: Domain has no external dependencies</action>
      <action>Check: Application → Domain only</action>
      <action>Check: Infrastructure → Application + Domain</action>
    </step>

    <step n="5" title="Score Domain Events (10%)">
      <action>Grep: "IDomainEvent" in Domain/Events/</action>
      <action>Check: Aggregates raise events on state changes</action>
    </step>

    <step n="6" title="Score Owned Entities (10%)">
      <action>Grep: "OwnsMany" in Infrastructure/Persistence/</action>
      <action>Check: Owned entities properly configured</action>
    </step>

    <step n="7" title="Calculate Weighted Total">
      <action>Total = Σ(Dimension Score × Weight)</action>
      <action>Status: PASS ≥95%, WARN 90-94%, FAIL &lt;90%</action>
    </step>

    <step n="8" title="Output Compliance Score">
      <output format="yaml">
compliance_score:
  overall: {score}
  target: 95
  status: PASS / WARN / FAIL
  grade: A / B / C / F

  dimensions:
    - name: Result&lt;T&gt;
      score: {score}
      weight: 25
      status: PASS/WARN/FAIL
    # ... all 6 dimensions

  weighted_calculation:
    result_pattern: {weighted-score}
    strong_ids: {weighted-score}
    cqrs: {weighted-score}
    layers: {weighted-score}
    events: {weighted-score}
    owned: {weighted-score}
    total: {total-score}
      </output>
    </step>
  </flow>

  <scoring-dimensions>
    <i>Result&lt;T&gt; (25%): Domain error handling</i>
    <i>StrongId&lt;T&gt; (20%): Type-safe IDs</i>
    <i>CQRS (20%): Command/query separation</i>
    <i>Layers (15%): Clean Architecture</i>
    <i>Events (10%): Domain events</i>
    <i>Owned (10%): Owned entities</i>
  </scoring-dimensions>

  <validation>
    <i>All dimensions must be scored (0-100)</i>
    <i>Weighted total must equal 100</i>
    <i>Grade: A=95+, B=90-94, C=85-89, F=&lt;85</i>
  </validation>
</task>
```
