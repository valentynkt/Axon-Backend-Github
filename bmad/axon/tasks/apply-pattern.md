# Apply Pattern Task

<task id="implementation-surgeon/apply-pattern" name="Apply Architectural Pattern">
  <llm critical="true">
    <i>Apply mandatory architectural pattern to code: Result&lt;T>, StrongId&lt;T>, CQRS, Events, Owned Entities</i>
    <i>ZERO tolerance for pattern violations - HALT if pattern not followed</i>
  </llm>

  <flow>
    <step n="1" title="Validate Pattern Applicability">
      <action>Extract pattern type (Result, StrongId, CQRS, DomainEvent, OwnedEntity)</action>
      <action>Validate pattern fits context (Domain methods → Result, Entities → StrongId, etc.)</action>
    </step>

    <step n="2" title="Apply Pattern">
      <action>Result&lt;T>: Return Result&lt;T>.Success / Result&lt;T>.Failure (never throw exceptions in Domain)</action>
      <action>StrongId&lt;T>: Use record ConversationId(Guid Value) : StrongId&lt;Guid>(Value)</action>
      <action>CQRS: IRequest&lt;Result&lt;T>> + IRequestHandler pattern</action>
      <action>Domain Events: record ConversationCreatedEvent : DomainEvent</action>
      <action>Owned Entities: OwnsMany(c => c.Messages).WithOwner(m => m.Conversation)</action>
    </step>
  </flow>

  <output format="csharp">
// Result&lt;T> pattern
public Result&lt;Unit> Revoke()
{
    if (IsRevoked) return Result&lt;Unit>.Failure(Errors.AlreadyRevoked);
    return Result&lt;Unit>.Success(Unit.Value);
}

// StrongId&lt;T> pattern
public record WalletOwnershipId(Guid Value) : StrongId&lt;Guid>(Value);
  </output>

  <halt-conditions>
    <i>Pattern violation detected - HALT, fix before proceeding</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md - All 5 patterns</i>
    <i>Docs/ENGINEERING/guides/architecture/adrs/003-result-pattern.md</i>
    <i>Docs/ENGINEERING/guides/architecture/adrs/004-strong-ids.md</i>
  </references>
</task>
