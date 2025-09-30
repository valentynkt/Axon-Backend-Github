# Inline Docs Task

<task id="implementation-surgeon/inline-docs" name="Generate XML Documentation">
  <llm critical="true">
    <i>Generate C# XML documentation comments for public APIs</i>
    <i>Required for: Public classes, public methods, properties</i>
  </llm>

  <flow>
    <step n="1" title="Identify Public APIs">
      <action>Find: public classes, public methods, public properties</action>
      <action>Skip: private/internal members (no docs needed)</action>
    </step>

    <step n="2" title="Generate XML Comments">
      <action>/// &lt;summary> - What does it do?</action>
      <action>/// &lt;param name="x"> - Parameter descriptions</action>
      <action>/// &lt;returns> - What does it return?</action>
      <action>/// &lt;exception cref=""> - Only if exceptions thrown (discouraged)</action>
    </step>
  </flow>

  <output format="csharp">
/// &lt;summary>
/// Revokes wallet ownership, marking it as no longer valid.
/// &lt;/summary>
/// &lt;param name="revokedBy">Who initiated the revocation (User or System).&lt;/param>
/// &lt;returns>Success if revoked, Failure if already revoked.&lt;/returns>
public Result&lt;Unit> Revoke(RevokedBy revokedBy)
{
    if (IsRevoked) return Result&lt;Unit>.Failure(WalletOwnershipErrors.AlreadyRevoked);
    RevokedAt = DateTimeOffset.UtcNow;
    RevokedBy = revokedBy;
    return Result&lt;Unit>.Success(Unit.Value);
}
  </output>

  <halt-conditions>
    <i>No public APIs - skip documentation</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/guides/codebase/coding-standards.md - XML doc standards</i>
  </references>
</task>
