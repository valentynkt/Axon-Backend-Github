# 🤖 Multi-Agent Coordination Pattern

## USAGE
When coordinating complex workflows requiring multiple specialized agents working together.

## ORCHESTRATION TEMPLATE
```javascript
// Single Message - Multi-Agent Workflow Coordination
[Session Initialization]:
  mcp__claude-flow__swarm_init({ 
    topology: "mesh", // Use mesh for complex multi-agent coordination
    maxAgents: 10, 
    strategy: "adaptive" 
  })
  mcp__claude-flow__task_orchestrate({ 
    task: "Coordinate [COMPLEX_WORKFLOW] across multiple specialized agents", 
    strategy: "parallel",
    priority: "high",
    maxAgents: 8
  })

[Agent Delegation & Coordination with MCP Tools]:
  Task("Axon-Orchestrator: Coordinate complex workflow [WORKFLOW_NAME] using Claude Flow orchestration, ML optimization, performance monitoring, and intelligent agent coordination")
  
[Supporting Agents Activation]:
  Task("SPARC-Master: Handle development phases requiring SPARC methodology")
  Task("Axon-Architect: Handle architecture decisions and design validation")

[Performance Monitoring]:
  mcp__claude-flow__neural_train({ pattern_type: "coordination", continuous: true })
  mcp__claude-flow__bottleneck_analyze({ component: "multi-agent-workflow" })
  mcp__claude-flow__performance_report({ format: "coordination-metrics" })

[Context Management]:
  TodoWrite([{
    content: "Initialize multi-agent coordination framework", 
    status: "pending", priority: "high"
  }, {
    content: "Coordinate agent task distribution and dependencies", 
    status: "pending", priority: "high"
  }, {
    content: "Monitor inter-agent communication and handoffs", 
    status: "pending", priority: "medium"
  }, {
    content: "Optimize coordination patterns based on performance", 
    status: "pending", priority: "medium"
  }])

[Memory & Results]:
  mcp__claude-flow__memory_usage({ 
    action: "store", 
    key: "coordination-[WORKFLOW_NAME]", 
    value: "Multi-agent coordination session with performance optimization",
    namespace: "axon-backend" 
  })
```

## EXPECTED OUTCOMES
- Optimized agent coordination
- Parallel task execution
- Performance monitoring
- Intelligent workflow adaptation