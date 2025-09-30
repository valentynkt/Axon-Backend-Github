# Library Capabilities Task

<task id="library-sage/library-capabilities" name="Comprehensive Library Capability Catalog">
  <llm critical="true">
    <i>Generate comprehensive capability catalog for a library</i>
    <i>CRITICAL: Read from Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md, not invented</i>
    <i>Categories: Core features, Advanced features, Integration points, Limitations</i>
  </llm>

  <flow>
    <step n="1" title="Parse Input & Read Library Docs">
      <action>Extract library name</action>
      <action>Read: Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md</action>
      <action>Extract sections: Features, Capabilities, Examples, Limitations</action>
    </step>

    <step n="2" title="Catalog Core Features">
      <action>List primary capabilities (what library is designed for)</action>
      <action>Example: MediatR → Command/Query dispatch, Pipeline behaviors</action>
      <action>Example: FastEndpoints → REST endpoints, Request validation, Response mapping</action>
    </step>

    <step n="3" title="Catalog Advanced Features">
      <action>List advanced capabilities (beyond basic usage)</action>
      <action>Example: MediatR → Notification patterns, Request pre/post processors</action>
      <action>Example: EF Core → Change tracking, Migrations, Interceptors</action>
    </step>

    <step n="4" title="Catalog Integration Points">
      <action>List how library integrates with other libraries/patterns</action>
      <action>Example: MediatR + FluentValidation → Validation pipeline</action>
      <action>Example: FastEndpoints + MediatR → Endpoint dispatches to commands</action>
    </step>

    <step n="5" title="Document Limitations">
      <action>List what library does NOT provide (important negatives)</action>
      <action>Example: MediatR → No built-in validation (use FluentValidation)</action>
      <action>Example: Dynamic Auth → No database persistence (manual implementation needed)</action>
    </step>
  </flow>

  <validation>
    <i>At least 5 core features listed</i>
    <i>Limitations documented (no library is perfect)</i>
    <i>All capabilities verified from implementation guide</i>
  </validation>

  <output format="yaml">
library_capabilities:
  library: "MediatR"
  version: "12.x"
  category: "CQRS/Messaging"

  core_features:
    - "Command/Query dispatch (IRequest/IRequestHandler)"
    - "Result&lt;T> pattern support"
    - "Async/await throughout"
    - "Dependency injection integration"
    - "Pipeline behaviors for cross-cutting concerns"

  advanced_features:
    - "Notification patterns (INotification)"
    - "Request pre/post processors"
    - "Stream requests (IStreamRequest)"
    - "Unit of work pattern support"

  integration_points:
    - library: "FluentValidation"
      integration: "Validation pipeline behavior"
    - library: "FastEndpoints"
      integration: "Endpoints dispatch to MediatR commands"

  limitations:
    - "No built-in validation (requires FluentValidation)"
    - "No built-in error handling (use Result&lt;T> pattern)"
    - "No automatic transaction management (manual UoW needed)"

  use_cases:
    - "CQRS command/query separation"
    - "Decoupled domain logic from API layer"
    - "Cross-cutting concerns (logging, validation, transactions)"
  </output>

  <halt-conditions>
    <i>Implementation guide not found - cannot catalog capabilities</i>
    <i>Library not documented - suggest creating implementation guide first</i>
  </halt-conditions>

  <references>
    <i>Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md</i>
    <i>Docs/Libraries/00-INDEX.md - All 15 libraries cataloged</i>
  </references>
</task>