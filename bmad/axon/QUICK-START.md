# Axon Module - Quick Start Guide

**Get productive with Axon in 10 minutes** ⚡

---

## 📋 Prerequisites

Before starting, ensure you have:

1. ✅ **Axon Module installed** at `bmad/axon/`
2. ✅ **Documentation present** in `Docs/ENGINEERING/`
3. ✅ **Claude Code** or compatible AI assistant
4. ✅ **Project setup**: .NET 10, Clean Architecture, CQRS

---

## 🚀 Your First Story (5 Minutes)

### Step 1: Create a Story

Create a story file in `Docs/PROCESS/active-stories/`:

```markdown
# Story: Add User Logout Endpoint

**Story ID**: STORY-TEST-001
**Module**: Identity
**Story Type**: Feature
**Priority**: Medium
**Complexity**: Simple

## User Story

**As a** user
**I want** to logout and invalidate my session
**So that** my account remains secure

## Acceptance Criteria

1. **AC1**: POST /api/v1/auth/logout returns 200 OK
   - Test: Given authenticated user, When logout called, Then returns success

2. **AC2**: Session token is invalidated after logout
   - Test: Given logged out user, When using old token, Then returns 401

3. **AC3**: Refresh token is revoked
   - Test: Given logged out user, When attempting refresh, Then fails
```

### Step 2: Load the Story Orchestrator

In Claude Code, run:

```
Load agent: bmad/axon/agents/axon-story-orchestrator.md
```

### Step 3: Execute the Story

```
@axon-story-orchestrator implement-story

Story file: Docs/PROCESS/active-stories/STORY-TEST-001.md
```

### Step 4: Review Checkpoints

The workflow will pause at **4 checkpoints**:

1. **Understanding** (1 min): Review story summary → [c] Continue
2. **Pre-flight** (3-5 min): Review discovery/library/patterns → [c] Continue
3. **Implementation** (5-10 min): Review code diff → [c] Continue
4. **Final** (2 min): Review quality gates → [c] Commit

**Total time**: 11-18 minutes of review

---

## 🎯 Common Workflows

### Feature Development (Identity Module)

```bash
# 1. Create story in active-stories/
# 2. Load orchestrator
Load: bmad/axon/agents/axon-story-orchestrator.md

# 3. Implement
@axon-story-orchestrator implement-story
Input: Docs/PROCESS/active-stories/STORY-XXX.md

# 4. Workflow automatically routes to identity-workflow
# 5. Review 4 checkpoints
# 6. Commit when ready
```

### Refactoring Existing Code

```bash
# 1. Create refactoring story
# 2. Load orchestrator
Load: bmad/axon/agents/axon-story-orchestrator.md

# 3. Execute refactoring workflow
@axon-story-orchestrator implement-story
Input: Docs/PROCESS/active-stories/REFACTOR-XXX.md

# Workflow detects "Refactor" and routes to story-refactoring
```

### Bug Fix

```bash
# 1. Create bugfix story with reproduction steps
# 2. Load orchestrator
Load: bmad/axon/agents/axon-story-orchestrator.md

# 3. Execute bugfix workflow
@axon-story-orchestrator implement-story
Input: Docs/PROCESS/active-stories/BUG-XXX.md

# Workflow performs root cause analysis
```

---

## 🦸 Meet the Agents

### When to Use Each Agent

| Agent | Command | Use Case |
|-------|---------|----------|
| **Story Orchestrator** 🎯 | `implement-story` | Start any story (routes automatically) |
| **Doc Oracle** 📚 | `validate-patterns` | Check pattern compliance manually |
| **Archaeologist** 🔍 | `discover-code` | Search for existing implementations |
| **Library Sage** 🛠️ | `check-library` | Validate library usage |
| **Implementation Surgeon** ⚙️ | `apply-pattern` | Generate code with patterns |
| **Quality Guardian** ✅ | `generate-tests` | Generate comprehensive tests |

### Quick Agent Reference

```bash
# Load any agent:
Load: bmad/axon/agents/{agent-name}.md

# Available agents:
- axon-story-orchestrator.md
- axon-doc-oracle.md
- axon-archaeologist.md
- axon-library-sage.md
- axon-implementation-surgeon.md
- axon-quality-guardian.md
```

---

## 📚 Story Template

Use this template for all stories:

```bash
# Copy template
cp bmad/axon/templates/story-template.md Docs/PROCESS/active-stories/STORY-XXX.md

# Required fields:
- Story ID (unique)
- Module (Identity | Chat | API | Cross-cutting)
- Story Type (Feature | Refactor | Bugfix)
- User Story (As a... I want... So that...)
- Acceptance Criteria (3-5, testable)
- Technical Context (patterns, ADRs)
```

---

## ⚡ Fast Track Mode (#yolo)

**For experienced users only** - Skip optional steps and minimize prompts:

### Enable #yolo Mode

```bash
# In your prompt:
@axon-story-orchestrator implement-story #yolo
Input: Docs/PROCESS/active-stories/STORY-XXX.md
```

**What #yolo skips**:
- ❌ Optional validation steps
- ❌ Elicitation menus
- ❌ Detailed explanations
- ✅ **Still stops at 4 critical checkpoints**

**When to use**:
- ✅ Simple stories (complexity: Simple)
- ✅ Well-understood domains
- ✅ Trusted patterns
- ✅ Time-sensitive work

**When NOT to use**:
- ❌ Complex refactoring
- ❌ New domain areas
- ❌ Experimental features
- ❌ High-risk changes

---

## 🎯 Module-Specific Workflows

### Identity Module

**Best for**: Authentication, wallet management, principal resolution, credentials

```bash
# Automatically enhanced with Identity context:
- 7 Identity-specific docs loaded
- Owned entity patterns (AxonPrincipal, IdentityCredential, WalletOwnership)
- 6 domain invariants validated
- Ed25519/JWKS/Dynamic.xyz library awareness
```

**Example stories**:
- Add wallet verification
- Implement credential revocation
- Add multi-chain support
- Fix principal resolution bug

### Chat Module

**Best for**: Conversations, messages, AI integration

```bash
# Automatically enhanced with Chat context:
- Chat-specific docs loaded
- Business rules validation
- Owned entity patterns (Message, ConversationParticipant)
- AI service integration patterns
```

### API Module

**Best for**: FastEndpoints, validation, REST patterns

```bash
# Automatically enhanced with API context:
- FastEndpoints patterns
- FluentValidation integration
- Request/response mapping
- Error handling conventions
```

---

## 🔍 Discovery & Reuse

### Before Creating New Code

**Always run discovery first**:

```bash
# Load archaeologist
Load: bmad/axon/agents/axon-archaeologist.md

# Discover existing implementations
@axon-archaeologist discover-code
Input: "wallet verification patterns"

# Get reuse recommendations:
# - REUSE: Use as-is
# - EXTEND: Add functionality
# - ADAPT: Modify pattern
# - CREATE: Build new
```

### Check Library Capabilities

```bash
# Load library sage
Load: bmad/axon/agents/axon-library-sage.md

# Check if library solves problem
@axon-library-sage check-library
Input: "JWT validation with custom claims"

# Get recommendations:
# - Library-First (use existing)
# - Hybrid (library + custom)
# - Manual (write custom)
```

---

## ✅ Quality Assurance

### Pattern Compliance

**Axon validates 6 patterns automatically**:
1. ✅ Result<T, Error> for error handling
2. ✅ StrongId<T> for entity IDs
3. ✅ CQRS separation (commands vs queries)
4. ✅ Domain events for state changes
5. ✅ Owned entities (EF Core patterns)
6. ✅ ADR compliance

**Target**: 95%+ compliance

### Test Coverage

**Axon generates 4 test layers**:
1. **Domain**: Aggregates, value objects, events
2. **Application**: Command/query handlers
3. **Infrastructure**: EF Core, external services
4. **E2E**: API endpoints, full workflows

**Target**: 90%+ coverage, 100% AC coverage

---

## 📊 Monitoring Progress

### During Workflow Execution

```
Phase 0: Story Understanding [████████░░] 80% (2 min)
Phase 1: Pre-Flight Validation [████░░░░░░] 40% (5 min)
Phase 2: Implementation [░░░░░░░░░░] 0% (0 min)
Phase 3: Validation [░░░░░░░░░░] 0% (0 min)
```

### After Completion

**Check these outputs**:
```bash
# Implementation log
cat {output_folder}/implementation-log.md

# Pre-flight package
cat {output_folder}/preflight-package.md

# Implementation diff
cat {output_folder}/implementation-diff.md

# Decision log
cat {output_folder}/decisions/STORY-XXX-decisions.yaml
```

---

## 🚧 Common Pitfalls

### ❌ Don't Do This

1. **Skip story template**: Leads to poor story quality
2. **Ignore checkpoints**: Review is critical for quality
3. **Modify during execution**: Wait for checkpoints
4. **Use #yolo for complex stories**: Too risky
5. **Skip documentation**: Causes drift

### ✅ Do This Instead

1. **Use story template**: Ensures completeness
2. **Review at checkpoints**: 11-18 minutes well spent
3. **Wait for previews**: See changes before applying
4. **Use normal mode for complex**: Safety first
5. **Keep docs updated**: Run doc-sync regularly

---

## 🎓 Learning Path

### Week 1: Basics
- ✅ Run 1-2 simple feature stories
- ✅ Get familiar with 4 checkpoints
- ✅ Review generated code patterns
- ✅ Run tests manually

### Week 2: Intermediate
- ✅ Try refactoring workflow
- ✅ Use bugfix workflow
- ✅ Explore agent commands individually
- ✅ Customize story templates

### Week 3: Advanced
- ✅ Use #yolo mode selectively
- ✅ Optimize workflow performance
- ✅ Contribute patterns to catalog
- ✅ Create custom workflows

---

## 📞 Need Help?

### Quick References

- **Full documentation**: `bmad/axon/README.md`
- **Troubleshooting**: `bmad/axon/TROUBLESHOOTING.md`
- **Workflow docs**: `bmad/axon/workflows/{workflow}/README.md`
- **Agent docs**: Inline in `bmad/axon/agents/*.md`

### Common Commands

```bash
# List all workflows
ls bmad/axon/workflows/

# List all agents
ls bmad/axon/agents/*.md

# Check configuration
cat bmad/axon/config.yaml

# Validate setup
find bmad/axon -name "*.yaml" | wc -l  # Should be ~15
find bmad/axon -name "*.md" | wc -l    # Should be ~76
```

### Health Check

```bash
# Quick diagnostics
ls bmad/axon/{agents,workflows,tasks,templates,data}

# Should see:
# agents: 7 files
# workflows: 10 directories
# tasks: 35 files
# templates: 3 files
# data: 3 files
```

---

## 🎉 Success Checklist

After your first story, verify:

- ✅ Code compiles (`dotnet build`)
- ✅ All tests pass (`dotnet test`)
- ✅ Pattern compliance ≥ 95%
- ✅ Test coverage ≥ 90%
- ✅ AC coverage = 100%
- ✅ Documentation updated
- ✅ Decision log created
- ✅ Git commit clean

**Congratulations!** You've successfully used the Axon Module! 🎊

---

## 🚀 Next Steps

1. **Run more stories**: Practice with different types
2. **Explore agents**: Try individual agent commands
3. **Customize**: Adapt templates and workflows
4. **Contribute**: Add patterns to catalog
5. **Optimize**: Fine-tune for your team

---

**Happy coding with Axon!** 🦸‍♂️

**Module Version**: 1.0.0
**Last Updated**: 2025-09-30