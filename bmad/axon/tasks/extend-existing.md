# Extend Existing Task

<task id="implementation-surgeon/extend-existing" name="Surgical Extension of Existing Code">
  <llm critical="true">
    <i>Extend existing code with MINIMAL, surgical changes - brownfield-safe</i>
    <i>CRITICAL: Read existing code first, preserve patterns, add only what's needed</i>
    <i>Bottom-up order: Domain → Application → Infrastructure → API</i>
  </llm>

  <flow>
    <step n="1" title="Read Existing Code">
      <action>Read target file(s) to extend</action>
      <action>Understand current patterns: Result&lt;T>, StrongId&lt;T>, naming conventions</action>
      <action>Identify extension point: New property, new method, new class</action>
    </step>

    <step n="2" title="Design Minimal Extension">
      <action>Add property: Make nullable for backward compatibility (DateTimeOffset?)</action>
      <action>Add method: Follow existing naming (PascalCase, Result&lt;T> return)</action>
      <action>Add class: Place in same namespace, follow existing structure</action>
    </step>

    <step n="3" title="Generate Extension Code">
      <action>Domain: Extend entity/aggregate (new property, new behavior method)</action>
      <action>Application: Extend command/query (optional new field)</action>
      <action>Infrastructure: Extend configuration (new property mapping)</action>
      <action>API: Extend DTO (optional new field in response)</action>
    </step>
  </flow>

  <output format="csharp">
// Domain extension (Entity)
public DateTimeOffset? AutoRevokeAt { get; private set; }

public Result&lt;Unit> CheckAndRevokeIfExpired()
{
    if (AutoRevokeAt.HasValue && AutoRevokeAt.Value < DateTimeOffset.UtcNow)
        return Revoke(RevokedBy.System);
    return Result&lt;Unit>.Success(Unit.Value);
}
  </output>

  <halt-conditions>
    <i>Existing code not found - cannot extend non-existent code</i>
    <i>Pattern mismatch - existing code doesn't follow patterns, flag for refactoring first</i>
  </halt-conditions>

  <references>
    <i>Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md</i>
    <i>Example: WalletOwnership extensions for realistic patterns</i>
  </references>
</task>
