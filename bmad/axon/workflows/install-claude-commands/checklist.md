# Install Claude Code Commands - Validation Checklist

## 📁 Directory Structure

- [ ] `.claude/commands/bmad/axon/` directory exists
- [ ] `.claude/commands/bmad/axon/agents/` directory exists
- [ ] `.claude/commands/bmad/axon/workflows/` directory exists
- [ ] All directories have proper permissions

## 🤖 Agent Installation

- [ ] 6 agent files installed in `.claude/commands/bmad/axon/agents/`
- [ ] `axon-story-orchestrator.md` present and valid
- [ ] `axon-doc-oracle.md` present and valid
- [ ] `axon-archaeologist.md` present and valid
- [ ] `axon-library-sage.md` present and valid
- [ ] `axon-implementation-surgeon.md` present and valid
- [ ] `axon-quality-guardian.md` present and valid
- [ ] All agent files have `{project-root}` replaced with absolute paths
- [ ] All agent files have valid XML structure with `<agent>` tags
- [ ] Agent personas and commands are intact

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

- [ ] Slash commands discoverable in Claude Code (type `/`)
- [ ] Agent commands appear as `/bmad:axon:agents:*`
- [ ] Workflow commands appear as `/bmad:axon:workflows:*`
- [ ] Commands execute without errors
- [ ] Agents load personas correctly
- [ ] Workflows load and execute instructions

## 📊 Final Verification

**Installation Summary:**
- Total files installed: _____ (expected: 16 = 6 agents + 9 workflows + 1 README)
- Installation location verified: `.claude/commands/bmad/axon/`
- All paths absolute: Yes / No
- Claude Code integration working: Yes / No

**Issues Found:**
- [ ] No issues - installation successful ✅
- [ ] List any issues below:

**Next Steps:**
- [ ] Test agent invocation: `/bmad:axon:agents:axon-story-orchestrator`
- [ ] Test workflow invocation: `/bmad:axon:workflows:story-implementation`
- [ ] Update Axon module documentation with slash command usage
- [ ] Document installation process for future modules