# Implement New Code Task

**Agent**: Axon Implementation Surgeon
**Purpose**: Generate pattern-compliant code surgically (post-validation, minimal changes)

---

```xml
<task id="bmad/axon/tasks/implement-new.md" name="Implement New Code">
  <llm critical="true">
    <i>MANDATORY: Execute ALL steps in flow section IN EXACT ORDER</i>
    <i>DO NOT skip steps or change sequence</i>
    <i>Only execute AFTER pre-flight validation complete</i>
    <i>Implement bottom-up: Domain → Application → Infrastructure → API</i>
  </llm>

  <flow>
    <step n="1" title="Load Pre-Flight Results">
      <action>Read discovery report from Archaeologist (existing code found)</action>
      <action>Read library recommendations from Library Sage (library vs manual)</action>
      <action>Read pattern validation from Doc Oracle (compliance requirements)</action>
      <action>Determine: REUSE DIRECTLY / EXTEND EXISTING / CREATE NEW</action>
    </step>

    <step n="2" title="Generate Domain Layer (Bottom-Up)">
      <action>If entity/aggregate needed: Create in src/Modules/{module}/Domain/Entities/</action>
      <action>Pattern: Use StrongId&lt;T&gt; for entity IDs</action>
      <action>Pattern: Return Result&lt;T, Error&gt; from all methods</action>
      <action>Pattern: Raise domain events for state changes</action>
      <action>If value object needed: Create in src/Modules/{module}/Domain/ValueObjects/</action>
      <action>Pattern: Immutable records with validation in Create() factory</action>
    </step>

    <step n="3" title="Generate Application Layer">
      <action>Create command: src/Modules/{module}/Application/Commands/{Command}.cs</action>
      <action>Pattern: Record implementing IRequest&lt;Result&lt;T&gt;&gt;</action>
      <action>Create handler: {Command}Handler.cs implementing IRequestHandler</action>
      <action>Pattern: Inject dependencies via constructor</action>
      <action>Pattern: Use domain entities, return Result&lt;T&gt;</action>
      <action>If query needed: Create in Queries/ folder (same pattern)</action>
    </step>

    <step n="4" title="Generate Infrastructure Layer">
      <action>If repository needed: Create in src/Modules/{module}/Infrastructure/Repositories/</action>
      <action>Pattern: Implement domain interface, use EF Core</action>
      <action>If external service needed: Create in Infrastructure/Services/</action>
      <action>Pattern: Return Result&lt;T&gt;, handle external errors gracefully</action>
      <action>If database migration needed: Run dotnet ef migrations add {Name}</action>
    </step>

    <step n="5" title="Generate API Layer">
      <action>Create endpoint: src/Api/Endpoints/{module}/{Feature}Endpoint.cs</action>
      <action>Pattern: Extend FastEndpoints Endpoint&lt;TRequest, TResponse&gt;</action>
      <action>Pattern: Inject ISender, call await Send(command)</action>
      <action>Pattern: Map Result&lt;T&gt; to HTTP codes (200, 400, 404, etc.)</action>
      <action>Create request/response models in Contracts/</action>
      <action>Pattern: Immutable records with validation attributes</action>
    </step>

    <step n="6" title="Add Inline Documentation">
      <action>Add XML comments to all public APIs</action>
      <action>Document parameters, returns, exceptions (if any)</action>
      <action>Add code examples for complex APIs</action>
    </step>

    <step n="7" title="Validate Generated Code">
      <action>Verify Result&lt;T&gt; used everywhere (no exceptions in domain)</action>
      <action>Verify StrongId&lt;T&gt; used for all entity IDs</action>
      <action>Verify CQRS: Commands/queries separate, use MediatR</action>
      <action>Verify Clean Architecture: Dependencies point inward</action>
      <action>Run dotnet build --no-restore (must succeed)</action>
    </step>

    <step n="8" title="Output Implementation Result">
      <action>Append implementation summary to: {implementation_log}</action>
      <output format="yaml">
implementation_result:
  story_id: {story-id}
  approach: REUSE_DIRECTLY / EXTEND_EXISTING / CREATE_NEW

  files_created:
    domain:
      - {file-path}
    application:
      - {file-path}
    infrastructure:
      - {file-path}
    api:
      - {file-path}

  patterns_applied:
    - Result&lt;T&gt; error handling: {count} usages
    - StrongId&lt;T&gt; type-safe IDs: {count} usages
    - CQRS commands/queries: {count} handlers
    - Domain events: {count} events

  build_status: SUCCESS / FAILURE
  warnings: {count}
  lines_of_code: {total}
      </output>
    </step>
  </flow>

  <validation critical="true">
    <i>Zero exceptions in domain layer (Result&lt;T&gt; only)</i>
    <i>All entity IDs are StrongId&lt;T&gt; (no Guid, int)</i>
    <i>All operations use MediatR (CQRS compliance)</i>
    <i>Dependencies point inward (Clean Architecture)</i>
    <i>Build succeeds with zero warnings (TreatWarningsAsErrors)</i>
  </validation>

  <patterns critical="true">
    <i>Result&lt;T&gt;: All domain methods return Result&lt;T, Error&gt;</i>
    <i>StrongId&lt;T&gt;: public record UserId : StrongId&lt;User&gt;</i>
    <i>CQRS: Commands (IRequest&lt;Result&gt;), Queries (IRequest&lt;Result&lt;T&gt;&gt;)</i>
    <i>Domain Events: Record implementing IDomainEvent, raised in aggregates</i>
    <i>Owned Entities: Configure as OwnsMany in EF Core configuration</i>
  </patterns>

  <halt-conditions>
    <i>HALT if pre-flight validation not complete (Archaeologist, Library Sage, Doc Oracle)</i>
    <i>HALT if library exists but not used (check Library Sage recommendation)</i>
    <i>HALT if patterns violated (Result&lt;T&gt;, StrongId&lt;T&gt;, CQRS)</i>
    <i>HALT if build fails (dotnet build must succeed)</i>
  </halt-conditions>

  <references>
    <i>Patterns: Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md</i>
    <i>Examples: src/Modules/Identity/, src/Modules/Chat/</i>
    <i>Result&lt;T&gt;: src/BuildingBlocks/Core/Result/Result.cs</i>
    <i>StrongId&lt;T&gt;: src/BuildingBlocks/Core/StrongId/StrongId.cs</i>
  </references>
</task>
```