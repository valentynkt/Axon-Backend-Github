# Run Build Task

**Agent**: Axon Quality Guardian
**Purpose**: Execute dotnet build with warnings-as-errors and validate success

---

```xml
<task id="bmad/axon/tasks/run-build.md" name="Run Build">
  <llm critical="true">
    <i>MANDATORY: Build MUST succeed with zero warnings (TreatWarningsAsErrors=true)</i>
    <i>If build fails, do NOT proceed to next steps</i>
  </llm>

  <flow>
    <step n="1" title="Clean Build Environment">
      <action>Run: dotnet clean</action>
      <action>Remove bin/ and obj/ directories if needed</action>
    </step>

    <step n="2" title="Restore Dependencies">
      <action>Run: dotnet restore</action>
      <action>Verify NuGet packages restored successfully</action>
    </step>

    <step n="3" title="Execute Build">
      <action>Run: dotnet build --no-restore --configuration Release</action>
      <action>Capture build output (stdout + stderr)</action>
      <action>Parse for errors and warnings</action>
    </step>

    <step n="4" title="Validate Build Success">
      <action>Check exit code = 0 (success)</action>
      <action>Check warnings count = 0 (TreatWarningsAsErrors)</action>
      <action>Check errors count = 0</action>
    </step>

    <step n="5" title="Output Build Result">
      <output format="yaml">
build_result:
  status: SUCCESS / FAILURE
  exit_code: {code}
  errors: {count}
  warnings: {count}
  duration_seconds: {duration}

  failure_details:  # If status = FAILURE
    - file: {file-path}
      line: {line-number}
      code: {error-code}
      message: {error-message}
      </output>
    </step>
  </flow>

  <validation>
    <i>Exit code must be 0</i>
    <i>Warnings must be 0 (TreatWarningsAsErrors=true)</i>
    <i>Errors must be 0</i>
    <i>All projects in solution must build</i>
  </validation>

  <halt-conditions>
    <i>HALT if build fails (exit code != 0)</i>
    <i>HALT if warnings exist (count > 0)</i>
    <i>Report errors clearly with file:line references</i>
  </halt-conditions>

  <references>
    <i>Build config: Directory.Build.props</i>
    <i>Solution file: Axon.sln</i>
  </references>
</task>
```