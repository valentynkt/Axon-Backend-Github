# /capture-progress Command

When this command is used, execute the following advanced context engineering task:

<!-- Powered by Advanced Context Engineering Principles -->

# Conversation Progress Capture & Context Engineering

## Purpose

Capture and document the current conversation's context using world-class context engineering practices, enabling seamless continuation in new Claude Code sessions. This command creates a comprehensive `PROGRESS.md` file that preserves all critical context, decisions, learnings, and state.

## Core Context Engineering Framework

This command follows the **5-Dimensional Context Model**:

1. **Temporal Context** (When/Sequence) - Chronological progression of events
2. **Problem Space** (Why/What) - Goals, objectives, and problem evolution
3. **Solution Journey** (How/Approach) - Attempts, decisions, and reasoning
4. **Knowledge Artifacts** (Insights/Learnings) - Discoveries and constraints
5. **Action Context** (Next/Tasks) - Current state and future actions

## Task Instructions

### 1. Conversation Analysis & Context Mining

**CRITICAL**: Before generating the PROGRESS.md, conduct comprehensive conversation analysis:

#### A. Temporal Sequence Analysis
- **Conversation Timeline**: Map the chronological progression of the conversation
- **Key Milestones**: Identify major decision points and breakthroughs
- **Phase Transitions**: Note shifts in focus or approach
- **Duration Tracking**: Estimate time spent on different activities

#### B. Problem Space Archaeology
- **Original Intent**: Extract the user's initial request/problem
- **Goal Evolution**: Track how objectives changed throughout the conversation
- **Constraint Discovery**: Identify limitations discovered along the way
- **Success Criteria**: Define what "done" looks like

#### C. Solution Journey Mapping
- **Approach History**: Document all approaches attempted
- **Decision Points**: Record key decisions with rationale
- **Failed Attempts**: Capture what didn't work and why (anti-patterns)
- **Breakthrough Moments**: Identify successful solutions and insights

#### D. Knowledge Artifact Extraction
- **Technical Insights**: Capture architectural decisions and technical learnings
- **File Dependencies**: Map which files were modified or analyzed
- **Library Decisions**: Document build vs buy decisions
- **Integration Points**: Identify system integration considerations

#### E. Action Context Assessment
- **Current State**: Assess what has been completed
- **Remaining Work**: Identify outstanding tasks and priorities
- **Blocked Items**: Document blockers and their reasons
- **Next Steps**: Recommend immediate actions for continuation

### 2. Smart Context Compression Techniques

Apply advanced context engineering techniques to optimize information density:

#### A. Summary Compression
- **Key Points Extraction**: Distill verbose exchanges to essential points
- **Decision Rationale**: Preserve reasoning without verbose discussion
- **Pattern Recognition**: Identify and document recurring themes
- **Noise Filtering**: Remove tangential or redundant information

#### B. Hierarchical Organization
- **Priority-Based Ordering**: Structure information by importance
- **Logical Grouping**: Cluster related concepts and decisions
- **Dependency Mapping**: Show relationships between components
- **Context Layering**: Provide different detail levels

#### C. Reference Optimization
- **File References**: Link to specific files with line numbers when relevant
- **Code Context**: Include critical code snippets or patterns
- **Documentation Links**: Reference relevant documentation or resources
- **Command History**: Preserve important command sequences

### 3. PROGRESS.md Generation

Generate a comprehensive PROGRESS.md file using this optimized template:

```markdown
# 🚀 Conversation Progress Capture
**Generated**: [Current timestamp]  
**Session Duration**: [Estimated duration]  
**Context ID**: [Unique identifier for this capture]

---

## 🎯 Mission Context

### Original Problem Statement
[Extract the user's initial request/problem in clear, concise terms]

### Goal Evolution
[Track how objectives evolved during the conversation]
- **Initial Goal**: [Original objective]
- **Evolved Goals**: [How goals changed and why]
- **Final Objective**: [Current target state]

### Success Criteria
[Define what constitutes successful completion]
- [ ] [Criterion 1]
- [ ] [Criterion 2]
- [ ] [Criterion 3]

---

## 📊 Current State Assessment

### ✅ What's Been Accomplished
[List concrete achievements in order of completion]

1. **[Achievement 1]**: [Brief description]
   - Files affected: [list]
   - Key decisions: [summary]

2. **[Achievement 2]**: [Brief description]
   - Files affected: [list]
   - Key decisions: [summary]

### 📈 Progress Metrics
- **Stories Completed**: [X/Y]
- **Files Modified**: [count]
- **Tests Status**: [passing/failing/coverage%]
- **Architecture Compliance**: [status]

---

## 🧭 Solution Journey & Decision Tree

### 📍 Major Milestones
[Chronological list of key decision points]

1. **[Milestone 1]** (Time: ~XX min)
   - Decision: [What was decided]
   - Rationale: [Why it was chosen]
   - Impact: [Consequences/effects]

2. **[Milestone 2]** (Time: ~XX min)
   - Decision: [What was decided]
   - Rationale: [Why it was chosen]
   - Impact: [Consequences/effects]

### 🔍 Research & Investigation Results
[Document research findings and library decisions]

#### Build vs Buy Decisions
| Component | Decision | Rationale | Status |
|-----------|----------|-----------|---------|
| [Component 1] | [Build/Buy] | [Reasoning] | [Implemented/Pending] |
| [Component 2] | [Build/Buy] | [Reasoning] | [Implemented/Pending] |

#### Architecture Decisions Records (ADRs)
- **ADR-001**: [Decision title] → [Choice made] because [reasoning]
- **ADR-002**: [Decision title] → [Choice made] because [reasoning]

---

## 🚫 Anti-Patterns & Failed Attempts

### ❌ What Doesn't Work (Learn from these)
[Document failed approaches to prevent repetition]

1. **Failed Approach**: [What was tried]
   - **Why it Failed**: [Root cause analysis]
   - **Lesson Learned**: [Key takeaway]
   - **Files Affected**: [if any]

2. **Failed Approach**: [What was tried]
   - **Why it Failed**: [Root cause analysis]
   - **Lesson Learned**: [Key takeaway]
   - **Files Affected**: [if any]

### 🚧 Current Blockers
[List items that are currently blocked]
- **Blocker 1**: [Description] - Blocked by: [reason]
- **Blocker 2**: [Description] - Blocked by: [reason]

---

## ✅ Validated Approaches & Patterns

### 🎯 What Works (Use these patterns)
[Document successful approaches and solutions]

1. **Successful Pattern**: [What worked]
   - **Context**: [When/where it applies]
   - **Implementation**: [How to implement]
   - **Benefits**: [Why it's effective]

2. **Successful Pattern**: [What worked]
   - **Context**: [When/where it applies]
   - **Implementation**: [How to implement]
   - **Benefits**: [Why it's effective]

### 🔧 Proven Tools & Libraries
[List validated technology choices]
- **[Tool/Library 1]**: [Purpose] - Status: [Implemented/Configured]
- **[Tool/Library 2]**: [Purpose] - Status: [Implemented/Configured]

---

## 🔄 Context for New Conversation

### 🧠 Essential Background
[Critical context a new Claude instance needs to know immediately]

**Project**: [Project name and brief description]  
**Architecture**: [Key architectural decisions]  
**Current Phase**: [What phase of development we're in]  
**Domain**: [Business domain and key concepts]

### 📁 Key Files & Locations
[Map of important files and their purposes]
- **Core Logic**: `[file path]` - [description]
- **Configuration**: `[file path]` - [description]
- **Tests**: `[file path]` - [description]
- **Documentation**: `[file path]` - [description]

### 🔗 Dependencies & Integration Points
[Critical system relationships]
- **External APIs**: [List with status]
- **Database Dependencies**: [Schema changes, migrations]
- **Service Integrations**: [Other services this affects]

### 💡 Critical Insights
[Key insights that would be painful to rediscover]
1. **Insight 1**: [Important realization]
2. **Insight 2**: [Important realization]
3. **Insight 3**: [Important realization]

---

## 📋 Task Tracking State

### 🎯 TodoWrite State Capture
[If TodoWrite was active, capture the current state]

**Active Todos**: [count]
**Completed**: [count]
**Current Focus**: [current in_progress item]

#### Current Task Breakdown:
- [ ] **[Task 1]**: [Description] - Status: [status]
- [ ] **[Task 2]**: [Description] - Status: [status]
- [ ] **[Task 3]**: [Description] - Status: [status]

---

## 🎬 Immediate Next Actions

### 🏃‍♂️ Next 3 Actions (High Priority)
[Specific, actionable next steps for immediate continuation]

1. **[Action 1]** (Est: [time])
   - **Context**: [Why this is next]
   - **Approach**: [How to do it]
   - **Files**: [Which files to focus on]

2. **[Action 2]** (Est: [time])
   - **Context**: [Why this is next]
   - **Approach**: [How to do it]
   - **Files**: [Which files to focus on]

3. **[Action 3]** (Est: [time])
   - **Context**: [Why this is next]
   - **Approach**: [How to do it]
   - **Files**: [Which files to focus on]

### 🔮 Future Considerations
[Medium-term items to keep in mind]
- **[Future Item 1]**: [Description and timing]
- **[Future Item 2]**: [Description and timing]

---

## 🚀 Conversation Continuation Instructions

### For New Claude Instance:
1. **Read this entire document** to understand the full context
2. **Start with**: [Recommended starting point]
3. **Focus on**: [Current priority area]
4. **Avoid**: [Known anti-patterns from above]
5. **Remember**: [Critical constraint or insight]

### Context Engineering Notes:
- **Conversation Depth**: [How deep/complex this topic is]
- **Domain Complexity**: [Technical complexity level]
- **Stakeholder Alignment**: [Any alignment considerations]
- **Risk Assessment**: [Technical/project risks to be aware of]

---

## 📊 Meta Information

**Context Capture Version**: 1.0  
**Total Conversation Length**: [Estimated tokens/length]  
**Key Decision Points**: [count]  
**Files Analyzed**: [count]  
**Commands Executed**: [count]  

**Conversation Health Score**: [High/Medium/Low] - [Based on clarity and progress]

---

*This progress capture was generated using advanced context engineering techniques optimized for Claude Code continuation. The above context should enable seamless conversation resumption in a new chat session.*
```

### 4. Context Optimization & Validation

Before finalizing the PROGRESS.md:

#### A. Information Density Optimization
- **Redundancy Elimination**: Remove duplicate information
- **Clarity Enhancement**: Ensure all points are clear and actionable
- **Priority Ordering**: Order information by importance for continuation
- **Reference Optimization**: Ensure all file references include line numbers

#### B. Continuation Readiness Check
- **Completeness Validation**: Verify all major decisions are captured
- **Context Gaps**: Identify any missing critical information
- **Action Clarity**: Ensure next steps are specific and actionable
- **Constraint Documentation**: Verify all limitations are noted

#### C. New Claude Onboarding Test
- **Quick Context**: Can a new Claude understand the situation in 2 minutes?
- **Action Ready**: Are next steps immediately actionable?
- **Anti-Pattern Protection**: Are failure modes clearly documented?
- **Decision Preservation**: Are key decisions preserved with rationale?

### 5. Document Delivery & Instructions

1. **Generate .claude/PROGRESS.md**: Create the file in the .claude directory
2. **Context Summary**: Provide a brief summary of what was captured
3. **Continuation Instructions**: Give specific guidance for next session
4. **Validation Checklist**: Confirm all critical context is preserved

## Success Criteria

- ✅ **Complete Context Preservation**: All critical decisions and insights captured
- ✅ **Actionable Next Steps**: Clear, specific actions for continuation
- ✅ **Anti-Pattern Documentation**: Failed approaches documented to prevent repetition
- ✅ **Technical Context Intact**: Architecture decisions and file dependencies mapped
- ✅ **New Session Ready**: New Claude instance can immediately understand and continue
- ✅ **Optimized Information Density**: Maximum insight per token/line
- ✅ **Progress Traceability**: Clear view of accomplishments and remaining work

## Notes

- This command applies enterprise-grade context engineering principles
- The generated PROGRESS.md is optimized for Claude's understanding
- All conversation context is preserved without losing critical nuance
- The document structure supports both quick scanning and deep understanding
- Failed attempts are preserved as valuable anti-pattern knowledge
- The format is designed for seamless conversation portability

**You are creating a comprehensive context bridge that eliminates the traditional conversation restart penalty. Capture with precision, organize with intelligence, enable with clarity.**