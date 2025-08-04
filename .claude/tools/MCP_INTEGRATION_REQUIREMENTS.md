# 🛠️ MCP TOOL INTEGRATION REQUIREMENTS

## 🎯 MANDATORY MCP TOOL USAGE FOR ALL AGENTS

### 📋 CORE PRINCIPLE
**ALL agents MUST prioritize MCP tools over manual operations for maximum efficiency and consistency.**

## 🔧 PRIMARY MCP TOOL CATEGORIES

### 1. **SERENA (Code Operations) - HIGHEST PRIORITY**
```yaml
Purpose: All code analysis, editing, and repository operations
When to Use: ANY code-related task
Tools:
  - mcp__serena__find_symbol: Find classes, methods, functions
  - mcp__serena__search_for_pattern: Search code patterns
  - mcp__serena__replace_symbol_body: Replace entire functions/classes
  - mcp__serena__replace_regex: Targeted code replacements
  - mcp__serena__insert_after_symbol: Add new code after existing
  - mcp__serena__insert_before_symbol: Add new code before existing
  - mcp__serena__find_referencing_symbols: Find usage of symbols
  - mcp__serena__get_symbols_overview: Understand file structure
```

### 2. **CLAUDE FLOW (Orchestration & Memory)**
```yaml
Purpose: Agent coordination, memory, and workflow management
When to Use: Multi-agent tasks, state management, complex workflows
Tools:
  - mcp__claude_flow__swarm_init: Initialize agent coordination
  - mcp__claude_flow__task_orchestrate: Coordinate complex tasks
  - mcp__claude_flow__memory_usage: Store/retrieve persistent data
  - mcp__claude_flow__agent_spawn: Create specialized agents
  - mcp__claude_flow__neural_train: Learn from patterns
```

### 3. **DESKTOP COMMANDER (File System Operations)**
```yaml
Purpose: File operations when Serena cannot be used
When to Use: Non-code files, basic file operations
Tools:
  - mcp__desktop_commander__read_file: Read any file type
  - mcp__desktop_commander__write_file: Write files (use chunking)
  - mcp__desktop_commander__edit_block: Targeted file edits
  - mcp__desktop_commander__search_code: Search code content
  - mcp__desktop_commander__start_process: Run commands
```

## 🚨 AGENT-SPECIFIC MCP REQUIREMENTS

### **SPARC MASTER AGENT**
```yaml
MANDATORY MCP Usage:
  Code Operations: "ALWAYS use Serena for all code analysis and modifications"
  Memory Management: "ALWAYS use Claude Flow memory for workflow state"
  Agent Coordination: "ALWAYS use Claude Flow for phase agent orchestration"
  
Example Pattern:
  # WRONG - Manual file operations
  Write("file.cs", content)
  
  # CORRECT - MCP tool usage
  mcp__serena__replace_symbol_body({
    name_path: "ClassName/MethodName",
    relative_path: "src/file.cs",
    body: newImplementation
  })
```

### **AXON ARCHITECT AGENT**
```yaml
MANDATORY MCP Usage:
  Architecture Analysis: "ALWAYS use Serena for code structure analysis"
  Documentation: "ALWAYS use Serena for finding and updating architecture docs"
  Pattern Validation: "ALWAYS use Serena to find pattern implementations"
  
Example Pattern:
  # WRONG - Manual code search
  Read("src/Application/Commands/")
  
  # CORRECT - MCP tool usage
  mcp__serena__find_symbol({
    name_path: "Commands",
    relative_path: "src/Application/",
    depth: 2,
    include_body: false
  })
```

### **AXON ORCHESTRATOR AGENT**
```yaml
MANDATORY MCP Usage:
  Swarm Management: "ALWAYS use Claude Flow for agent coordination"
  Performance Monitoring: "ALWAYS use Claude Flow performance tools"
  Neural Learning: "ALWAYS use Claude Flow neural patterns"
  
Example Pattern:
  # WRONG - Manual coordination
  spawn multiple agents manually
  
  # CORRECT - MCP tool usage
  mcp__claude_flow__swarm_init({ topology: "hierarchical" })
  mcp__claude_flow__task_orchestrate({ 
    task: "coordinate multi-agent workflow",
    strategy: "adaptive"
  })
```

### **ALL OTHER AGENTS**
```yaml
Universal Requirements:
  1. Code Analysis: "ALWAYS use Serena first"
  2. File Operations: "Use Serena for code, Desktop Commander for others"
  3. Complex Tasks: "Use Claude Flow task orchestration"
  4. State Persistence: "Use Claude Flow memory"
  5. Pattern Recognition: "Use Claude Flow neural tools"
```

## 📋 MCP DECISION TREE

```
Task Type?
├── Code Analysis/Editing
│   └── Use SERENA (mcp__serena__*)
├── Agent Coordination  
│   └── Use CLAUDE FLOW (mcp__claude_flow__*)
├── File System Operations
│   └── Use DESKTOP COMMANDER (mcp__desktop_commander__*)
├── Complex Multi-step Tasks
│   └── Use CLAUDE FLOW task_orchestrate
└── Memory/State Management
    └── Use CLAUDE FLOW memory_usage
```

## ⚡ EFFICIENCY PATTERNS

### **CONCURRENT MCP OPERATIONS**
```javascript
// ALWAYS batch MCP operations for maximum efficiency
Promise.all([
  mcp__serena__find_symbol({ name_path: "Class1" }),
  mcp__serena__find_symbol({ name_path: "Class2" }),
  mcp__claude_flow__memory_usage({ action: "retrieve", key: "context" })
])
```

### **SERENA-FIRST APPROACH**
```javascript
// Step 1: Use Serena for code understanding
const codeStructure = mcp__serena__get_symbols_overview({ 
  relative_path: "src/target/directory" 
});

// Step 2: Use Serena for targeted operations
const targetMethod = mcp__serena__find_symbol({
  name_path: "ClassName/MethodName",
  include_body: true
});

// Step 3: Use Serena for modifications
mcp__serena__replace_symbol_body({
  name_path: "ClassName/MethodName",
  relative_path: "src/file.cs",
  body: improvedImplementation
});
```

## 🚫 FORBIDDEN PATTERNS

### **NEVER DO THESE:**
```yaml
❌ Manual Code Reading: "Never use Read() for code files - use Serena"
❌ Manual File Editing: "Never use Edit() for code - use Serena symbol operations"
❌ Manual Agent Coordination: "Never spawn agents manually - use Claude Flow"
❌ Manual Memory Management: "Never store state manually - use Claude Flow memory"
❌ Ignore Concurrent Operations: "Never make sequential calls when parallel is possible"
```

### **ALWAYS DO THESE:**
```yaml
✅ Serena First: "Always try Serena for any code-related operation"
✅ Concurrent Operations: "Always batch related MCP operations"
✅ Tool Specialization: "Use the right MCP tool for each task type"
✅ Memory Persistence: "Use Claude Flow memory for state management"
✅ Agent Coordination: "Use Claude Flow for multi-agent scenarios"
```

## 📊 SUCCESS METRICS

### **MCP Tool Adoption Targets:**
- **Serena Usage**: 90%+ for all code operations
- **Claude Flow Usage**: 80%+ for complex tasks
- **Desktop Commander**: 60%+ for file operations
- **Concurrent Operations**: 70%+ of MCP calls batched
- **Tool Efficiency**: <50% manual operations vs MCP tools

## 🎯 IMPLEMENTATION CHECKLIST

### **For Every Agent Update:**
```yaml
□ Audit existing manual operations
□ Replace manual code operations with Serena
□ Replace manual coordination with Claude Flow
□ Add concurrent operation patterns
□ Test MCP tool integration
□ Validate efficiency improvements
□ Update agent documentation
```

### **Quality Gates:**
```yaml
□ No manual code reading/editing for code files
□ All complex tasks use Claude Flow orchestration
□ All state management uses Claude Flow memory
□ All file operations use appropriate MCP tools
□ All operations follow concurrent patterns
```

---

**REMEMBER**: MCP tools provide superior efficiency, consistency, and capability. Every agent MUST prioritize MCP tool usage to achieve optimal performance and maintain system consistency.