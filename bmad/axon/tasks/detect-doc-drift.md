# Detect Documentation Drift Task

**Agent**: Axon Doc Oracle
**Purpose**: Compare code vs documentation to detect inconsistencies and prevent drift

---

```xml
<task id="bmad/axon/tasks/detect-doc-drift.md" name="Detect Doc Drift">
  <llm critical="true">
    <i>Zero drift target: Documentation must match code reality</i>
    <i>4 drift types: API, Pattern, Architecture, Domain</i>
  </llm>

  <flow>
    <step n="1" title="Parse Documentation">
      <action>Read module docs: Docs/ENGINEERING/modules/{module}/*.md</action>
      <action>Extract documented APIs from 05-api-contracts.md</action>
      <action>Extract documented entities from 01-domain-model.md</action>
      <action>Extract documented patterns from guides/patterns/*.md</action>
    </step>

    <step n="2" title="Scan Codebase">
      <action>Grep: "class.*Endpoint" in src/Api/Endpoints/{module}/ (actual APIs)</action>
      <action>Grep: "class.*Entity" in src/Modules/{module}/Domain/Entities/ (actual entities)</action>
      <action>Grep: "Result&lt;" in src/Modules/{module}/Domain/ (Result pattern usage)</action>
      <action>Grep: "StrongId&lt;" in src/Modules/{module}/Domain/ (StrongId usage)</action>
    </step>

    <step n="3" title="Compare and Detect Drift">
      <action>For each documented item: Does it exist in code? Does code match docs?</action>
      <action>For each code item: Is it documented? Properly described?</action>
      <action>Categorize drift: API / Pattern / Architecture / Domain</action>
      <action>Assign severity: HIGH (public API) / MEDIUM (domain) / LOW (internal)</action>
    </step>

    <step n="4" title="Calculate Drift Score">
      <action>Count items with drift vs total items</action>
      <action>Calculate: (Drifted / Total) × 100</action>
      <action>Status: PASS=0%, WARN=1-10%, FAIL>10%</action>
    </step>

    <step n="5" title="Output Drift Report">
      <output format="yaml">
drift_report:
  overall_drift: {percentage}%
  status: PASS / WARN / FAIL
  items_total: {count}
  items_drifted: {count}

  drifts:
    - type: API / Pattern / Architecture / Domain
      severity: HIGH / MEDIUM / LOW
      item: {item-name}
      issue: {what-is-wrong}
      file_code: {code-file}:{line}
      file_doc: {doc-file}
      recommendation: {how-to-fix}
      </output>
    </step>
  </flow>

  <drift-types>
    <i>API: Endpoints, contracts, HTTP methods</i>
    <i>Pattern: Result&lt;T&gt;, StrongId&lt;T&gt;, CQRS, events</i>
    <i>Architecture: Layers, modules, dependencies</i>
    <i>Domain: Entities, aggregates, business rules</i>
  </drift-types>

  <validation>
    <i>Zero drift is the goal (0%)</i>
    <i>All drifts must have recommendations</i>
    <i>HIGH severity drifts block commit</i>
  </validation>

  <references>
    <i>Module docs: Docs/ENGINEERING/modules/{module}/</i>
    <i>Pattern docs: Docs/ENGINEERING/guides/patterns/</i>
  </references>
</task>
```