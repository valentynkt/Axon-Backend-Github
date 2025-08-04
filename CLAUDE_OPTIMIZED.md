# 🧠 CLAUDE.md - MCP Agent Orchestration Hub

**CORE PRINCIPLE**: MCP agents handle strategy, Claude executes. Delegate everything to specialized sub-agents.

## 🎯 ORCHESTRATION PRIORITIES
1. **Always MCP First**: `mcp__claude-flow__swarm_init` → `mcp__claude-flow__task_orchestrate`
2. **Agent Delegation**: Route all work to specialized agents
3. **Concurrent Operations**: Batch all operations in single messages
4. **Memory Persistence**: Store all context via MCP memory system

## 🤖 AGENT DELEGATION MATRIX

### Primary Orchestration Agents
- **SPARC-Master** (9.9/10): Complete feature development with human approval gates
- **Axon-Orchestrator** (9.5/10): Multi-agent coordination and topology optimization  
- **Axon-Architect** (9.7/10): System architecture and Clean Architecture compliance

### Delegation Patterns
```javascript
// ✅ CORRECT: Orchestration-focused approach
[Single Message]:
  mcp__claude-flow__swarm_init({ topology: "hierarchical", maxAgents: 8 })
  mcp__claude-flow__task_orchestrate({ task: "Build feature X", strategy: "adaptive" })
  Task("SPARC-Master: Execute complete SPARC methodology for feature X")
  TodoWrite([{...all tasks in one call...}])
```

## 🏗️ PROJECT CONTEXT: AXON BACKEND
**Stack**: .NET 10 • Clean Architecture • CQRS/MediatR • FastEndpoints • NUnit/Shouldly

**Build Commands**:
```bash
dotnet build          # Build solution
dotnet test           # Run tests  
dotnet run --project src/Api  # Run API
```

## 📋 AGENT ACTIVATION PATTERNS

### For Feature Development
```javascript
Task("SPARC-Master: Implement [feature] using complete SPARC methodology with human validation gates")
```

### For Architecture Work  
```javascript
Task("Axon-Architect: Design Clean Architecture solution for [requirement] with documentation")
```

### For System Coordination
```javascript
Task("Axon-Orchestrator: Coordinate multi-agent workflow for [objective] with ML optimization")
```

## 🚀 SESSION INITIALIZATION
**Every session starts with**:
```javascript
mcp__claude-flow__swarm_init({ topology: "hierarchical", strategy: "adaptive" })
mcp__claude-flow__task_orchestrate({ task: "[session objective]", maxAgents: 6 })
```

## 🛡️ ANTI-PATTERNS
❌ Never do manual work that agents can handle  
❌ Never use sequential operations when parallel possible  
❌ Never skip MCP initialization  
❌ Never bypass agent delegation for complex tasks

**Agent Reference**: `.claude/agents/` contains detailed agent specifications.