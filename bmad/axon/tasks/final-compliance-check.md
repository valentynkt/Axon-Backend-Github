# Final Compliance Check Task

<task id="quality-guardian/final-compliance-check" name="Final Pattern Compliance Check">
  <llm critical="true">
    <i>Final validation before commit: Patterns, Tests, Build, AC coverage</i>
    <i>CRITICAL: All gates must pass - no exceptions</i>
  </llm>

  <flow>
    <step n="1" title="Pattern Compliance">
      <action>Run validate-code task</action>
      <action>Target: ≥95% compliance</action>
    </step>

    <step n="2" title="Test Coverage">
      <action>Run: dotnet test --collect:"XPlat Code Coverage"</action>
      <action>Target: ≥90% coverage</action>
    </step>

    <step n="3" title="AC Coverage">
      <action>Run validate-acceptance-criteria task</action>
      <action>Target: 100% coverage</action>
    </step>

    <step n="4" title="Build Success">
      <action>Run: dotnet build --no-incremental</action>
      <action>Target: 100% success (0 warnings, TreatWarningsAsErrors=true)</action>
    </step>

    <step n="5" title="Doc Sync">
      <action>Run detect-doc-drift task</action>
      <action>Target: Zero drift</action>
    </step>
  </flow>

  <output format="yaml">
compliance_check:
  pattern_compliance: 98%  # Target: ≥95%
  test_coverage: 92%       # Target: ≥90%
  ac_coverage: 100%        # Target: 100%
  build_success: 100%      # Target: 100%
  doc_drift: 0             # Target: 0

  overall_status: PASS
  ready_for_commit: true
  </output>

  <halt-conditions>
    <i>Any gate fails - HALT, fix before commit</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md - Compliance targets</i>
  </references>
</task>
