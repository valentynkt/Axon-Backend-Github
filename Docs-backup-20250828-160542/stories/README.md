# Story-Driven Development Workflow

## Overview

This directory contains all stories for the Axon Backend, following BMAD methodology integrated with Axon-specific development patterns.

## Story Lifecycle

### 1. Story Creation (BMAD SM Agent)
```bash
@sm → create-story
# Creates story from sharded epic documentation
# Story starts in "Draft" status
# Uses story-template.md as base structure
```

### 2. Research Gate (Axon Research Architect) ✅ MANDATORY
```bash
@axon-research-architect → research-solutions "story-topic"  
# REQUIRED before any implementation
# Creates ADR with build vs buy recommendation
# Documents solution validation and approval
```

### 3. Story Implementation (Axon Agents)
```bash
@axon-story-orchestrator → implement-story "1.2"
# Orchestrates complete story development
# Coordinates research → implementation → quality
# Manages dependencies and epic coherence
```

### 4. Quality Validation (Axon Quality Guardian)
```bash
@axon-quality-guardian → validate-requirements "story 1.2"
# Ensures acceptance criteria satisfaction
# Validates architecture compliance  
# Confirms testing strategy execution
```

## Story Organization

### File Naming Convention
- **Format**: `story-{epic}.{number}-{short-description}.md`
- **Examples**: 
  - `story-1.1-user-authentication.md`
  - `story-2.3-payment-processing.md`
  - `story-3.1-chat-real-time.md`

### Directory Structure
```
stories/
├── README.md                          # This file
├── story-template.md                  # Template for new stories
├── epic-1-authentication/            # Epic-based organization
│   ├── story-1.1-user-login.md
│   ├── story-1.2-jwt-tokens.md
│   └── story-1.3-role-management.md
├── epic-2-chat-system/
│   ├── story-2.1-create-chatroom.md
│   └── story-2.2-real-time-messaging.md
└── completed/                         # Archived completed stories
```

## Story Status Values

| Status | Description | Actions Available |
|--------|-------------|------------------|
| **Draft** | SM created, needs validation | Review, approve, or request changes |
| **Ready** | Approved, ready for development | Assign to developer, start implementation |
| **In Progress** | Developer actively working | Continue development, update progress |
| **Review** | Implementation complete, needs QA | Code review, testing, validation |
| **Done** | All acceptance criteria met | Archive, prepare for deployment |

## Integration with BMAD Workflow

### Planning Phase (BMAD Agents)
1. **@analyst** → Business analysis and requirements gathering
2. **@pm** → PRD creation with epics and initial stories  
3. **@architect** → Architecture design with technical constraints
4. **@po** → Document validation and sharding for development

### Development Phase (Axon Agents)
1. **@sm** → Story creation from sharded epics
2. **@axon-research-architect** → Research gate validation (MANDATORY)
3. **@axon-story-orchestrator** → Story implementation orchestration
4. **@axon-quality-guardian** → Quality assurance and validation

## Research-First Development

Every story MUST pass through the research gate:
1. **Solution Research**: Check existing .NET libraries and frameworks
2. **ADR Creation**: Document build vs buy decision with evidence  
3. **Architecture Alignment**: Ensure solution fits Clean Architecture + CQRS
4. **Research Approval**: Get explicit approval before implementation starts

## Quality Standards

- **Acceptance Criteria**: All must be testable and verifiable
- **Architecture Compliance**: Must follow Clean Architecture + CQRS patterns  
- **Test Coverage**: >90% unit test coverage for business logic
- **Code Quality**: No warnings in Release build, passes all analyzers