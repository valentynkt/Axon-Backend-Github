# Validate Code Task

<task id="implementation-surgeon/validate-code" name="Pre-Generation Pattern Validation">
  <llm critical="true">
    <i>Validate code design BEFORE generation - catch violations early</i>
    <i>Validate: Result&lt;T> usage, StrongId&lt;T>, CQRS structure, naming conventions</i>
  </llm>

  <flow>
    <step n="1" title="Validate Domain Layer">
      <action>Methods return Result&lt;T> (never void, never throw)</action>
      <action>Entity IDs use StrongId&lt;T></action>
      <action>Properties: private set, public getters</action>
      <action>Naming: PascalCase for methods/properties</action>
    </step>

    <step n="2" title="Validate Application Layer">
      <action>Commands/Queries: IRequest&lt;Result&lt;T>></action>
      <action>Handlers: IRequestHandler&lt;TCommand, Result&lt;T>></action>
      <action>Async suffix: HandleAsync, ExecuteAsync</action>
    </step>

    <step n="3" title="Validate Infrastructure Layer">
      <action>Interfaces: I prefix (IRepository, IService)</action>
      <action>EF Configurations: Owned entities use OwnsOne/OwnsMany</action>
      <action>Async throughout: Task&lt;>, async/await</action>
    </step>

    <step n="4" title="Validate API Layer">
      <action>FastEndpoints: Endpoint&lt;TRequest, TResponse></action>
      <action>Validators: AbstractValidator&lt;TRequest></action>
      <action>Route naming: kebab-case (/api/identity/wallets)</action>
    </step>
  </flow>

  <output format="yaml">
validation_result:
  status: PASS | FAIL
  violations:
    - layer: "Domain"
      file: "WalletOwnership.cs"
      line: 45
      issue: "Method throws exception instead of returning Result&lt;T>"
      severity: HIGH
      fix: "Change to return Result&lt;T>.Failure(...)"
  pattern_compliance: 95%
  </output>

  <halt-conditions>
    <i>FAIL status - fix violations before generating code</i>
    <i>Compliance &lt;90% - significant violations, review design</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md</i>
    <i>Docs/ENGINEERING/guides/codebase/coding-standards.md</i>
  </references>
</task>
