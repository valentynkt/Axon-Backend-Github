# Diff Preview Task

<task id="implementation-surgeon/diff-preview" name="Generate Unified Diff Preview">
  <llm critical="true">
    <i>Generate unified diff preview BEFORE applying changes</i>
    <i>Show: - (removed lines) + (added lines) with 3 lines context</i>
  </llm>

  <flow>
    <step n="1" title="Read Current Code">
      <action>Read all files that will be modified</action>
      <action>Store current state as baseline</action>
    </step>

    <step n="2" title="Generate Proposed Changes">
      <action>Generate new code (based on story requirements)</action>
      <action>Ensure pattern compliance</action>
    </step>

    <step n="3" title="Generate Unified Diff">
      <action>For each file: Compare current vs proposed</action>
      <action>Format: --- a/file.cs +++ b/file.cs @@ line,count @@</action>
      <action>Show - removed lines, + added lines, 3 context lines</action>
    </step>
  </flow>

  <output format="diff">
--- a/src/Modules/Identity/Domain/Entities/WalletOwnership.cs
+++ b/src/Modules/Identity/Domain/Entities/WalletOwnership.cs
@@ -12,6 +12,8 @@ public class WalletOwnership : Entity&lt;WalletOwnershipId>
     public DateTimeOffset? RevokedAt { get; private set; }
     public RevokedBy? RevokedBy { get; private set; }

+    public DateTimeOffset? AutoRevokeAt { get; private set; }
+
     public bool IsRevoked => RevokedAt.HasValue;

     public Result&lt;Unit> Revoke(RevokedBy revokedBy)
  </output>

  <halt-conditions>
    <i>Diff too large (>500 lines changed) - warn user, break into smaller changes</i>
  </halt-conditions>

  <references>
    <i>Use standard unified diff format (git diff compatible)</i>
  </references>
</task>
