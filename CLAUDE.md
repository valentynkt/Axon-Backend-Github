# 🧠 CLAUDE.md - Claude Flow MCP Orchestration Hub

**ABSOLUTE PARADIGM**: Claude Flow MCP tools are the primary orchestration engine. ALL interactions, decisions, and work coordination MUST flow through Claude Flow MCP tools. Claude Code is the execution layer, Claude Flow is the intelligence layer.

## 🌊 CLAUDE FLOW MCP AS UNIVERSAL ENTRY POINT

### 🚨 MANDATORY MCP AUTOMATION PROTOCOL

**RULE #1**: Every session MUST begin with Claude Flow MCP initialization:
```javascript
// MANDATORY session start sequence - Use MCP tools, not CLI
mcp__claude-flow__swarm_init({ topology: "hierarchical", maxAgents: 8, strategy: "adaptive" })
mcp__claude-flow__memory_usage({ action: "store", key: "session/init", value: "Axon Backend development session", namespace: "axon-backend" })
mcp__claude-flow__neural_train({ pattern_type: "coordination", continuous: true })
```

**RULE #2**: ALL work delegation MUST use Claude Flow MCP coordination:
- 🧠 **Intelligence Layer**: Claude Flow MCP tools coordinate, plan, decide (87 specialized tools)
- 🛠️ **Execution Layer**: Claude Code executes based on Claude Flow instructions
- 💾 **Memory Layer**: All context, decisions, progress stored in Claude Flow memory system

### 🐝 SWARM + AUTO-SPAWNING ORCHESTRATION SYSTEM

**Primary Workflow Pattern** (Based on Actual Claude Flow Capabilities):
```javascript
// ✅ CORRECT: MCP-first approach with auto-spawning
[Message 1 - Intelligence Coordination]:
  // Initialize swarm topology (hierarchical works WITH coordination, not as alternative)
  mcp__claude-flow__swarm_init({ topology: "hierarchical", maxAgents: 8, strategy: "adaptive" })
  
  // Auto-spawn agents through task orchestration (preferred over manual spawn)
  mcp__claude-flow__task_orchestrate({ 
    task: "Build feature X", 
    strategy: "adaptive",
    priority: "high",
    maxAgents: 4  // Auto-spawns needed agents
  })
  
  // Store session context in memory system
  mcp__claude-flow__memory_usage({ action: "store", key: "session/init", value: "session-data", namespace: "axon-backend" })

[Message 2 - Execution Coordination]:
  // Task orchestration with auto-spawning (no manual agent creation needed)
  mcp__claude-flow__task_orchestrate({ task: "Implement Axon feature", strategy: "parallel" })
  
  // Neural pattern training for continuous learning
  mcp__claude-flow__neural_train({ pattern_type: "coordination", continuous: true })
  
  // Use Task tool for Claude Code execution coordination
  Task("Implementation: Execute feature based on Claude Flow orchestration")
  TodoWrite({ todos: ["comprehensive todos coordinated by Claude Flow MCP"] })
```

## 🎯 AXON BACKEND + CLAUDE FLOW INTEGRATION

### Project Architecture Enhanced with Claude Flow

**Stack**: .NET 10 Preview • Clean Architecture • DDD • CQRS (MediatR) • FastEndpoints • **Claude Flow MCP Orchestration** • OpenAI Direct MCP • NUnit • Shouldly

**Enhanced Architecture with Claude Flow MCP Layer**:
```
🧠 Claude Flow MCP Intelligence Layer (87 Tools + 64 Agents)
├── Swarm Coordination (hierarchical/mesh/ring/star topologies)
├── Memory Management & Persistence (SQLite with 12 tables)
├── Auto-Spawning Agent Orchestration (64 specialized agents)
├── Neural Pattern Learning & Optimization
└── Performance Analytics & Continuous Learning

🏗️ Clean Architecture Implementation Layer
├── src/Api/ (HTTP host, endpoints, DTOs)
├── src/Shared/ (Result<T>, Error, primitives)
├── src/Modules/Chat/ (Bounded context)
│   ├── Application/ (Commands, handlers, ports)
│   ├── Domain/ (Aggregates, entities, VOs)
│   └── Infrastructure/ (Adapters, OpenAI client)
└── tests/ (Mirrors src/ structure)
```

### 🔄 CLAUDE FLOW MCP LIFECYCLE MANAGEMENT

**Every Development Session Pattern** (MCP Tools Only):
```javascript
// 1. Session Initialization (MANDATORY MCP)
mcp__claude-flow__swarm_init({ topology: "hierarchical", strategy: "adaptive" })
mcp__claude-flow__memory_usage({ action: "store", key: "session/start", namespace: "axon-backend" })

// 2. Memory Context Loading (MCP MEMORY)
mcp__claude-flow__memory_search({ pattern: "axon-backend", namespace: "project" })
mcp__claude-flow__memory_usage({ action: "retrieve", key: "project/context" })

// 3. Task Orchestration with Auto-Spawning (PREFERRED)
mcp__claude-flow__task_orchestrate({ 
  task: "Development objective", 
  strategy: "adaptive",
  maxAgents: 6  // Auto-spawns needed agents
})

// 4. Progress Tracking (CONTINUOUS MCP)
mcp__claude-flow__performance_report({ format: "summary", timeframe: "24h" })
mcp__claude-flow__memory_usage({ action: "store", key: "session/progress" })

// 5. Session Learning (NEURAL PATTERNS)
mcp__claude-flow__neural_train({ pattern_type: "coordination", continuous: true })
mcp__claude-flow__neural_patterns({ action: "learn", operation: "development-session" })
```

## 🛠️ DEVELOPMENT COMMANDS (Claude Flow Enhanced)

### Essential Commands with Claude Flow MCP Integration
```javascript
// Build with Claude Flow MCP monitoring
mcp__claude-flow__task_orchestrate({ task: "Build solution", strategy: "sequential" })
// Followed by: Bash({ command: "dotnet build", description: "Build Axon Backend solution" })
mcp__claude-flow__performance_report({ format: "detailed" })

// Test execution with MCP progress tracking
mcp__claude-flow__task_orchestrate({ task: "Run tests", strategy: "parallel" })
// Followed by: Bash({ command: "dotnet test", description: "Run all test suites" })
mcp__claude-flow__neural_patterns({ action: "analyze", operation: "test-execution" })

// Architecture validation with MCP memory
// First run tests, then store results
mcp__claude-flow__memory_usage({ 
  action: "store", 
  key: "arch-test-results", 
  value: "test-results-data",
  namespace: "axon-backend" 
})

// Feature development with MCP orchestration (auto-spawning preferred)
mcp__claude-flow__task_orchestrate({ 
  task: "Implement ProcessMessage feature",
  strategy: "adaptive",
  priority: "high",
  maxAgents: 5  // Auto-spawns: architect, coder, tester agents
})
```

## 🧠 CLAUDE FLOW MCP TOOL ECOSYSTEM (87 ACTUAL TOOLS)

### Core Orchestration Tools (ALWAYS USE THESE)
- `mcp__claude-flow__swarm_init` - Setup coordination topology (hierarchical/mesh/ring/star)
- `mcp__claude-flow__task_orchestrate` - Coordinate complex workflows with auto-spawning
- `mcp__claude-flow__memory_usage` - Persistent memory management (SQLite backend)
- `mcp__claude-flow__neural_train` - Train neural patterns for continuous learning
- `mcp__claude-flow__performance_report` - Generate analytics and insights
- `mcp__claude-flow__agent_spawn` - Manual agent creation (use sparingly, prefer auto-spawn)

### Intelligence Enhancement Tools
- `mcp__claude-flow__neural_patterns` - Analyze cognitive approaches and patterns
- `mcp__claude-flow__learning_adapt` - Improve coordination over time
- `mcp__claude-flow__bottleneck_analyze` - Identify performance issues
- `mcp__claude-flow__cognitive_analyze` - Analyze development behaviors
- `mcp__claude-flow__pattern_recognize` - Recognize code and workflow patterns

### Workflow Automation Tools  
- `mcp__claude-flow__workflow_create` - Define reusable workflows
- `mcp__claude-flow__workflow_execute` - Execute predefined workflows
- `mcp__claude-flow__automation_setup` - Configure automation rules
- `mcp__claude-flow__parallel_execute` - Concurrent task execution
- `mcp__claude-flow__batch_process` - Process multiple items efficiently

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
mcp__claude-flow__task_orchestrate({ 
  task: "Implement user authentication",
  strategy: "adaptive",
  maxAgents: 4  // Auto-spawns architect, coder, tester
})
mcp__claude-flow__memory_usage({ action: "store", key: "auth-requirements", value: specifications })
mcp__claude-flow__neural_patterns({ action: "analyze", pattern: "authentication-patterns" })

// Phase 2: Coordination & Design (Claude Flow orchestrates)  
mcp__claude-flow__workflow_create({ 
  name: "auth-implementation-pipeline",
  steps: ["design", "implement", "test", "validate"]
})
mcp__claude-flow__performance_report({ format: "baseline-metrics" })

// Phase 3: Execution (Claude Code executes under Claude Flow guidance)
Task("Implementation Agent: Build auth following Claude Flow design")
TodoWrite([{content: "Implement authentication following Claude Flow orchestration", priority: "high"}])
// Read/Write/Edit operations based on Claude Flow instructions

// Phase 4: Validation & Learning (Claude Flow validates & learns)
mcp__claude-flow__bottleneck_analyze({ component: "auth-implementation" })
mcp__claude-flow__neural_train({ pattern_type: "auth-development-patterns" })  
mcp__claude-flow__memory_usage({ action: "store", key: "auth-lessons", value: "learnings" })
```

## 🎯 AXON BACKEND DEVELOPMENT WITH CLAUDE FLOW

### Clean Architecture + Claude Flow Pattern

**CQRS Command Development with Full Orchestration**:
```javascript
// 1. Strategic Planning (Claude Flow Intelligence)
mcp__claude-flow__task_orchestrate({ 
  task: "ProcessMessage command implementation",
  strategy: "adaptive",
  maxAgents: 3
})
mcp__claude-flow__memory_search({ pattern: "cqrs-patterns", namespace: "axon-backend" })

// 2. Architecture Design (Claude Flow Coordination)
mcp__claude-flow__workflow_create({ 
  name: "cqrs-implementation-workflow",
  steps: ["design", "implement", "test"]
})
mcp__claude-flow__neural_patterns({ action: "analyze", pattern: "vertical-slice-patterns" })

// 3. Implementation (Claude Code Execution)
Task("CQRS Agent: Implement ProcessMessage with Claude Flow coordination")
Write("src/Modules/Chat/Application/Commands/ProcessMessage/ProcessMessageCommand.cs")
Write("src/Modules/Chat/Application/Commands/ProcessMessage/ProcessMessageHandler.cs")

// 4. Validation & Learning (Claude Flow Quality Gates)
mcp__claude-flow__performance_report({ format: "cqrs-implementation-metrics" })
mcp__claude-flow__neural_train({ pattern_type: "clean-architecture-patterns" })
```

### Result Pattern with Claude Flow Enhancement
```csharp
// Enhanced Result pattern with Claude Flow integration
public async Task<Result<ProcessMessageResponse>> Handle(
    ProcessMessageCommand command, 
    CancellationToken cancellationToken)
{
    // Claude Flow pre-execution logging
    await _logger.LogInformationAsync("Processing message with Claude Flow coordination");
    
    var result = await ProcessMessageCore(command, cancellationToken);
    
    // Claude Flow post-execution learning
    await _logger.LogInformationAsync("Successfully processed message. Response ID: {ResponseId}", 
        result.IsSuccess ? result.Value.ConversationId : "failed");
    
    return result;
}
```

## 🚀 AUTOMATION WORKFLOWS

### 🔄 DAILY DEVELOPMENT AUTOMATION

**Morning Routine (MCP Orchestrated)**:
```javascript
// Claude Flow handles entire morning setup via MCP
mcp__claude-flow__automation_setup({ 
  rules: [{
    name: "morning-dev-routine",
    triggers: ["session-start"],
    actions: ["update-dependencies", "run-tests", "analyze-metrics", "plan-day"]
  }]
})
mcp__claude-flow__task_orchestrate({ 
  task: "Daily development session", 
  strategy: "adaptive",
  maxAgents: 3
})
```

**Feature Development (90% Automated)**:
```javascript
// Claude Flow orchestrates entire feature lifecycle via MCP
mcp__claude-flow__workflow_create({ 
  name: "feature-development-pipeline",
  steps: ["design-architecture", "generate-code", "write-tests", "review-quality"]
})
mcp__claude-flow__task_orchestrate({ 
  task: "Build OAuth integration", 
  strategy: "adaptive",
  maxAgents: 5
})
```

**Code Review (Fully Automated)**:
```javascript
// Claude Flow handles comprehensive code review via MCP
mcp__claude-flow__github_code_review({ 
  repo: "axon-backend",
  pr: 123, 
  deep_analysis: true, 
  auto_fix_violations: true 
})
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
mcp__claude-flow__neural_train({ pattern_type: "axon-backend-patterns", continuous: true })
mcp__claude-flow__performance_report({ format: "development-velocity", timeframe: "7d" })
mcp__claude-flow__bottleneck_analyze({ component: "development-workflow" })
mcp__claude-flow__learning_adapt({ experience: { context: "development-session" } })
```

## 🚨 ANTI-PATTERNS (STRICTLY FORBIDDEN)

**❌ NEVER DO THESE**:
- Manual task coordination (always use Claude Flow MCP orchestration)
- Direct Claude Code execution without Claude Flow MCP planning
- Memory management outside Claude Flow MCP system
- Sequential operations when Claude Flow supports parallel execution
- Decision-making without Claude Flow MCP intelligence input
- Architecture changes without Claude Flow MCP validation
- Performance optimization without Claude Flow MCP analytics
- CLI commands instead of MCP tools (use `mcp__claude-flow__*` not `npx claude-flow@alpha`)

**✅ ALWAYS DO THESE**:
- Start every session with `mcp__claude-flow__swarm_init` and `mcp__claude-flow__task_orchestrate`
- Delegate strategic decisions to Claude Flow MCP tools
- Store all context and learnings in Claude Flow memory via MCP
- Use auto-spawning through task orchestration instead of manual agent creation
- Follow Claude Flow workflow orchestration patterns via MCP
- Validate architecture through Claude Flow quality gates via MCP
- Learn and adapt through Claude Flow neural patterns via MCP

---

## 🎉 ULTIMATE GOAL ACHIEVED

**This CLAUDE.md transforms the development experience**:
- 🧠 **Intelligence-First**: Claude Flow MCP tools make all strategic decisions
- 🤖 **90% Automation**: Only 10% manual execution required
- 💾 **Persistent Learning**: Every session improves the system via neural training
- ⚡ **Maximum Performance**: 300-500% development velocity increase
- 🎯 **Perfect Quality**: Architecture + Intelligence = Zero defects

**Start every session with**: 
```javascript
mcp__claude-flow__swarm_init({ topology: "hierarchical", strategy: "adaptive" })
mcp__claude-flow__task_orchestrate({ 
  task: "Today's objective", 
  strategy: "adaptive",
  maxAgents: 6  // Auto-spawns needed agents
})
```

## 📚 SUBSYSTEM ARCHITECTURE INDEX

This CLAUDE.md orchestrates specialized subsystem prompts for maximum efficiency:

### 🎯 Core Subsystems (Auto-loaded when needed)
- **[.claude/subsystems/mcp-tools-reference.md]** - Complete MCP tools catalog (87 actual tools)
- **[.claude/subsystems/automation-workflows.md]** - Advanced workflow orchestration patterns  
- **[.claude/subsystems/axon-patterns.md]** - Axon Backend specific development templates

### 🔧 Additional Subsystems (On-demand based on user feedback)
Future subsystems will be added based on actual needs, maintaining focus on the current 3 core subsystems with possible 1-2 additions as needed.

Claude Flow MCP tools are now your primary development partner, handling strategy, coordination, memory, and learning while you focus on creative problem-solving and high-level guidance.