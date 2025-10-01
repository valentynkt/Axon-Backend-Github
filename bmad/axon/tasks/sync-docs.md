# Sync Docs Task

<task id="quality-guardian/sync-docs" name="Documentation Synchronization">
  <llm critical="true">
    <i>Sync documentation with code changes - prevent drift</i>
    <i>CRITICAL: Update affected docs, verify accuracy</i>
  </llm>

  <flow>
    <step n="1" title="Detect Drift">
      <action>Run detect-doc-drift task</action>
      <action>Identify: Which docs are affected by code changes</action>
    </step>

    <step n="2" title="Update Docs">
      <action>Module docs: Update domain model, API contracts, database schema</action>
      <action>Pattern docs: Update if new pattern usage discovered</action>
      <action>ADRs: No changes (ADRs are immutable decisions)</action>
    </step>

    <step n="3" title="Validate Updates">
      <action>Read updated docs, verify accuracy</action>
      <action>Check: Code references (file:line) are correct</action>
      <action>Check: No stale information remains</action>
    </step>
  </flow>

  <action>Append doc sync summary to: {implementation_log}</action>
  <output format="yaml">
doc_sync:
  docs_updated: 3
  changes:
    - doc: "Docs/ENGINEERING/modules/identity/01-domain-model.md"
      section: "WalletOwnership"
      change: "Added AutoRevokeAt property"
      code_ref: "src/.../WalletOwnership.cs:15"

  drift_after_sync: 0
  status: COMPLETE
  </output>

  <halt-conditions>
    <i>Drift detected after sync - re-sync until drift = 0</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/modules/{module}/00-INDEX.md - Module docs</i>
    <i>Templates: doc-update-template.md for format</i>
  </references>
</task>
