# 🤖 Agent Delegation Matrix

## PRIMARY AGENTS
| Agent | Expertise | Trigger | Use Case |
|-------|-----------|---------|----------|
| SPARC-Master | 9.9/10 | Full development cycle | Features, TDD workflows |
| Axon-Architect | 9.7/10 | System design | Architecture, documentation |
| Axon-Orchestrator | 9.5/10 | Coordination | Multi-agent workflows |

## DELEGATION DECISION TREE
```
Task Type?
├── Feature Development → SPARC-Master (+ Serena for code ops)
├── Architecture/Design → Axon-Architect (+ Serena for analysis)
├── Multi-Agent Coord → Axon-Orchestrator (+ Claude Flow)
├── Code Operations → Use Serena MCP Tools FIRST
├── File Operations → Use Desktop Commander MCP
└── Simple Execution → Claude Code (+ MCP where applicable)
```

## 🔧 MCP TOOL PRIORITY MATRIX
```
Operation Type → Primary MCP Tool → Secondary Tool
├── Code Analysis → Serena (find_symbol, search_pattern) → Desktop Commander
├── Code Editing → Serena (replace_symbol_body, regex) → Desktop Commander  
├── Agent Coordination → Claude Flow (swarm_init, orchestrate) → Task
├── Memory/State → Claude Flow (memory_usage) → Write/Read
├── File Operations → Desktop Commander → Write/Read
└── Complex Workflows → Claude Flow + Serena → Task + Edit
```

## CONTEXT HANDOFF PATTERNS
Each agent receives: Previous context + Current objective + Success criteria

## AGENT ACTIVATION PATTERNS
```javascript
// Feature Development (MCP-Enhanced)
Task("SPARC-Master: Implement [feature] using complete SPARC methodology with Serena for code operations, Claude Flow for memory, and human validation gates")

// Architecture Design (MCP-Enhanced)  
Task("Axon-Architect: Design [component] with Clean Architecture compliance using Serena for code analysis and documentation updates")

// System Coordination (MCP-Enhanced)
Task("Axon-Orchestrator: Coordinate [workflow] using Claude Flow orchestration, ML optimization, and performance monitoring")
```

## 🔧 MCP INTEGRATION REQUIREMENTS
**ALL AGENTS MUST:**
- Use Serena for ALL code analysis and editing operations
- Use Claude Flow for agent coordination and memory management  
- Use Desktop Commander for non-code file operations
- Prioritize concurrent MCP operations over sequential manual operations

## PERFORMANCE TARGETS
- **Agent Utilization**: 90%+ (vs 20% current)
- **Task Delegation**: Auto-delegate tasks >30 seconds
- **Context Persistence**: All agent results stored in MCP memory
- **Orchestration Speed**: <100ms agent activation