# ⚡ MCP Tools Quick Reference

## 🚨 HIGHEST PRIORITY - SERENA FIRST (Use for ALL Code Operations)
- `mcp__serena__find_symbol` - Find classes/methods/functions (ALWAYS for code)
- `mcp__serena__search_for_pattern` - Search code patterns (ALWAYS for code search)  
- `mcp__serena__replace_symbol_body` - Replace entire methods/classes (ALWAYS for code editing)
- `mcp__serena__get_symbols_overview` - Understand file structure (ALWAYS for new files)
- `mcp__serena__replace_regex` - Targeted code changes (ALWAYS for small edits)
- `mcp__serena__find_referencing_symbols` - Find code usage (ALWAYS for refactoring)

## ESSENTIAL ORCHESTRATION (Use Daily)
- `mcp__claude_flow__swarm_init` - Initialize coordination topology
- `mcp__claude_flow__task_orchestrate` - Delegate complex tasks to agents
- `mcp__claude_flow__memory_usage` - Store/retrieve context and results
- `mcp__desktop_commander__start_process` - Run commands/tests/builds
- `mcp__desktop_commander__read_file` - Read files (use for non-code files)
- `mcp__desktop_commander__write_file` - Write/create files (use chunking)

## AGENT MANAGEMENT (Use Weekly)  
- `mcp__claude-flow__agent_spawn` - Create specialized agents manually
- `mcp__claude-flow__agent_list` - View active agents and capabilities
- `mcp__claude-flow__agent_metrics` - Track agent performance
- `mcp__claude-flow__neural_train` - Continuous learning improvement

## WORKFLOW AUTOMATION (Use Monthly)
- `mcp__claude-flow__workflow_create` - Define reusable processes
- `mcp__claude-flow__workflow_execute` - Run predefined workflows
- `mcp__claude-flow__bottleneck_analyze` - Identify optimization opportunities
- `mcp__claude-flow__automation_setup` - Configure automation rules

## OPTIMIZATION TOOLS (As Needed)
- `mcp__claude-flow__neural_patterns` - Analyze cognitive approaches
- `mcp__claude-flow__learning_adapt` - Improve coordination over time
- `mcp__claude-flow__parallel_execute` - Run tasks concurrently
- `mcp__claude-flow__batch_process` - Process multiple items efficiently

## MEMORY & PERSISTENCE
- `mcp__claude-flow__memory_search` - Find stored context by patterns
- `mcp__claude-flow__memory_backup` - Backup memory stores
- `mcp__claude-flow__memory_namespace` - Organize context by domains
- `mcp__claude-flow__cache_manage` - Handle coordination cache

## 📋 MCP PRIORITY RULES
1. **Code Operations**: Always use Serena FIRST - never use manual Read/Edit for code
2. **Agent Coordination**: Use Claude Flow for complex tasks and memory
3. **File Operations**: Use Desktop Commander for non-code files
4. **Concurrent Operations**: Always batch MCP calls when possible

**🔧 MCP Integration Guide**: See `.claude/tools/MCP_INTEGRATION_REQUIREMENTS.md`  
**Total Available**: 87 MCP tools across Serena, Claude Flow, Desktop Commander
**Documentation**: See individual agent files for specialized tool usage