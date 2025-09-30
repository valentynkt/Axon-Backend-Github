# Validate Acceptance Criteria Task

<task id="quality-guardian/validate-acceptance-criteria" name="100% AC Coverage Validation">
  <llm critical="true">
    <i>Validate 100% acceptance criteria coverage with tests</i>
    <i>CRITICAL: One test per AC, Given-When-Then format</i>
  </llm>

  <flow>
    <step n="1" title="Extract Acceptance Criteria">
      <action>Read story file, extract all ACs (AC1, AC2, AC3...)</action>
      <action>Count total ACs</action>
    </step>

    <step n="2" title="Map Tests to ACs">
      <action>Grep test files for AC references: [Test] public void Given_When_Then_AC1()</action>
      <action>For each AC, find corresponding test</action>
      <action>Calculate coverage: (Tests with AC ref / Total ACs) × 100</action>
    </step>

    <step n="3" title="Identify Gaps">
      <action>List ACs without corresponding tests</action>
      <action>HALT if coverage &lt; 100%</action>
    </step>
  </flow>

  <output format="yaml">
ac_coverage:
  total_acs: 5
  covered_acs: 5
  coverage: 100%
  status: PASS

  mapping:
    - ac: "AC1: Wallet signature validated"
      test: "tests/.../Given_ValidSignature_When_Verify_Then_ReturnsSuccess_AC1.cs"
      status: COVERED
    - ac: "AC2: Invalid signature rejected"
      test: "tests/.../Given_InvalidSignature_When_Verify_Then_ReturnsFailure_AC2.cs"
      status: COVERED
  </output>

  <halt-conditions>
    <i>Coverage &lt; 100% - HALT, generate missing AC tests</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/testing/00-INDEX.md - AC coverage standards</i>
  </references>
</task>
