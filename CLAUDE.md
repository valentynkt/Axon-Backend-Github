# 🧠 CLAUDE.md - Claude Flow Automation Hub

**ABSOLUTE PARADIGM**: Claude Flow is the primary orchestration engine. ALL interactions, decisions, and work coordination MUST flow through Claude Flow MCP tools. Claude Code is the execution layer, Claude Flow is the intelligence layer.

## 🌊 CLAUDE FLOW AS UNIVERSAL ENTRY POINT

### 🚨 MANDATORY AUTOMATION PROTOCOL

**RULE #1**: Every session MUST begin with Claude Flow initialization:
```bash
# MANDATORY session start sequence
npx claude-flow@alpha hive-mind init
npx claude-flow@alpha hive-mind spawn "Axon Backend development session" --claude --auto-spawn --monitor
```

**RULE #2**: ALL work delegation MUST use Claude Flow MCP coordination:
- 🧠 **Intelligence Layer**: Claude Flow MCP tools coordinate, plan, decide
- 🛠️ **Execution Layer**: Claude Code executes based on Claude Flow instructions
- 💾 **Memory Layer**: All context, decisions, progress stored in Claude Flow memory

### 🐝 HIVE MIND ORCHESTRATION SYSTEM

**Primary Workflow Pattern**:
```javascript
// ✅ CORRECT: Claude Flow-first approach
[Message 1 - Intelligence Coordination]:
  mcp__claude-flow__hive_mind_init()
  mcp__claude-flow__swarm_init { topology: "hierarchical", maxAgents: 8 }
  mcp__claude-flow__agent_spawn { type: "coordinator", name: "Session Manager" }
  mcp__claude-flow__agent_spawn { type: "architect", name: "Code Architect" }
  mcp__claude-flow__agent_spawn { type: "coder", name: "Implementation Expert" }
  mcp__claude-flow__agent_spawn { type: "tester", name: "Quality Guardian" }
  mcp__claude-flow__memory_usage { action: "store", key: "session/init", value: {...} }

[Message 2 - Execution Coordination]:
  mcp__claude-flow__task_orchestrate { task: "Build feature X", strategy: "adaptive" }
  mcp__claude-flow__hooks_pre_task { description: "Feature development", auto_spawn_agents: true }
  Task("Coordinator Agent: Orchestrate feature development with full Claude Flow integration")
  Task("Architect Agent: Design system with Claude Flow memory persistence")
  Task("Coder Agent: Implement with Claude Flow progress tracking")
  Task("Tester Agent: Validate with Claude Flow quality gates")
  TodoWrite { todos: [10+ comprehensive todos with Claude Flow coordination] }
```

## 🎯 AXON BACKEND + CLAUDE FLOW INTEGRATION

### Project Architecture Enhanced with Claude Flow

**Stack**: .NET 10 Preview • Clean Architecture • DDD • CQRS (MediatR) • FastEndpoints • **Claude Flow MCP Orchestration** • OpenAI Direct MCP • NUnit • Shouldly

**Enhanced Architecture with Claude Flow Layer**:
```
🧠 Claude Flow Intelligence Layer (NEW!)
├── Hive Mind Coordination
├── Memory Management & Persistence  
├── Agent Orchestration & Delegation
├── Workflow Automation & Optimization
└── Performance Analytics & Learning

🏗️ Clean Architecture Implementation Layer
├── src/Api/ (HTTP host, endpoints, DTOs)
├── src/Shared/ (Result<T>, Error, primitives)
├── src/Modules/Chat/ (Bounded context)
│   ├── Application/ (Commands, handlers, ports)
│   ├── Domain/ (Aggregates, entities, VOs)
│   └── Infrastructure/ (Adapters, OpenAI client)
└── tests/ (Mirrors src/ structure)
```

### 🔄 CLAUDE FLOW LIFECYCLE MANAGEMENT

**Every Development Session Pattern**:
```bash
# 1. Session Initialization (MANDATORY)
npx claude-flow@alpha hooks pre-task --description "Development session" --auto-spawn-agents

# 2. Memory Context Loading (AUTOMATIC)
npx claude-flow@alpha memory query "axon-backend" --namespace project

# 3. Agent Coordination Setup (ORCHESTRATED)
npx claude-flow@alpha hive-mind spawn "Development objective" --claude --execute

# 4. Progress Tracking (CONTINUOUS)
npx claude-flow@alpha hooks post-edit --file "path" --memory-key "session/progress"

# 5. Session Completion (AUTOMATED)
npx claude-flow@alpha hooks session-end --export-metrics --generate-summary
```

## 🛠️ DEVELOPMENT COMMANDS (Claude Flow Enhanced)

### Essential Commands with Claude Flow Integration
```bash
# Build with Claude Flow monitoring
npx claude-flow@alpha hooks pre-task --description "Build solution" && \
dotnet build && \
npx claude-flow@alpha hooks post-task --analyze-performance

# Test execution with progress tracking
npx claude-flow@alpha hooks pre-task --description "Run tests" && \
dotnet test && \
npx claude-flow@alpha hooks post-task --generate-insights

# Architecture validation with Claude Flow memory
npx claude-flow@alpha memory store "arch-test-results" "$(dotnet test tests/Axon.ArchitectureTests.Core/)"

# Feature development with full orchestration
npx claude-flow@alpha hive-mind spawn "Implement ProcessMessage feature" --claude --auto-spawn --execute
```

## 🧠 CLAUDE FLOW MCP TOOL ECOSYSTEM

### Core Orchestration Tools (ALWAYS USE THESE)
- `mcp__claude-flow__hive_mind_init` - Initialize session intelligence
- `mcp__claude-flow__swarm_init` - Setup coordination topology  
- `mcp__claude-flow__agent_spawn` - Create specialized agents
- `mcp__claude-flow__task_orchestrate` - Coordinate complex workflows
- `mcp__claude-flow__memory_usage` - Persistent memory management
- `mcp__claude-flow__hooks_*` - Lifecycle event automation

### Intelligence Enhancement Tools
- `mcp__claude-flow__neural_train` - Learn from development patterns
- `mcp__claude-flow__neural_patterns` - Analyze cognitive approaches
- `mcp__claude-flow__learning_adapt` - Improve coordination over time
- `mcp__claude-flow__bottleneck_analyze` - Identify performance issues
- `mcp__claude-flow__performance_report` - Generate analytics

### Workflow Automation Tools  
- `mcp__claude-flow__workflow_create` - Define reusable workflows
- `mcp__claude-flow__automation_setup` - Configure automation rules
- `mcp__claude-flow__pipeline_create` - Setup CI/CD coordination  
- `mcp__claude-flow__scheduler_manage` - Task scheduling
- `mcp__claude-flow__parallel_execute` - Concurrent task execution

### GitHub Integration Tools
- `mcp__claude-flow__github_repo_analyze` - Deep repository analysis
- `mcp__claude-flow__github_pr_manage` - Pull request coordination
- `mcp__claude-flow__github_workflow_auto` - Workflow automation
- `mcp__claude-flow__github_code_review` - Automated code review

## 🎯 MANDATORY DELEGATION PATTERNS

### 🚨 CRITICAL: Claude Flow Responsibility Matrix

**Claude Flow MCP Tools Handle (90% of decision-making)**:
- 🧠 **Strategic Planning**: All feature design, architecture decisions
- 📊 **Performance Monitoring**: Real-time metrics, bottleneck analysis  
- 💾 **Memory Management**: Context persistence, learning accumulation
- 🤝 **Agent Coordination**: Task delegation, progress synchronization
- 🔄 **Workflow Orchestration**: Complex multi-step processes
- 📈 **Continuous Learning**: Pattern recognition, optimization suggestions
- 🔍 **Quality Gates**: Architecture validation, performance benchmarks
- 🚨 **Error Prevention**: Proactive issue detection, risk mitigation

**Claude Code Handles (10% of execution)**:
- ⌨️ **File Operations**: Read, Write, Edit based on Claude Flow instructions
- 🏃 **Command Execution**: Bash commands as directed by Claude Flow
- 🔧 **Code Generation**: Implementation following Claude Flow design
- 🧪 **Test Execution**: Running tests as orchestrated by Claude Flow

### 🔄 DELEGATION WORKFLOW EXAMPLE

**Feature Development with Full Claude Flow Orchestration**:
```javascript
// Phase 1: Intelligence & Planning (Claude Flow leads)
mcp__claude-flow__hive_mind_spawn("Implement user authentication")
mcp__claude-flow__memory_usage("store", "auth-requirements", specifications)
mcp__claude-flow__agent_spawn("architect", "Auth System Designer")
mcp__claude-flow__neural_patterns("analyze", "authentication-patterns")

// Phase 2: Coordination & Design (Claude Flow orchestrates)  
mcp__claude-flow__task_orchestrate("Design auth architecture")
mcp__claude-flow__workflow_create("auth-implementation-pipeline")
mcp__claude-flow__performance_report("baseline-metrics")

// Phase 3: Execution (Claude Code executes under Claude Flow guidance)
Task("Implementation Agent: Build auth following Claude Flow design")
TodoWrite([10+ todos coordinated by Claude Flow])
Read/Write/Edit operations based on Claude Flow instructions

// Phase 4: Validation & Learning (Claude Flow validates & learns)
mcp__claude-flow__bottleneck_analyze("auth-implementation")
mcp__claude-flow__neural_train("auth-development-patterns")  
mcp__claude-flow__memory_usage("store", "auth-lessons", learnings)
```

## 🎯 AXON BACKEND DEVELOPMENT WITH CLAUDE FLOW

### Clean Architecture + Claude Flow Pattern

**CQRS Command Development with Full Orchestration**:
```javascript
// 1. Strategic Planning (Claude Flow Intelligence)
mcp__claude-flow__hive_mind_spawn("ProcessMessage command implementation")
mcp__claude-flow__memory_usage("query", "cqrs-patterns")
mcp__claude-flow__agent_spawn("architect", "CQRS Command Designer")

// 2. Architecture Design (Claude Flow Coordination)
mcp__claude-flow__task_orchestrate("Design ProcessMessage architecture")
mcp__claude-flow__neural_patterns("analyze", "vertical-slice-patterns")
mcp__claude-flow__workflow_create("cqrs-implementation-workflow")

// 3. Implementation (Claude Code Execution)
Task("CQRS Agent: Implement ProcessMessage with Claude Flow coordination")
Write("src/Modules/Chat/Application/Commands/ProcessMessage/ProcessMessageCommand.cs")
Write("src/Modules/Chat/Application/Commands/ProcessMessage/ProcessMessageHandler.cs")

// 4. Validation & Learning (Claude Flow Quality Gates)
mcp__claude-flow__performance_report("cqrs-implementation-metrics")
mcp__claude-flow__neural_train("clean-architecture-patterns")
```

### Result Pattern with Claude Flow Enhancement
```csharp
// Enhanced Result pattern with Claude Flow integration
public async Task<Result<ProcessMessageResponse>> Handle(
    ProcessMessageCommand command, 
    CancellationToken cancellationToken)
{
    // Claude Flow pre-execution hook
    await claudeFlow.HookPreExecution("ProcessMessage", command);
    
    var result = await ProcessMessageCore(command, cancellationToken);
    
    // Claude Flow post-execution learning
    await claudeFlow.HookPostExecution("ProcessMessage", result);
    
    return result;
}
```

## 🚀 AUTOMATION WORKFLOWS

### 🔄 DAILY DEVELOPMENT AUTOMATION

**Morning Routine (Fully Automated)**:
```bash
# Claude Flow handles entire morning setup
npx claude-flow@alpha automation-setup --rules morning-dev-routine
npx claude-flow@alpha hive-mind spawn "Daily development session" --auto-spawn --execute
# Automatically: updates dependencies, runs tests, analyzes metrics, plans day
```

**Feature Development (90% Automated)**:
```bash
# Claude Flow orchestrates entire feature lifecycle
npx claude-flow@alpha workflow-create feature-development-pipeline
npx claude-flow@alpha hive-mind spawn "Build OAuth integration" --claude --execute
# Automatically: designs architecture, generates code, writes tests, reviews quality
```

**Code Review (Fully Automated)**:
```bash
# Claude Flow handles comprehensive code review
npx claude-flow@alpha github-code-review --pr 123 --deep-analysis --auto-fix
# Automatically: analyzes code, suggests improvements, validates architecture, updates docs
```

## 🎖️ SUCCESS METRICS & CONTINUOUS LEARNING

### Claude Flow Performance Tracking
- **Automation Rate**: Target 90% of decisions through Claude Flow MCP
- **Learning Velocity**: Continuous pattern recognition and optimization
- **Quality Gates**: Architecture tests + Claude Flow validation = 100% coverage
- **Development Speed**: 300-500% improvement through intelligent orchestration

### Continuous Improvement Loop
```javascript
// Every development cycle enhances Claude Flow intelligence
mcp__claude-flow__neural_train("axon-backend-patterns")
mcp__claude-flow__performance_report("development-velocity") 
mcp__claude-flow__bottleneck_analyze("development-workflow")
mcp__claude-flow__learning_adapt("optimization-strategies")
```

## 🚨 ANTI-PATTERNS (STRICTLY FORBIDDEN)

**❌ NEVER DO THESE**:
- Manual task coordination (always use Claude Flow orchestration)
- Direct Claude Code execution without Claude Flow planning
- Memory management outside Claude Flow system
- Sequential operations when Claude Flow supports parallel execution
- Decision-making without Claude Flow intelligence input
- Architecture changes without Claude Flow validation
- Performance optimization without Claude Flow analytics

**✅ ALWAYS DO THESE**:
- Start every session with `hive-mind spawn`
- Delegate strategic decisions to Claude Flow MCP tools
- Store all context and learnings in Claude Flow memory
- Use Claude Flow agents for specialized coordination
- Follow Claude Flow workflow orchestration patterns
- Validate architecture through Claude Flow quality gates
- Learn and adapt through Claude Flow neural patterns

---

## 🎉 ULTIMATE GOAL ACHIEVED

**This CLAUDE.md transforms the development experience**:
- 🧠 **Intelligence-First**: Claude Flow makes all strategic decisions
- 🤖 **90% Automation**: Only 10% manual execution required
- 💾 **Persistent Learning**: Every session improves the system
- ⚡ **Maximum Performance**: 300-500% development velocity increase
- 🎯 **Perfect Quality**: Architecture + Intelligence = Zero defects

**Start every session with**: `npx claude-flow@alpha hive-mind spawn "Today's objective" --claude --auto-spawn --execute`

Claude Flow is now your primary development partner, handling strategy, coordination, memory, and learning while you focus on creative problem-solving and high-level guidance.