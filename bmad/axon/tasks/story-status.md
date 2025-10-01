# Story Status Task

**Agent**: Axon Story Orchestrator
**Purpose**: Display story implementation progress, decision log, and next actions

---

```xml
<task id="bmad/axon/tasks/story-status.md" name="Story Status">
  <llm critical="true">
    <i>Provide clear, actionable status summary</i>
    <i>Show progress through 4 checkpoints</i>
    <i>Highlight decisions made and next steps</i>
  </llm>

  <flow>
    <step n="1" title="Locate Story Artifacts">
      <action>Read story file: {story_workspace}/story.md</action>
      <action>Read implementation log: {story_workspace}/implementation.log</action>
      <action>Read decision log: {story_workspace}/decisions.yaml (if exists)</action>
      <action>Handle missing files gracefully (not all may exist yet)</action>
    </step>

    <step n="2" title="Parse Story Progress">
      <action>Extract story metadata (ID, title, module, type, status)</action>
      <action>Determine current phase: Understanding / Pre-Flight / Implementation / Validation / Complete</action>
      <action>Count checkpoints completed (0-4)</action>
      <action>Calculate time metrics (started, last update, duration)</action>
      <action>Identify which agents have executed</action>
    </step>

    <step n="3" title="Load Decision Summary">
      <action>Parse decision log YAML if exists</action>
      <action>Count total decisions by category (Architecture, Pattern, Library, etc.)</action>
      <action>Extract key decisions (highest confidence or impact)</action>
      <action>Note learning items captured</action>
    </step>

    <step n="4" title="Determine Next Actions">
      <action>If phase = Understanding: "Awaiting Checkpoint 1 approval"</action>
      <action>If phase = Pre-Flight: "Awaiting Checkpoint 2 approval"</action>
      <action>If phase = Implementation: "Awaiting Checkpoint 3 approval"</action>
      <action>If phase = Validation: "Awaiting Checkpoint 4 approval"</action>
      <action>If phase = Complete: "Story complete, ready for commit"</action>
    </step>

    <step n="5" title="Output Status Report">
      <output format="markdown">
# Story Status: {story-title}

**ID**: {story-id}
**Module**: {module}
**Type**: {story-type}
**Status**: {status}

## Progress
- **Current Phase**: {phase} ({checkpoint}/4 checkpoints)
- **Started**: {start-time}
- **Last Update**: {last-update}
- **Duration**: {elapsed-time}

## Workflow Execution
- **Workflow**: {workflow-name}
- **Agents Involved**: {agent-list}

## Decisions Captured ({count} total)
{decision-summary-by-category}

Key Decisions:
1. [{category}] {decision-summary}
2. [{category}] {decision-summary}

## Next Actions
{next-action-recommendation}

## Files Generated
- Domain: {count} files
- Application: {count} files
- Infrastructure: {count} files
- API: {count} files
- Tests: {count} files
      </output>
    </step>
  </flow>

  <validation>
    <i>Story file must exist (error if not found)</i>
    <i>Handle missing decision log gracefully (may not exist yet)</i>
    <i>Time calculations must be accurate</i>
    <i>Next actions must be specific and actionable</i>
  </validation>

  <references>
    <i>Story workspace: {story_workspace}/</i>
    <i>Story file: {story_workspace}/story.md</i>
    <i>Implementation log: {story_workspace}/implementation.log</i>
    <i>Decision log: {story_workspace}/decisions.yaml</i>
  </references>
</task>
```