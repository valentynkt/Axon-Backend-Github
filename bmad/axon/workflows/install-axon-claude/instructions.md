# Install Axon Claude Integration - Installation Instructions

<workflow>

<critical>The workflow execution engine is governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>You MUST have already loaded and processed: {project-root}/bmad/axon/workflows/install-axon-claude/workflow.yaml</critical>
<critical>This workflow installs Axon agents and workflows into Claude Code for direct @mention and slash command access</critical>

<step n="1" goal="Validate prerequisites and environment">
<action>Verify that source directories exist:
  - {source_agents} must exist with 6 agent files
  - {source_workflows} must exist with 9 workflow directories
</action>

<check>Count agent files in {source_agents}:</check>
<action>Use Glob tool to find all *.md files (excluding README.md)</action>
<check>Verify count = 6 agents</check>

<check>Count workflow directories in {source_workflows}:</check>
<action>Use Glob tool to find all */workflow.yaml files</action>
<check>Verify count = 9 workflows</check>

<action>Get project root absolute path: {project-root}</action>
<action>Store as {{PROJECT_ROOT_ABS}} for path replacements</action>

<check>Show user summary of what will be installed</check>
<ask>Ready to proceed with installation? [y/n]</ask>
</step>

<step n="2" goal="Create target directory structure">
<action>Create three installation locations:

**Location 1: .claude/agents/ (Direct Agent Access)**
- Create directory: {target_claude_agents}
- Purpose: Enables @agent-name mentions in Claude Code

**Location 2: .claude/commands/bmad/axon/agents/ (Command Wrappers)**
- Create directory: {target_command_agents}
- Purpose: Slash command access to agents

**Location 3: .claude/commands/bmad/axon/workflows/ (Workflow Wrappers)**
- Create directory: {target_command_workflows}
- Purpose: Slash command access to workflows
</action>

<check>Verify all directories created successfully</check>
<action>Show created paths to user with their purposes</action>
</step>

<step n="3" goal="Install agents to .claude/agents/ (Direct Access)">
<action>For each agent in the agents list from workflow.yaml:</action>

<loop for-each="agent_file in agents">
  <substep n="3a">
    <action>Read source file: {source_agents}/{{agent_file}}</action>
    <action>Get complete file content</action>
  </substep>

  <substep n="3b">
    <action>Replace all occurrences of {project-root} with {{PROJECT_ROOT_ABS}}</action>
    <action>Replace all occurrences of {config_source} references with absolute paths</action>
    <critical>Must use exact path replacement to ensure agents work</critical>
  </substep>

  <substep n="3c">
    <action>Extract agent name from filename (e.g., axon-story-orchestrator.md → axon-story-orchestrator)</action>
    <action>Write modified content to: {target_claude_agents}/{{agent_file}}</action>
    <check>Verify file written successfully to .claude/agents/</check>
  </substep>

  <substep n="3d">
    <action>Log: "✅ Installed agent to .claude/agents/: {{agent_file}}"</action>
  </substep>
</loop>

<action>Show summary: "Installed X of 6 agents to .claude/agents/ (available via @agent-name)"</action>
</step>

<step n="4" goal="Install agent command wrappers (Slash Commands)">
<action>For each agent in the agents list from workflow.yaml:</action>

<loop for-each="agent_file in agents">
  <substep n="4a">
    <action>Extract agent name: {{agent_name}} = filename without .md</action>
    <action>Read source agent to get description from metadata</action>
  </substep>

  <substep n="4b">
    <action>Create command wrapper in {target_command_agents}/{{agent_file}}</action>
    <action>Write the SAME content as .claude/agents/ version (already has absolute paths)</action>
  </substep>

  <substep n="4c">
    <check>Verify file written successfully</check>
    <action>Log: "✅ Created command wrapper: /bmad:axon:agents:{{agent_name}}"</action>
  </substep>
</loop>

<action>Show summary: "Created X command wrappers in .claude/commands/bmad/axon/agents/"</action>
</step>

<step n="5" goal="Create workflow command wrappers">
<action>For each workflow in the workflows list from workflow.yaml:</action>

<loop for-each="workflow_name in workflows">
  <substep n="5a">
    <action>Create wrapper file: {target_command_workflows}/{{workflow_name}}.md</action>
    <action>Generate wrapper content with:
      - Workflow title and description
      - Command to execute the workflow
      - Path to workflow.yaml with absolute path
      - Usage instructions
    </action>
  </substep>

  <substep n="5b">
    <action>Use this template structure:
```markdown
<!-- Powered by BMAD-CORE™ -->

# {{Workflow Title}}

Execute the {{workflow_name}} workflow.

## Usage

This command loads and executes the workflow at:
`{{PROJECT_ROOT_ABS}}/bmad/axon/workflows/{{workflow_name}}/workflow.yaml`

## Execution

Load the workflow task executor from:
`{{PROJECT_ROOT_ABS}}/bmad/core/tasks/workflow.md`

Then execute with workflow configuration:
`{{PROJECT_ROOT_ABS}}/bmad/axon/workflows/{{workflow_name}}/workflow.yaml`
```
    </action>
  </substep>

  <substep n="5c">
    <action>Write wrapper file to {target_command_workflows}/{{workflow_name}}.md</action>
    <check>Verify file written successfully</check>
    <action>Log: "✅ Installed workflow: /bmad:axon:workflows:{{workflow_name}}"</action>
  </substep>
</loop>

<action>Show summary: "Installed X of 9 workflows to .claude/commands/bmad/axon/workflows/"</action>
</step>

<step n="6" goal="Create README documentation">
<action>Create file: {target_base}/README.md</action>

<action>Write README content explaining:
  1. Purpose: Axon agents and workflows for Claude Code
  2. Installation locations:
     - `.claude/agents/` - Direct agent access via @agent-name
     - `.claude/commands/bmad/axon/agents/` - Slash command access
     - `.claude/commands/bmad/axon/workflows/` - Workflow commands
  3. Available agents (list all 6 with descriptions and both access methods)
  4. Available workflows (list all 9 with descriptions)
  5. Usage examples:
     - Direct agent: @axon-story-orchestrator
     - Slash command: /bmad:axon:agents:axon-story-orchestrator
     - Workflow: /bmad:axon:workflows:story-implementation
  6. Troubleshooting tips
</action>

<check>Verify README created successfully</check>
</step>

<step n="7" goal="Validate installation">
<action>Run validation checks:
  1. Count files in {target_claude_agents} = 6 agents
  2. Count files in {target_command_agents} = 6 command wrappers
  3. Count files in {target_command_workflows} = 9 workflow wrappers
  4. Verify README exists at {target_base}/README.md
  5. Sample check: Read one agent file from .claude/agents/ and verify paths are absolute
</action>

<check>All validation checks passed?</check>

<action>Show final installation report:

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ AXON MODULE INSTALLATION COMPLETE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

**Installation Summary:**
- Agents installed: 6/6
- Workflows installed: 9/9
- Total files created: 21

**Installation Locations:**

1. **Direct Agent Access** (.claude/agents/)
   - 6 agents available via @agent-name mentions
   - Example: @axon-story-orchestrator

2. **Slash Commands - Agents** (.claude/commands/bmad/axon/agents/)
   - 6 agent command wrappers
   - Example: /bmad:axon:agents:axon-story-orchestrator

3. **Slash Commands - Workflows** (.claude/commands/bmad/axon/workflows/)
   - 9 workflow command wrappers
   - Example: /bmad:axon:workflows:story-implementation

**Quick Start:**
- Direct mention: @axon-story-orchestrator implement-story story-001
- Slash command: /bmad:axon:agents:axon-story-orchestrator
- Run workflow: /bmad:axon:workflows:story-implementation

**Documentation:** {target_base}/README.md

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
</action>
</step>

<step n="8" goal="Test command availability" optional="true">
<ask>Would you like to test that commands are accessible? [y/n]</ask>

<action if="user_response == 'y'">
  Show user: "Try typing `/` in Claude Code and look for bmad:axon commands"
  Provide example: "/bmad:axon:agents:axon-story-orchestrator"
</action>
</step>

</workflow>