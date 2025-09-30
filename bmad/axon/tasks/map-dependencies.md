# Map Dependencies Task

<task id="archaeologist/map-dependencies" name="Map Dependencies & Calculate Blast Radius">
  <llm critical="true">
    <i>Map ALL dependencies for target code and calculate impact analysis (blast radius)</i>
    <i>CRITICAL: Find EVERY usage - same module, cross-module, API, infrastructure, tests</i>
    <i>Calculate blast radius: SMALL (1-5 files), MEDIUM (6-15), LARGE (16+)</i>
    <i>Identify breaking changes: Public API, database schema, cross-module contracts, events</i>
  </llm>

  <flow>
    <step n="1" title="Parse Input & Validate Target">
      <action>Extract target code (class/method/entity to be changed)</action>
      <action>Extract change type (modify/delete/rename/move)</action>
      <action>Validate target exists: Read file to confirm class/method exists</action>
      <action>HALT if target not found with error message</action>
    </step>

    <step n="2" title="Find All Usages (Comprehensive Search)">
      <action>Direct usages: Grep "{TargetName}" in src/ (case-sensitive)</action>
      <action>Type references: Grep "{TargetName}" in method signatures, constructors, properties</action>
      <action>Property access: Grep "{TargetName}\\..*" (accessing members)</action>
      <action>Inheritance: Grep "class.*:.*{TargetName}" (subtypes)</action>
      <action>Record ALL file:line occurrences</action>
    </step>

    <step n="3" title="Categorize Dependencies by Layer">
      <action>Same Module: Usages within same module (e.g., Identity → Identity)</action>
      <action>Cross-Module: Usages from other modules (e.g., Chat → Identity)</action>
      <action>API Layer: HTTP endpoints (src/Api/)</action>
      <action>Infrastructure: Repositories, configurations, migrations</action>
      <action>Tests: All test files (tests/)</action>
      <action>Count usages per category</action>
    </step>

    <step n="4" title="Calculate Blast Radius">
      <action>Count total files affected</action>
      <action>Count modules affected (same vs cross-module)</action>
      <action>Classify: SMALL (1-5 files), MEDIUM (6-15 files), LARGE (16+ files)</action>
      <action>Calculate impact per file: LOW (read-only), MEDIUM (constructor calls), HIGH (schema/config)</action>
    </step>

    <step n="5" title="Identify Breaking Changes">
      <action>DATABASE_SCHEMA: Check Infrastructure/Persistence/Configurations/ for entity config changes</action>
      <action>API_CONTRACT: Check src/Api/ for public endpoint changes</action>
      <action>CROSS_MODULE: Check if other modules depend on this code</action>
      <action>EVENT_SIGNATURE: Check Domain/Events/ for event contract changes</action>
      <action>For each breaking change, suggest mitigation strategy</action>
    </step>

    <step n="6" title="Generate Recommendations">
      <action>Backward compatibility: Suggest nullable properties, optional parameters, versioning</action>
      <action>Migration strategy: Database migrations, API versioning, deprecated warnings</action>
      <action>Testing strategy: Which test suites must pass (module + cross-module)</action>
      <action>Rollback plan: How to safely revert changes</action>
    </step>
  </flow>

  <validation>
    <i>All usages found (comprehensive grep across src/ and tests/)</i>
    <i>Blast radius classification matches file count</i>
    <i>Breaking changes identified with mitigation strategies</i>
    <i>Recommendations actionable and specific</i>
  </validation>

  <output format="yaml">
dependency_map:
  target: "WalletOwnership"
  target_file: "src/Modules/Identity/Domain/Entities/WalletOwnership.cs"
  change_type: "modify"
  total_usages: 24

  blast_radius:
    size: MEDIUM
    files_affected: 12
    modules_affected: 2

  dependencies_by_category:
    same_module:
      count: 8
      files:
        - file: "src/Modules/Identity/Application/Commands/VerifyWalletCommandHandler.cs"
          line: 45
          usage: "Creates new WalletOwnership instance"
          impact: MEDIUM
          breaking: false
          note: "Constructor call - add new optional parameter"

    cross_module:
      count: 2
      files:
        - file: "src/Modules/Chat/Application/Services/UserContextService.cs"
          line: 67
          usage: "Reads WalletOwnership.WalletAddress"
          impact: LOW
          breaking: false

    api_layer:
      count: 3
    infrastructure:
      count: 4
    tests:
      count: 7

  breaking_changes:
    - type: DATABASE_SCHEMA
      severity: HIGH
      description: "New column AutoRevokeAt requires migration"
      mitigation: "Create EF Core migration, nullable column for backward compatibility"

  impact_summary:
    low_impact: 10 files
    medium_impact: 8 files
    high_impact: 4 files
    breaking: 2 files

  recommendations:
    - "Add AutoRevokeAt as nullable property for backward compatibility"
    - "Create database migration before deploying"
    - "Run full regression test suite (identity + chat modules)"
  </output>

  <halt-conditions>
    <i>Target code not found - ask user to verify class/method name</i>
    <i>Grep failures - report tool error</i>
    <i>Large blast radius (20+ files) - warn user about high risk, recommend phased approach</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/guides/codebase/dependency-management.md - Dependency patterns</i>
    <i>Example: Use WalletOwnership or Message for realistic dependency analysis</i>
  </references>
</task>