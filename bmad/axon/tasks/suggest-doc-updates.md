# Suggest Documentation Updates Task

**Agent**: Axon Doc Oracle
**Purpose**: Generate documentation update recommendations based on code changes

---

```xml
<task id="bmad/axon/tasks/suggest-doc-updates.md" name="Suggest Doc Updates">
  <llm critical="true">
    <i>Map code changes to documentation files</i>
    <i>Prioritize: CRITICAL (APIs), HIGH (domain), MEDIUM (flows), LOW (internal)</i>
  </llm>

  <flow>
    <step n="1" title="Analyze Code Changes">
      <action>Read changed files (from git diff or Implementation Surgeon output)</action>
      <action>Categorize changes: New entity / New endpoint / Changed rule / Pattern usage / Schema change</action>
    </step>

    <step n="2" title="Map to Documentation Files">
      <action>Entity change → Docs/ENGINEERING/modules/{module}/01-domain-model.md</action>
      <action>Endpoint change → Docs/ENGINEERING/modules/{module}/05-api-contracts.md</action>
      <action>Rule change → Docs/ENGINEERING/modules/{module}/03-*.md (flows)</action>
      <action>Schema change → Docs/ENGINEERING/modules/{module}/06-database-schema.md</action>
    </step>

    <step n="3" title="Generate Update Suggestions">
      <action>Identify exact section to update in doc file</action>
      <action>Provide before/after snippet recommendation</action>
      <action>Explain why update is needed (code reference)</action>
      <action>Assign priority: CRITICAL / HIGH / MEDIUM / LOW</action>
    </step>

    <step n="4" title="Output Suggestions">
      <output format="yaml">
doc_update_suggestions:
  story_id: {story-id}
  changes_detected: {count}

  suggestions:
    - priority: CRITICAL / HIGH / MEDIUM / LOW
      doc_file: {doc-path}
      section: {section-name}
      reason: {why-update-needed}
      change_type: {type}
      suggested_update: |
        {markdown-snippet}
      code_reference: {code-file}:{line}
      </output>
    </step>
  </flow>

  <change-mapping>
    <i>Domain entities → 01-domain-model.md</i>
    <i>API endpoints → 05-api-contracts.md</i>
    <i>Business rules → 03-*-flows.md</i>
    <i>Database schema → 06-database-schema.md</i>
  </change-mapping>

  <priority-levels>
    <i>CRITICAL: API contracts, domain models (public interface)</i>
    <i>HIGH: Business rules, authentication flows</i>
    <i>MEDIUM: Implementation details, examples</i>
    <i>LOW: Internal refactorings</i>
  </priority-levels>

  <validation>
    <i>All code changes must map to docs</i>
    <i>CRITICAL updates must not be skipped</i>
    <i>Suggestions must be actionable (clear sections + snippets)</i>
  </validation>

  <references>
    <i>Module docs: Docs/ENGINEERING/modules/</i>
    <i>Template: bmad/axon/templates/doc-update-template.md</i>
  </references>
</task>
```
