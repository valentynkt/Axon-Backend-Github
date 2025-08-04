# 🧠 CLAUDE.md - MCP Orchestration Hub

## CORE PRINCIPLE
MCP agents handle ALL strategy. Claude executes only. Always delegate.

## SESSION START (MANDATORY)
```javascript
mcp__claude-flow__swarm_init({ topology: "hierarchical" })
mcp__claude-flow__task_orchestrate({ task: "[objective]" })
```

## DELEGATION PATTERNS
- **Features**: `Task("SPARC-Master: [feature] with SPARC methodology")`
- **Architecture**: `Task("Axon-Architect: [design] with Clean Architecture")`  
- **Coordination**: `Task("Axon-Orchestrator: [workflow] optimization")`

## PROJECT: AXON BACKEND
**.NET 10 • Clean Architecture • CQRS • FastEndpoints**
```bash
dotnet build && dotnet test
```

## REFERENCES
- Agents: `.claude/agents/MATRIX.md`
- Tools: `.claude/tools/QUICK_REF.md`
- Patterns: `.claude/patterns/`

**Rule**: If task > 30 seconds → delegate to agent