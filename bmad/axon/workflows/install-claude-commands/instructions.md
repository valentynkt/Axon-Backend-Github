# Install Claude Code Commands - Installation Instructions

<workflow>

<critical>The workflow execution engine is governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>You MUST have already loaded and processed: {project-root}/bmad/axon/workflows/install-claude-commands/workflow.yaml</critical>
<critical>This workflow installs Axon agents/workflows as Claude Code slash commands</critical>

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
<action>Create directory: {target_base}</action>
<action>Create directory: {target_agents}</action>
<action>Create directory: {target_workflows}</action>

<check>Verify all directories created successfully</check>
<action>Show created paths to user</action>
</step>

<step n="3" goal="Install agent command files">
<action>For each agent in the agents list from workflow.yaml:</action>

<loop for-each="agent_file in agents">
  <substep n="3a">
    <action>Read source file: {source_agents}/{{agent_file}}</action>
    <action>Get complete file content</action>
  </substep>

  <substep n="3b">
    <action>Replace all occurrences of {project-root} with {{PROJECT_ROOT_ABS}}/</action>
    <action>Replace all occurrences of {config_source} references with absolute paths</action>
    <critical>Must use exact path replacement to ensure commands work</critical>
  </substep>

  <substep n="3c">
    <action>Write modified content to: {target_agents}/{{agent_file}}</action>
    <check>Verify file written successfully</check>
  </substep>

  <substep n="3d">
    <action>Log: "✅ Installed agent: {{agent_file}}"</action>
  </substep>
</loop>

<action>Show summary: "Installed X of 6 agents successfully"</action>
</step>

<step n="4" goal="Create workflow command wrappers">
<action>For each workflow in the workflows list from workflow.yaml:</action>

<loop for-each="workflow_name in workflows">
  <substep n="4a">
    <action>Create wrapper file: {target_workflows}/{{workflow_name}}.md</action>
    <action>Generate wrapper content with:
      - Workflow title and description
      - Command to execute the workflow
      - Path to workflow.yaml with absolute path
      - Usage instructions
    </action>
  </substep>

  <substep n="4b">
    <action>Use this template structure:
```markdown
<!-- Powered by BMAD-CORE™ -->

# {{Workflow Title}}

Execute the {{workflow_name}} workflow.

## Usage

This command loads and executes the workflow at:
`{{PROJECT_ROOT_ABS}}/bmad/axon/workflows/{{workflow_name}}/workflow.yaml`

## Execution

Load the workflow task executor:
{project-root}/bmad/core/tasks/workflow.md

Then execute with workflow config:
{{PROJECT_ROOT_ABS}}/bmad/axon/workflows/{{workflow_name}}/workflow.yaml
```
    </action>
  </substep>

  <substep n="4c">
    <action>Write wrapper file to {target_workflows}/{{workflow_name}}.md</action>
    <check>Verify file written successfully</check>
    <action>Log: "✅ Installed workflow: {{workflow_name}}"</action>
  </substep>
</loop>

<action>Show summary: "Installed X of 9 workflows successfully"</action>
</step>

<step n="5" goal="Create README documentation">
<action>Create file: {target_base}/README.md</action>

<action>Write README content explaining:
  1. Purpose of these Claude Code commands
  2. Available agents (list all 6 with descriptions)
  3. Available workflows (list all 9 with descriptions)
  4. How to use slash commands in Claude Code
  5. Example invocations
  6. Troubleshooting tips
</action>

<check>Verify README created successfully</check>
</step>

<step n="6" goal="Validate installation">
<action>Run validation checks:
  1. Count files in {target_agents} = 6
  2. Count files in {target_workflows} = 9
  3. Verify README exists
  4. Sample check: Read one agent file and verify paths are absolute
</action>

<check>All validation checks passed?</check>

<action>Show final installation report:
  - Total agents installed: 6
  - Total workflows installed: 9
  - Installation location: {target_base}
  - Commands now available via Claude Code slash commands
</action>

<action>Provide usage instructions:
  - Type `/` in Claude Code to see available commands
  - Type `/bmad:axon:agents:` to see Axon agents
  - Type `/bmad:axon:workflows:` to see Axon workflows
</action>
</step>

<step n="7" goal="Test command availability" optional="true">
<ask>Would you like to test that commands are accessible? [y/n]</ask>

<action if="user_response == 'y'">
  Show user: "Try typing `/` in Claude Code and look for bmad:axon commands"
  Provide example: "/bmad:axon:agents:axon-story-orchestrator"
</action>
</step>

</workflow>