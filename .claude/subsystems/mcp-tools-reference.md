# 🛠️ MCP Tools Reference - Complete Catalog

**SUBSYSTEM PURPOSE**: Comprehensive reference for all 87 Claude Flow MCP tools with usage patterns, parameters, and advanced orchestration techniques based on actual capabilities research.

## 🏗️ CORE ORCHESTRATION TOOLS

### Session Management
```javascript
// Initialize swarm coordination system (no separate hive-mind init needed)
// Swarm coordination handles agent orchestration

// Initialize swarm coordination (primary orchestration method)
mcp__claude-flow__swarm_init({
  topology: "hierarchical" | "mesh" | "ring" | "star",
  maxAgents: 5-100,  // Based on actual research findings
  strategy: "balanced" | "specialized" | "adaptive"
})

// Get swarm status and metrics
mcp__claude-flow__swarm_status({
  verbose: true,
  includeMetrics: true,
  showAgentDetails: true
})
```

### Agent Management
```javascript
// Spawn specialized agents (use sparingly - prefer auto-spawning through task orchestration)
mcp__claude-flow__agent_spawn({
  type: "coordinator" | "researcher" | "coder" | "analyst" | "architect" | "tester" | "reviewer" | "optimizer" | "documenter" | "monitor" | "specialist",
  name: "Custom Agent Name",
  capabilities: ["strategic-planning", "risk-assessment", "performance-optimization"]
})

// PREFERRED: Auto-spawning through task orchestration
mcp__claude-flow__task_orchestrate({
  task: "Feature implementation",
  strategy: "adaptive",
  maxAgents: 4  // Auto-spawns needed agent types
})

// List all active agents
mcp__claude-flow__agent_list({
  swarmId: "specific-swarm",
  filter: "all" | "active" | "idle" | "busy"
})

// Get agent performance metrics
mcp__claude-flow__agent_metrics({
  agentId: "specific-agent",
  metric: "all" | "cpu" | "memory" | "tasks" | "performance"
})
```

## 🧠 NEURAL INTELLIGENCE SUITE

### Pattern Recognition & Learning
```javascript
// Train neural patterns
mcp__claude-flow__neural_train({
  pattern_type: "coordination" | "optimization" | "prediction",
  training_data: "comprehensive-dataset",
  epochs: 50-100,
  iterations: 10-100,
  agentId: "specific-agent-optional",
  continuous: true
})

// Analyze cognitive patterns
mcp__claude-flow__neural_patterns({
  action: "analyze" | "learn" | "predict",
  pattern: "all" | "convergent" | "divergent" | "lateral" | "systems" | "critical" | "abstract",
  operation: "development-workflow",
  outcome: "performance-metrics"
})

// Get neural status and performance
mcp__claude-flow__neural_status({
  agentId: "specific-agent-optional",
  modelId: "model-identifier"
})

// Make AI predictions
mcp__claude-flow__neural_predict({
  modelId: "trained-model-id",
  input: "prediction-data"
})
```

### Advanced Learning Systems
```javascript
// Cognitive behavior analysis
mcp__claude-flow__cognitive_analyze({
  behavior: "development-patterns" | "decision-making" | "problem-solving"
})

// Adaptive learning
mcp__claude-flow__learning_adapt({
  experience: {
    context: "development-session",
    outcomes: ["success", "challenges"],
    metrics: performanceData
  }
})

// Meta-learning across domains
mcp__claude-flow__meta_learning({
  sourceDomain: "axon-backend",
  targetDomain: "clean-architecture",
  transferMode: "adaptive" | "direct" | "gradual"
})

// Pattern recognition
mcp__claude-flow__pattern_recognize({
  data: codebaseAnalysisResults,
  patterns: ["architectural", "performance", "quality"]
})
```

## 🔄 WORKFLOW ORCHESTRATION SUITE

### Task Coordination
```javascript
// Orchestrate complex tasks
mcp__claude-flow__task_orchestrate({
  task: "Build comprehensive feature X",
  strategy: "parallel" | "sequential" | "adaptive" | "balanced",
  priority: "low" | "medium" | "high" | "critical",
  maxAgents: 10,
  dependencies: ["task-1", "task-2"]
})

// Check task status
mcp__claude-flow__task_status({
  taskId: "specific-task-id",
  detailed: true
})

// Get task results
mcp__claude-flow__task_results({
  taskId: "completed-task-id",
  format: "summary" | "detailed" | "raw"
})
```

### Workflow Management
```javascript
// Create custom workflows
mcp__claude-flow__workflow_create({
  name: "feature-development-pipeline",
  steps: [
    "requirements-analysis",
    "architecture-design",
    "implementation",
    "testing",
    "documentation",
    "deployment"
  ],
  triggers: ["git-push", "pr-creation"],
  dependencies: { "implementation": ["architecture-design"] }
})

// Execute workflows
mcp__claude-flow__workflow_execute({
  workflowId: "feature-development-pipeline",
  params: { 
    feature: "OAuth-integration",
    priority: "high"
  }
})

// Export workflow definitions
mcp__claude-flow__workflow_export({
  workflowId: "workflow-id",
  format: "json" | "yaml" | "mermaid"
})
```

## 💾 MEMORY MANAGEMENT SUITE

### Core Memory Operations
```javascript
// Store persistent memory
mcp__claude-flow__memory_usage({
  action: "store" | "retrieve" | "list" | "delete" | "search",
  key: "session/context",
  value: "comprehensive-session-data",
  namespace: "axon-backend" | "session-context" | "custom-namespace",
  ttl: 86400 // seconds
})

// Search memory with patterns
mcp__claude-flow__memory_search({
  pattern: "search-pattern",
  namespace: "specific-namespace",
  limit: 10
})

// Cross-session persistence
mcp__claude-flow__memory_persist({
  sessionId: "current-session-id"
})
```

### Advanced Memory Features
```javascript
// Namespace management
mcp__claude-flow__memory_namespace({
  action: "create" | "delete" | "list",
  namespace: "new-namespace"
})

// Memory backup and restore
mcp__claude-flow__memory_backup({
  path: "backup/location"
})

mcp__claude-flow__memory_restore({
  backupPath: "backup/location/file.backup"
})

// Memory compression and sync
mcp__claude-flow__memory_compress({
  namespace: "large-namespace"
})

mcp__claude-flow__memory_sync({
  target: "remote-instance"
})
```

## 📊 PERFORMANCE ANALYTICS SUITE

### Comprehensive Monitoring
```javascript
// Generate performance reports
mcp__claude-flow__performance_report({
  format: "summary" | "detailed" | "json",
  timeframe: "24h" | "7d" | "30d",
  components: ["swarm", "agents", "workflows", "memory"]
})

// Bottleneck analysis
mcp__claude-flow__bottleneck_analyze({
  component: "development-workflow" | "swarm" | "specific-agent",
  metrics: ["cpu", "memory", "response-time", "throughput"]
})

// Metrics collection
mcp__claude-flow__metrics_collect({
  components: ["all"] | ["swarm", "agents", "memory"],
  interval: 30 // seconds
})

// Trend analysis
mcp__claude-flow__trend_analysis({
  metric: "development-velocity" | "error-rate" | "performance",
  period: "1h" | "24h" | "7d" | "30d"
})
```

### Advanced Analytics
```javascript
// Cost analysis
mcp__claude-flow__cost_analysis({
  timeframe: "daily" | "weekly" | "monthly",
  breakdown: true
})

// Quality assessment
mcp__claude-flow__quality_assess({
  target: "codebase" | "architecture" | "workflows",
  criteria: ["all"] | ["performance", "security", "maintainability"]
})

// Usage statistics
mcp__claude-flow__usage_stats({
  component: "specific-component"
})

// System health monitoring
mcp__claude-flow__health_check({
  components: ["all"] | ["swarm", "agents", "memory", "workflows"]
})
```

## 🐙 GITHUB INTEGRATION SUITE

### Repository Management
```javascript
// Comprehensive repository analysis
mcp__claude-flow__github_repo_analyze({
  repo: "axon-backend",
  analysis_type: "code_quality" | "performance" | "security" | "comprehensive"
})

// Pull request management
mcp__claude-flow__github_pr_manage({
  repo: "axon-backend",
  action: "review" | "merge" | "close",
  pr_number: 123,
  auto_fix: true
})

// Issue tracking and triage
mcp__claude-flow__github_issue_track({
  repo: "axon-backend",
  action: "create" | "update" | "close" | "triage"
})
```

### Advanced GitHub Operations
```javascript
// Automated code review
mcp__claude-flow__github_code_review({
  repo: "axon-backend",
  pr: 123,
  deep_analysis: true,
  auto_fix_violations: true,
  security_scan: true
})

// Workflow automation
mcp__claude-flow__github_workflow_auto({
  repo: "axon-backend",
  workflow: {
    name: "ci-cd-pipeline",
    triggers: ["push", "pr"],
    jobs: ["build", "test", "deploy"]
  }
})

// Release coordination
mcp__claude-flow__github_release_coord({
  repo: "axon-backend",
  version: "v2.0.0",
  auto_changelog: true
})

// Repository metrics
mcp__claude-flow__github_metrics({
  repo: "axon-backend"
})
```

## 🤖 DECENTRALIZED AUTONOMOUS AGENTS (DAA)

### Agent Creation & Management
```javascript
// Create autonomous agents
mcp__claude-flow__daa_agent_create({
  id: "unique-agent-identifier",
  agent_type: "full-stack-developer" | "security-specialist" | "performance-optimizer",
  capabilities: ["coding", "testing", "documentation", "security-analysis"],
  cognitivePattern: "convergent" | "divergent" | "lateral" | "systems" | "critical" | "adaptive",
  enableMemory: true,
  learningRate: 0.1 // 0-1
})

// Agent adaptation
mcp__claude-flow__daa_agent_adapt({
  agent_id: "agent-identifier",
  feedback: "Performance feedback message",
  performanceScore: 0.85, // 0-1
  suggestions: ["improve-error-handling", "optimize-performance"]
})

// Agent lifecycle management
mcp__claude-flow__daa_lifecycle_manage({
  agentId: "agent-identifier",
  action: "start" | "pause" | "resume" | "terminate"
})
```

### Advanced DAA Features
```javascript
// Knowledge sharing between agents
mcp__claude-flow__daa_knowledge_share({
  source_agent: "architect-agent",
  target_agents: ["coder-agent", "tester-agent"],
  knowledgeDomain: "clean-architecture-patterns",
  knowledgeContent: {
    patterns: ["cqrs", "ddd", "hexagonal"],
    bestPractices: ["separation-of-concerns", "dependency-inversion"]
  }
})

// Capability matching
mcp__claude-flow__daa_capability_match({
  task_requirements: ["c#-development", "clean-architecture", "testing"],
  available_agents: ["agent-1", "agent-2", "agent-3"]
})

// Learning status monitoring
mcp__claude-flow__daa_learning_status({
  agentId: "specific-agent",
  detailed: true
})

// Performance metrics
mcp__claude-flow__daa_performance_metrics({
  category: "all" | "system" | "performance" | "efficiency" | "neural",
  timeRange: "1h" | "24h" | "7d"
})
```

## 🛡️ SECURITY & QUALITY SUITE

### Security Analysis
```javascript
// Comprehensive security scanning
mcp__claude-flow__security_scan({
  target: "codebase" | "dependencies" | "infrastructure",
  depth: "basic" | "comprehensive" | "deep",
  auto_fix: true
})

// Error pattern analysis
mcp__claude-flow__error_analysis({
  logs: systemLogs,
  timeframe: "24h",
  patterns: ["security", "performance", "reliability"]
})

// System diagnostics
mcp__claude-flow__diagnostic_run({
  components: ["all"] | ["security", "performance", "reliability"]
})
```

## 🚀 AUTOMATION & DEPLOYMENT

### CI/CD Pipeline Management
```javascript
// Create deployment pipelines
mcp__claude-flow__pipeline_create({
  config: {
    name: "axon-backend-pipeline",
    stages: ["build", "test", "security-scan", "deploy"],
    triggers: ["git-push", "pr-merge"],
    environments: ["dev", "staging", "production"]
  }
})

// Batch processing
mcp__claude-flow__batch_process({
  items: ["task-1", "task-2", "task-3"],
  operation: "parallel-execute",
  maxConcurrency: 5
})

// Parallel execution
mcp__claude-flow__parallel_execute({
  tasks: [
    { name: "build", command: "dotnet build" },
    { name: "test", command: "dotnet test" },
    { name: "security-scan", command: "dotnet security-scan" }
  ]
})
```

## 🎯 USAGE PATTERNS & BEST PRACTICES

### Session Initialization Pattern
```javascript
// Complete ecosystem activation
const initializeSession = async () => {
  // 1. Initialize hive mind
  await mcp__claude-flow__hive_mind_init({ advanced: true, maxAgents: 20 })
  
  // 2. Setup swarm topology
  await mcp__claude-flow__swarm_init({ topology: "hierarchical", strategy: "adaptive" })
  
  // 3. Spawn core agents
  await mcp__claude-flow__agent_spawn({ type: "coordinator", name: "Session Manager" })
  await mcp__claude-flow__agent_spawn({ type: "architect", name: "System Architect" })
  
  // 4. Initialize neural learning
  await mcp__claude-flow__neural_train({ pattern_type: "coordination", continuous: true })
  
  // 5. Store session context
  await mcp__claude-flow__memory_usage({ action: "store", key: "session/init", value: Date.now() })
}
```

### Feature Development Pattern
```javascript
// Complete feature lifecycle
const developFeature = async (featureName) => {
  // 1. Orchestrate development task
  const taskId = await mcp__claude-flow__task_orchestrate({
    task: `Implement ${featureName}`,
    strategy: "adaptive",
    priority: "high"
  })
  
  // 2. Create specialized workflow
  await mcp__claude-flow__workflow_create({
    name: `${featureName}-workflow`,
    steps: ["analysis", "design", "implement", "test", "document"]
  })
  
  // 3. Execute with monitoring
  await mcp__claude-flow__workflow_execute({ workflowId: `${featureName}-workflow` })
  
  // 4. Track performance
  await mcp__claude-flow__performance_report({ format: "detailed" })
}
```

---

**SUBSYSTEM ACTIVATION**: This reference loads automatically when MCP tools are needed. All 87 tools support advanced parameters and can be chained for complex orchestration patterns. Based on actual Claude Flow research from official documentation.