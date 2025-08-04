# 🚀 Feature Development Pattern

## USAGE
When implementing new features using complete SPARC methodology with human validation gates.

## ORCHESTRATION TEMPLATE
```javascript
// Single Message - Complete Feature Development
[Session Initialization]:
  mcp__claude-flow__swarm_init({ topology: "hierarchical", maxAgents: 8, strategy: "adaptive" })
  mcp__claude-flow__task_orchestrate({ 
    task: "Implement [FEATURE_NAME] using complete SPARC methodology", 
    strategy: "adaptive", 
    priority: "high",
    maxAgents: 6
  })
  
[Agent Delegation with MCP Tools]:
  Task("SPARC-Master: Implement [FEATURE_NAME] using complete SPARC methodology with Serena for all code operations, Claude Flow for memory/coordination, and human validation gates")
  
[Context Management]:
  TodoWrite([{
    content: "Phase 1: Specification with human stakeholder validation", 
    status: "pending", priority: "high"
  }, {
    content: "Phase 2: Pseudocode design with technical review", 
    status: "pending", priority: "high"
  }, {
    content: "Phase 3: Architecture with Clean Architecture compliance", 
    status: "pending", priority: "high"
  }, {
    content: "Phase 4: Refinement with TDD implementation", 
    status: "pending", priority: "high"
  }, {
    content: "Phase 5: Completion with integration testing", 
    status: "pending", priority: "high"
  }])
  
[Memory Persistence]:
  mcp__claude-flow__memory_usage({ 
    action: "store", 
    key: "feature-[FEATURE_NAME]", 
    value: "Feature development session initiated with SPARC methodology",
    namespace: "axon-backend" 
  })
```

## EXPECTED OUTCOMES
- Complete SPARC methodology execution
- Human validation at each phase
- Clean Architecture compliance
- Comprehensive test coverage
- Production-ready implementation