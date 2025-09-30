# Map APIs Task

<task id="archaeologist/map-apis" name="Map Available APIs/Methods">
  <llm critical="true">
    <i>Map ALL available APIs/methods in a domain area across 4 layers: Domain, Application, Infrastructure, HTTP</i>
    <i>CRITICAL: Extract actual signatures, not summaries. Include file:line citations for traceability.</i>
    <i>Cross-reference layers: HTTP → Application → Domain → Infrastructure to show full call chains</i>
    <i>HALT if module path doesn't exist - ask user to clarify module name</i>
  </llm>

  <flow>
    <step n="1" title="Parse Input & Validate Module">
      <action>Extract domain area (e.g., "Identity", "Chat", "WalletOwnership")</action>
      <action>Extract API scope (Domain/Application/Infrastructure/HTTP or ALL)</action>
      <action>Validate module exists: Check src/Modules/{Module}/ directory</action>
      <action>HALT if invalid module with error message</action>
    </step>

    <step n="2" title="Domain API Mapping (Aggregates/Entities)">
      <action>Search: Grep "public.*Result&lt;" in src/Modules/{Module}/Domain/</action>
      <action>Extract method signatures from aggregates/entities</action>
      <action>Record: Method name, signature, return type, file:line</action>
      <action>Group by: Aggregate/Entity class</action>
    </step>

    <step n="3" title="Application API Mapping (Commands/Queries)">
      <action>Search Commands: Grep "IRequest&lt;Result&lt;" in src/Modules/{Module}/Application/Commands/</action>
      <action>Search Queries: Grep "IRequest&lt;Result&lt;" in src/Modules/{Module}/Application/Queries/</action>
      <action>Extract: Command/Query name, return type, handler class</action>
      <action>Record file:line for each command/query</action>
    </step>

    <step n="4" title="Infrastructure API Mapping (Repositories/Services)">
      <action>Search Repositories: Grep "interface I.*Repository" in src/Modules/{Module}/Infrastructure/</action>
      <action>Search Services: Grep "interface I.*Service" in src/Modules/{Module}/Infrastructure/</action>
      <action>Extract interface methods with signatures</action>
      <action>Record file:line for each interface</action>
    </step>

    <step n="5" title="HTTP API Mapping (REST Endpoints)">
      <action>Search: Grep "Endpoint&lt;" in src/Api/Endpoints/{Module}/</action>
      <action>Extract: Route (method + path), request/response types, command/query used</action>
      <action>Record file:line for each endpoint</action>
    </step>

    <step n="6" title="Cross-Reference Layers">
      <action>Map HTTP endpoints → Application commands/queries they invoke</action>
      <action>Map Application handlers → Domain methods they call</action>
      <action>Map Infrastructure services → External libraries they wrap</action>
      <action>Generate call chain visualization</action>
    </step>
  </flow>

  <validation>
    <i>At least one layer must have results (not empty map)</i>
    <i>All file paths must be valid and exist</i>
    <i>Line numbers must be accurate (verify with file read)</i>
    <i>Cross-references must be valid (endpoint → command exists)</i>
  </validation>

  <output format="yaml">
api_map:
  domain: "WalletOwnership"
  module: "Identity"
  layers_mapped: 4

  domain_api:
    aggregate: "WalletOwnership"
    file: "src/Modules/Identity/Domain/Entities/WalletOwnership.cs"
    methods:
      - signature: "Result&lt;Unit> Revoke(RevokedBy revokedBy)"
        line: 45
        returns: "Result&lt;Unit>"

  application_api:
    commands:
      - name: "VerifyWalletCommand"
        file: "src/Modules/Identity/Application/Commands/VerifyWalletCommand.cs"
        returns: "Result&lt;WalletVerificationResult>"
        handler: "VerifyWalletCommandHandler"
    queries: []

  infrastructure_api:
    repositories:
      - interface: "IIdentityRepository"
        file: "src/Modules/Identity/Infrastructure/Persistence/IIdentityRepository.cs:15"
        methods:
          - "Task&lt;WalletOwnership?> GetByWalletAddressAsync(...)"

  http_api:
    endpoints:
      - route: "POST /api/identity/wallets/verify"
        file: "src/Api/Endpoints/Identity/WalletVerificationEndpoint.cs:12"
        command: "VerifyWalletCommand"
        request: "WalletVerificationRequest"
        response: "WalletVerificationResponse"

  call_chains:
    - http: "POST /api/identity/wallets/verify"
      application: "VerifyWalletCommand"
      domain: "WalletOwnership.Create()"
      infrastructure: "IWalletVerificationService.VerifySignatureAsync()"
  </output>

  <halt-conditions>
    <i>Module directory doesn't exist - ask user for correct module name</i>
    <i>Zero APIs found - warn user, ask if module name is correct or if code exists</i>
    <i>Grep errors - report tool failure, suggest alternative search</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/guides/architecture/system-overview.md - Module structure</i>
    <i>Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md - API patterns</i>
    <i>Example: Use WalletOwnership or Message as reference for complete API mapping</i>
  </references>
</task>