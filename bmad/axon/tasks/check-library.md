# Check Library Task

<task id="library-sage/check-library" name="Check Library Capabilities">
  <llm critical="true">
    <i>Check if library provides out-of-box solution for requirement</i>
    <i>CRITICAL: Read actual implementation guide from Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md</i>
    <i>11 core libraries: MediatR, FastEndpoints, FluentValidation, EF Core, Dynamic Auth, OpenAI, MCP, Refit, Serilog, Shouldly, NUnit</i>
    <i>Recommendation: LIBRARY (use out-of-box), MANUAL (write custom), HYBRID (library + custom)</i>
  </llm>

  <flow>
    <step n="1" title="Parse Requirement">
      <action>Extract requirement (e.g., "Validate wallet signature", "Send HTTP request", "Write structured logs")</action>
      <action>Identify domain (Identity, Chat, API, Infrastructure)</action>
      <action>Extract key capabilities needed</action>
    </step>

    <step n="2" title="Identify Candidate Libraries">
      <action>Match requirement to 11 core libraries:</action>
      <action>CQRS/messaging → MediatR</action>
      <action>API endpoints → FastEndpoints</action>
      <action>Validation → FluentValidation</action>
      <action>Persistence → EF Core</action>
      <action>Web3 auth → Dynamic Auth</action>
      <action>AI → OpenAI/MCP</action>
      <action>HTTP client → Refit</action>
      <action>Logging → Serilog</action>
      <action>Testing → NUnit/Shouldly</action>
      <action>List 1-3 candidate libraries</action>
    </step>

    <step n="3" title="Check Library Capabilities">
      <action>For each candidate, read: Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md</action>
      <action>Search for: Keywords related to requirement (signature, validation, etc.)</action>
      <action>Check "Capabilities" or "Features" section</action>
      <action>Look for usage examples matching requirement</action>
      <action>Determine if library provides: Complete solution / Partial solution / No solution</action>
    </step>

    <step n="4" title="Calculate Confidence & Recommendation">
      <action>LIBRARY: Library provides ≥80% of requirement (HIGH confidence)</action>
      <action>HYBRID: Library provides 40-79% (MEDIUM confidence)</action>
      <action>MANUAL: Library provides &lt;40% or wrong fit (LOW confidence for library)</action>
      <action>Reference specific implementation guide section</action>
    </step>
  </flow>

  <validation>
    <i>At least 1 library checked</i>
    <i>Implementation guide actually read (not guessed)</i>
    <i>Confidence level justified by capability match</i>
    <i>Usage example path valid (exists in Docs/Libraries/)</i>
  </validation>

  <output format="yaml">
library_check:
  requirement: "Validate wallet signature"
  domain: "Identity"

  candidate_libraries:
    - name: "Dynamic Auth"
      capability_found: true
      coverage: 95%
      recommendation: LIBRARY
      confidence: HIGH
      usage_example: "Docs/Libraries/dynamic_auth/IMPLEMENTATION_GUIDE.md#wallet-verification"
      notes: "Provides wallet signature verification for Solana/Ethereum/EVM chains out-of-box"

    - name: "Manual Cryptography"
      capability_found: false
      coverage: 0%
      recommendation: MANUAL
      confidence: LOW
      notes: "Would require implementing Ed25519 verification manually - unnecessary when Dynamic Auth exists"

  final_recommendation:
    approach: LIBRARY
    library: "Dynamic Auth"
    confidence: HIGH
    effort_saved: "80% (avoid manual crypto implementation)"
    references:
      - "Docs/Libraries/dynamic_auth/IMPLEMENTATION_GUIDE.md"
      - "Docs/ENGINEERING/integrations/dynamic-xyz/authentication-flow.md"
  </output>

  <halt-conditions>
    <i>No candidate library identified - ask user for more requirement context</i>
    <i>Implementation guide not found - warn missing documentation, suggest creating it</i>
    <i>Multiple libraries equal fit - present options, let user choose</i>
  </halt-conditions>

  <references>
    <i>Docs/Libraries/00-INDEX.md - Master library catalog (15 libraries)</i>
    <i>Docs/ENGINEERING/guides/architecture/tech-stack.md - Technology decisions</i>
    <i>Example: Dynamic Auth for wallet verification, MediatR for CQRS, Refit for HTTP</i>
  </references>
</task>