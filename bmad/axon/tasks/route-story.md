# Route Story Task

**Agent**: Axon Story Orchestrator
**Purpose**: Analyze story and route to appropriate workflow based on type and module context

---

```xml
<task id="bmad/axon/tasks/route-story.md" name="Route Story">
  <llm critical="true">
    <i>MANDATORY: Analyze story deeply before routing</i>
    <i>Route based on: Story Type (Feature/Refactor/Bugfix) + Module Context (Identity/Chat/API/Cross-cutting)</i>
    <i>Generate routing decision log for traceability</i>
  </llm>

  <flow>
    <step n="1" title="Load and Parse Story">
      <action>Read story file: {story-file-path}</action>
      <action>Extract metadata: story_id, title, module, story_type, priority</action>
      <action>Extract acceptance criteria count</action>
      <action>Check for tech spec reference (BMM handoff)</action>
      <action>Parse user story section ("As a... I want... So that...")</action>
    </step>

    <step n="2" title="Detect Story Type">
      <action>Keywords for Feature: "add", "new", "implement", "create", "enable", "As a... I want..."</action>
      <action>Keywords for Refactor: "improve", "optimize", "refactor", "clean", "simplify", "performance"</action>
      <action>Keywords for Bugfix: "fix", "bug", "issue", "error", "broken", "incorrect", "fails"</action>
      <action>Default to Feature if ambiguous</action>
    </step>

    <step n="3" title="Detect Module Context">
      <action>Keywords for Identity: "auth", "wallet", "credential", "principal", "verification", "login", "JWT", "token"</action>
      <action>Keywords for Chat: "conversation", "message", "AI", "Claude", "turn", "chat", "streaming", "MCP"</action>
      <action>Keywords for API: "endpoint", "FastEndpoint", "REST", "HTTP", "contract", "request", "response"</action>
      <action>Check file paths mentioned: src/Modules/Identity/ → Identity, src/Modules/Chat/ → Chat</action>
      <action>Default to Cross-cutting if multiple modules or BuildingBlocks</action>
    </step>

    <step n="4" title="Apply Routing Matrix">
      <action>Feature + Identity → bmad/axon/workflows/identity-workflow/</action>
      <action>Feature + Chat → bmad/axon/workflows/chat-workflow/</action>
      <action>Feature + API → bmad/axon/workflows/api-workflow/</action>
      <action>Feature + Cross-cutting → bmad/axon/workflows/story-implementation/</action>
      <action>Refactor + Any → bmad/axon/workflows/story-refactoring/</action>
      <action>Bugfix + Any → bmad/axon/workflows/story-bugfix/</action>
    </step>

    <step n="5" title="Generate Routing Decision">
      <action>Append routing decision to: {story_workspace}/implementation.log</action>
      <format>
## Story Routing (Phase 0)
**Timestamp**: {iso-timestamp}
**Story ID**: {story-id}
**Story Title**: {title}

### Detection Results
- **Story Type**: {detected-type} (confidence: {confidence})
  - Keywords found: {keywords-found}
- **Module Context**: {detected-module} (confidence: {confidence})
  - Keywords found: {keywords-found}

### Routing Decision
- **Selected Workflow**: {workflow-path}
- **Rationale**: {why-this-workflow}

### Next Steps
1. Load documentation context (Doc Oracle)
2. Execute workflow: {workflow-name}

---
      </format>
    </step>
  </flow>

  <routing-matrix critical="true">
    <i>Feature + Identity → identity-workflow (Tier 3)</i>
    <i>Feature + Chat → chat-workflow (Tier 3)</i>
    <i>Feature + API → api-workflow (Tier 3)</i>
    <i>Feature + Cross-cutting → story-implementation (Tier 2)</i>
    <i>Refactor + Any → story-refactoring (Tier 2, extra safety)</i>
    <i>Bugfix + Any → story-bugfix (Tier 2, diagnostic focus)</i>
  </routing-matrix>

  <validation>
    <i>Story type must be detected (Feature/Refactor/Bugfix)</i>
    <i>Module context must be detected (even if Cross-cutting)</i>
    <i>Routing decision must include rationale</i>
    <i>Confidence level must be honest (HIGH/MEDIUM/LOW)</i>
  </validation>

  <halt-conditions>
    <i>HALT if story file not found or unreadable</i>
    <i>HALT if story type ambiguous AND confidence = LOW (ask user)</i>
  </halt-conditions>

  <references>
    <i>Workflows: bmad/axon/workflows/</i>
    <i>Implementation log: {story_workspace}/implementation.log</i>
  </references>
</task>
```