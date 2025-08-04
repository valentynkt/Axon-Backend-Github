# 🏗️ Architecture Review Pattern

## USAGE
When reviewing system architecture for Clean Architecture compliance and optimization opportunities.

## ORCHESTRATION TEMPLATE
```javascript
// Single Message - Architecture Review & Optimization
[Session Initialization]:
  mcp__claude-flow__swarm_init({ topology: "hierarchical", maxAgents: 6 })
  mcp__claude-flow__task_orchestrate({ 
    task: "Review [COMPONENT] architecture for compliance and optimization", 
    strategy: "adaptive"
  })

[Agent Delegation with MCP Tools]:
  Task("Axon-Architect: Review [COMPONENT] architecture using Serena for code structure analysis, Clean Architecture compliance validation, and documentation updates")

[Analysis & Monitoring]:
  mcp__claude-flow__bottleneck_analyze({ component: "[COMPONENT]-architecture" })
  mcp__claude-flow__performance_report({ format: "architecture-health", timeframe: "current" })
  mcp__claude-flow__neural_patterns({ action: "analyze", pattern: "architecture-compliance" })

[Context Management]:
  TodoWrite([{
    content: "Analyze current architecture state and compliance", 
    status: "pending", priority: "high"
  }, {
    content: "Identify Clean Architecture violations and fixes", 
    status: "pending", priority: "high"
  }, {
    content: "Generate optimization recommendations", 
    status: "pending", priority: "medium"
  }, {
    content: "Create architecture documentation updates", 
    status: "pending", priority: "medium"
  }])

[Memory Persistence]:
  mcp__claude-flow__memory_usage({ 
    action: "store", 
    key: "architecture-review-[COMPONENT]", 
    value: "Architecture review session with compliance analysis",
    namespace: "axon-backend" 
  })
```

## EXPECTED OUTCOMES
- Architecture compliance assessment
- Optimization recommendations
- Updated documentation
- Performance improvement plan