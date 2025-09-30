# Suggest Approach Task

<task id="library-sage/suggest-approach" name="Library vs Manual Recommendation">
  <llm critical="true">
    <i>Recommend library-first vs manual implementation using 4-factor scoring</i>
    <i>CRITICAL: Make data-driven decision, not gut feeling. Calculate scores across 4 dimensions.</i>
    <i>Factors: Feature match (40%), Complexity reduction (30%), Maintainability (20%), Learning curve (10%)</i>
    <i>Threshold: ≥70% → LIBRARY, 40-69% → HYBRID, &lt;40% → MANUAL</i>
  </llm>

  <flow>
    <step n="1" title="Parse Input & Gather Library Info">
      <action>Extract requirement description</action>
      <action>Extract candidate library name (from check-library result or user input)</action>
      <action>Read library implementation guide: Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md</action>
      <action>Extract: Features, complexity, learning curve, examples</action>
    </step>

    <step n="2" title="Score Factor 1 - Feature Match (40%)">
      <action>List required features (from story requirements)</action>
      <action>List library features (from implementation guide)</action>
      <action>Calculate match: (Matching features / Required features) × 100</action>
      <action>Weight: 40% of total score</action>
    </step>

    <step n="3" title="Score Factor 2 - Complexity Reduction (30%)">
      <action>Estimate manual LOC: How many lines to implement manually?</action>
      <action>Estimate library LOC: How many lines using library?</action>
      <action>Calculate reduction: (1 - Library LOC / Manual LOC) × 100</action>
      <action>Weight: 30% of total score</action>
    </step>

    <step n="4" title="Score Factor 3 - Maintainability (20%)">
      <action>Library maintained? Check last update date, GitHub stars/activity</action>
      <action>Community support? Active issues/PRs, Stack Overflow posts</action>
      <action>Breaking changes history? Major version upgrades frequency</action>
      <action>Score: High (90), Medium (60), Low (30)</action>
      <action>Weight: 20% of total score</action>
    </step>

    <step n="5" title="Score Factor 4 - Learning Curve (10%)">
      <action>How complex is library API? Simple/Medium/Complex</action>
      <action>Documentation quality? Excellent/Good/Poor</action>
      <action>Team familiarity? Already using / New library</action>
      <action>Score: Low curve (90), Medium (60), High (30)</action>
      <action>Weight: 10% of total score</action>
    </step>

    <step n="6" title="Calculate Total Score & Recommend">
      <action>Total = (Feature 40%) + (Complexity 30%) + (Maintainability 20%) + (Learning 10%)</action>
      <action>≥70% → LIBRARY (strong recommendation)</action>
      <action>40-69% → HYBRID (library for some parts, manual for others)</action>
      <action>&lt;40% → MANUAL (library wrong fit or overkill)</action>
      <action>Provide confidence level: HIGH (75-100%), MEDIUM (50-74%), LOW (&lt;50%)</action>
    </step>
  </flow>

  <validation>
    <i>All 4 factors scored (not skipped)</i>
    <i>Total score between 0-100%</i>
    <i>Recommendation matches score threshold</i>
    <i>Rationale explains why each score was given</i>
  </validation>

  <output format="yaml">
approach_recommendation:
  requirement: "Validate wallet signature for Solana/Ethereum"
  library: "Dynamic Auth"

  scoring:
    feature_match:
      score: 95%
      weight: 40%
      weighted: 38%
      rationale: "Supports Solana (Ed25519), Ethereum (ECDSA), all EVM chains. Covers 95% of requirements."

    complexity_reduction:
      manual_loc_estimate: 500
      library_loc_estimate: 50
      reduction: 90%
      weight: 30%
      weighted: 27%
      rationale: "Manual crypto implementation = 500 LOC. Library = 50 LOC (90% reduction)."

    maintainability:
      library_maintained: true
      last_update: "2025-09"
      community_support: "HIGH"
      breaking_changes: "LOW"
      score: 90%
      weight: 20%
      weighted: 18%
      rationale: "Active maintenance by Dynamic.xyz team. Monthly updates. 5K+ users."

    learning_curve:
      api_complexity: "Simple"
      documentation_quality: "Excellent"
      team_familiarity: "New library"
      score: 70%
      weight: 10%
      weighted: 7%
      rationale: "Simple API but new library. Excellent docs mitigate learning curve."

  total_score: 90%
  recommendation: LIBRARY
  confidence: HIGH
  effort_saved: "80-90%"

  rationale: "Strong library fit (90% score). Feature match excellent (95%), massive complexity reduction (90%), well-maintained (90%). Learning curve acceptable (70%) given docs quality. Manual implementation would require deep cryptography knowledge and 500+ LOC."
  </output>

  <halt-conditions>
    <i>Cannot estimate LOC - ask for more requirement details</i>
    <i>Library documentation missing - warn cannot fully evaluate, suggest caution</i>
    <i>Score borderline (68-72%) - present both options, let user decide</i>
  </halt-conditions>

  <references>
    <i>Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md - Library capabilities</i>
    <i>Docs/ENGINEERING/guides/architecture/tech-stack.md - Library selection criteria</i>
    <i>Example: Dynamic Auth (HIGH score), OpenAI (HIGH score), Custom crypto (LOW score)</i>
  </references>
</task>