# Install Axon Claude Integration - Validation Checklist

## 📁 Directory Structure

- [ ] `.claude/agents/` directory exists (for direct agent access)
- [ ] `.claude/commands/bmad/axon/` directory exists (for command wrappers)
- [ ] `.claude/commands/bmad/axon/agents/` directory exists
- [ ] `.claude/commands/bmad/axon/workflows/` directory exists
- [ ] All directories have proper permissions

## 🤖 Agent Installation - Direct Access (.claude/agents/)

- [ ] 6 agent files installed in `.claude/agents/`
- [ ] `axon-story-orchestrator.md` present in `.claude/agents/`
- [ ] `axon-doc-oracle.md` present in `.claude/agents/`
- [ ] `axon-archaeologist.md` present in `.claude/agents/`
- [ ] `axon-library-sage.md` present in `.claude/agents/`
- [ ] `axon-implementation-surgeon.md` present in `.claude/agents/`
- [ ] `axon-quality-guardian.md` present in `.claude/agents/`
- [ ] All agent files have `{project-root}` replaced with absolute paths
- [ ] All agent files have valid XML structure with `<agent>` tags
- [ ] Agent personas and commands are intact
- [ ] Agents accessible via @agent-name mentions in Claude Code

## 🔗 Agent Command Wrappers (.claude/commands/bmad/axon/agents/)

- [ ] 6 agent command wrappers installed in `.claude/commands/bmad/axon/agents/`
- [ ] All 6 command wrappers identical to `.claude/agents/` versions
- [ ] Command wrappers accessible via slash commands: `/bmad:axon:agents:*`

## 🔄 Workflow Installation

- [ ] 9 workflow command files installed in `.claude/commands/bmad/axon/workflows/`
- [ ] `story-implementation.md` present
- [ ] `story-orchestrator.md` present
- [ ] `story-refactoring.md` present
- [ ] `story-bugfix.md` present
- [ ] `identity-workflow.md` present
- [ ] `chat-workflow.md` present
- [ ] `api-workflow.md` present
- [ ] `pre-flight-validation.md` present
- [ ] `doc-sync.md` present
- [ ] All workflow wrappers reference correct workflow.yaml paths with absolute paths
- [ ] Workflow wrappers include proper execution instructions

## 📄 Path Resolution

- [ ] No `{project-root}` placeholders remain in any installed files
- [ ] All paths are absolute (start with `/Users/valentynkit/Repos/Axon-Backend/`)
- [ ] Config source references resolved correctly
- [ ] Agent override paths (`bmad/_cfg/agents/`) use absolute paths
- [ ] Workflow task paths (`bmad/core/tasks/workflow.md`) use absolute paths

## 📚 Documentation

- [ ] `README.md` created in `.claude/commands/bmad/axon/`
- [ ] README lists all 6 agents with descriptions
- [ ] README lists all 9 workflows with descriptions
- [ ] README includes usage instructions
- [ ] README includes example invocations
- [ ] README includes troubleshooting section

## ✅ Functionality Validation

- [ ] Sample agent file read successfully and XML parses correctly
- [ ] Sample workflow wrapper points to existing workflow.yaml
- [ ] File permissions allow Claude Code to read command files
- [ ] No syntax errors in any installed files
- [ ] Agent activation sequences intact (`<activation>` tags)
- [ ] Command lists (`<cmds>`) preserved in agents

## 🔍 Idempotency Check

- [ ] Running installation again doesn't break existing commands
- [ ] Existing files are overwritten cleanly
- [ ] No duplicate files created
- [ ] Installation can be run multiple times safely

## 🚀 Claude Code Integration

- [ ] Direct agent mentions work: `@axon-story-orchestrator`
- [ ] Slash commands discoverable in Claude Code (type `/`)
- [ ] Agent slash commands appear as `/bmad:axon:agents:*`
- [ ] Workflow commands appear as `/bmad:axon:workflows:*`
- [ ] Commands execute without errors
- [ ] Agents load personas correctly from `.claude/agents/`
- [ ] Workflows load and execute instructions

## 📊 Final Verification

**Installation Summary:**
- Total files installed: _____ (expected: 21 = 6 agents in .claude/agents/ + 6 agent wrappers + 9 workflows + 1 README)
- Primary agent location: `.claude/agents/` ✅
- Command wrapper location: `.claude/commands/bmad/axon/` ✅
- All paths absolute: Yes / No
- Claude Code integration working: Yes / No

**Access Methods Verified:**
- [ ] Direct mention works: `@axon-story-orchestrator`
- [ ] Slash command works: `/bmad:axon:agents:axon-story-orchestrator`
- [ ] Workflow command works: `/bmad:axon:workflows:story-implementation`

**Issues Found:**
- [ ] No issues - installation successful ✅
- [ ] List any issues below:

**Next Steps:**
- [ ] Test direct agent mention: `@axon-story-orchestrator`
- [ ] Test agent slash command: `/bmad:axon:agents:axon-story-orchestrator`
- [ ] Test workflow invocation: `/bmad:axon:workflows:story-implementation`
- [ ] Update Axon module documentation with both access methods
- [ ] Document installation process for future modules