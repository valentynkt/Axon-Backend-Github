# Check Compatibility Task

<task id="library-sage/check-compatibility" name="Check Library Version Compatibility">
  <llm critical="true">
    <i>Check if library version is compatible with existing dependencies</i>
    <i>CRITICAL: Run `dotnet list package` to get ACTUAL installed versions</i>
    <i>Check: .NET version, conflicting dependencies, breaking changes</i>
  </llm>

  <flow>
    <step n="1" title="Parse Input & Get Current Versions">
      <action>Extract library name + desired version</action>
      <action>Run: `dotnet list package` to get all installed packages</action>
      <action>Extract current version (if installed)</action>
      <action>Extract .NET target framework (net10.0)</action>
    </step>

    <step n="2" title="Check .NET Compatibility">
      <action>Does library support .NET 10? (check NuGet page or docs)</action>
      <action>Minimum .NET version required?</action>
      <action>HALT if library doesn't support .NET 10</action>
    </step>

    <step n="3" title="Check Dependency Conflicts">
      <action>List library's dependencies (NuGet transitive dependencies)</action>
      <action>Compare with installed packages</action>
      <action>Detect conflicts: Same package, different versions</action>
      <action>Example: Library needs Newtonsoft.Json 13.x, but we have 12.x</action>
    </step>

    <step n="4" title="Check Breaking Changes">
      <action>If upgrading existing library, check changelog/release notes</action>
      <action>List breaking changes between current version → desired version</action>
      <action>Estimate migration effort: LOW / MEDIUM / HIGH</action>
    </step>

    <step n="5" title="Generate Compatibility Report">
      <action>Status: COMPATIBLE / WARNING / INCOMPATIBLE</action>
      <action>Issues: List all conflicts/warnings</action>
      <action>Recommendations: How to resolve conflicts</action>
    </step>
  </flow>

  <validation>
    <i>Actual package versions checked (not guessed)</i>
    <i>All conflicts listed with resolution steps</i>
    <i>Breaking changes documented if upgrading</i>
  </validation>

  <output format="yaml">
compatibility_check:
  library: "Refit"
  desired_version: "7.0.0"
  current_version: "6.3.2"
  target_framework: "net10.0"

  net_compatibility:
    supported: true
    minimum_net_version: "net6.0"
    status: PASS

  dependency_conflicts:
    - package: "System.Text.Json"
      required_version: "8.0.0"
      installed_version: "8.0.5"
      conflict: false
      resolution: "N/A"

    - package: "Newtonsoft.Json"
      required_version: "13.0.3"
      installed_version: "13.0.1"
      conflict: true
      severity: LOW
      resolution: "Upgrade Newtonsoft.Json to 13.0.3"

  breaking_changes:
    - version: "7.0.0"
      change: "AuthorizationHeaderValueGetter signature changed"
      impact: "MEDIUM"
      files_affected: 2
      migration_effort: "30 minutes"

  overall_status: WARNING
  recommendation: "Compatible but upgrade Newtonsoft.Json first + fix breaking change in 2 files"
  installation_command: "dotnet add package Refit --version 7.0.0"
  </output>

  <halt-conditions>
    <i>INCOMPATIBLE status - halt, library cannot be used</i>
    <i>HIGH conflict severity - warn user, suggest alternatives</i>
    <i>Cannot run dotnet list package - ask user to provide package list</i>
  </halt-conditions>

  <references>
    <i>Run: `dotnet list package` for current versions</i>
    <i>Check: NuGet.org for library dependencies and .NET support</i>
    <i>Check: GitHub releases for breaking changes</i>
  </references>
</task>