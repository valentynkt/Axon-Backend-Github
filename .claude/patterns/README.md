# 📋 Orchestration Patterns

## Available Patterns

### 🚀 **feature-development.md**
Complete feature implementation using SPARC methodology with human validation gates.
- **Agent**: SPARC-Master (9.9/10)
- **Use Case**: New feature development, TDD workflows
- **Duration**: 2-4 hours with human approval gates

### 🏗️ **architecture-review.md** 
Architecture analysis and Clean Architecture compliance validation.
- **Agent**: Axon-Architect (9.7/10)
- **Use Case**: System design review, compliance checking
- **Duration**: 30-60 minutes

### 🤖 **multi-agent-coordination.md**
Complex workflows requiring multiple specialized agents.
- **Agent**: Axon-Orchestrator (9.5/10)
- **Use Case**: Multi-step processes, system coordination
- **Duration**: 1-3 hours depending on complexity

## Usage Instructions

1. **Select Pattern** based on task type
2. **Replace Placeholders** ([FEATURE_NAME], [COMPONENT], etc.)
3. **Execute Template** in single message
4. **Monitor Progress** through agent deliverables

## Pattern Selection Guide

```
Task Type Decision Tree:
├── New Feature → feature-development.md
├── Architecture Work → architecture-review.md
├── Complex Coordination → multi-agent-coordination.md
└── Simple Tasks → Direct Claude execution
```

## Template Customization

Each pattern can be customized by:
- Adjusting agent parameters
- Modifying TodoWrite breakdown
- Adding specific context requirements
- Customizing memory persistence keys