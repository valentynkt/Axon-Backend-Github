# Capture Decision Task

**Agent**: Axon Quality Guardian
**Purpose**: Capture decisions made during story implementation in YAML decision log for learning

---

```xml
<task id="bmad/axon/tasks/capture-decision.md" name="Capture Decision">
  <llm critical="true">
    <i>Capture ALL significant decisions for future reference</i>
    <i>Use decision-log-template.yaml format</i>
    <i>Include traceability (why, alternatives, impact)</i>
  </llm>

  <flow>
    <step n="1" title="Collect Decision Data">
      <action>List all decisions made during implementation</action>
      <action>Categories: Architecture, Pattern, Library, Approach, Refactoring, Testing</action>
      <action>For each decision, capture: What, Why, Alternatives Considered, Impact, Confidence</action>
    </step>

    <step n="2" title="Capture Learning">
      <action>What worked well: Successes, effective practices</action>
      <action>Challenges faced: Obstacles, blockers encountered</action>
      <action>Improvements for next story: Process optimizations</action>
      <action>Patterns reinforced: Pattern compliance wins</action>
      <action>Doc gaps discovered: Missing/outdated documentation</action>
    </step>

    <step n="3" title="Generate Decision Log">
      <action>Use template: bmad/axon/templates/decision-log-template.yaml</action>
      <action>Fill story metadata (story_id, title, module, type)</action>
      <action>Add decision entries with timestamps, agent, category, rationale</action>
      <action>Add learning section</action>
      <action>Calculate metrics (total decisions, by category, confidence distribution)</action>
    </step>

    <step n="4" title="Save Decision Log">
      <action>Write to: {story_workspace}/decisions.yaml</action>
      <action>Validate YAML syntax</action>
    </step>

    <step n="5" title="Output Capture Result">
      <output format="yaml">
decision_capture_result:
  story_id: {story-id}
  decision_log_path: {story_workspace}/decisions.yaml

  summary:
    total_decisions: {count}
    by_category:
      architecture: {count}
      pattern: {count}
      library: {count}
      approach: {count}
      refactoring: {count}
      testing: {count}

    confidence_distribution:
      high: {count}
      medium: {count}
      low: {count}

  learning_captured:
    what_worked: {count} items
    challenges: {count} items
    improvements: {count} items
    doc_gaps: {count} items
      </output>
    </step>
  </flow>

  <decision-categories>
    <i>Architecture: Layering, module boundaries, dependencies</i>
    <i>Pattern: Result&lt;T&gt;, StrongId&lt;T&gt;, CQRS, domain events</i>
    <i>Library: Library selection, library vs manual</i>
    <i>Approach: Implementation strategy, reuse vs create</i>
    <i>Refactoring: Code improvements, tech debt addressed</i>
    <i>Testing: Test strategy, coverage approach</i>
  </decision-categories>

  <validation>
    <i>All significant decisions captured (≥3 per story)</i>
    <i>Each decision has rationale + alternatives</i>
    <i>Learning section filled (what worked, challenges)</i>
    <i>Valid YAML syntax</i>
  </validation>

  <references>
    <i>Template: bmad/axon/templates/decision-log-template.yaml</i>
    <i>Decision log location: {story_workspace}/decisions.yaml</i>
  </references>
</task>
```