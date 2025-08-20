# Task Master Setup for Axon Backend

## Overview
This project uses Task Master AI for intelligent task management powered by Google Gemini 2.5 Pro via OpenRouter.

## Quick Start
```bash
# Load environment variables and aliases
source .taskmaster/setup.sh

# List all tasks
task-master list

# Show next task to work on
task-master next

# Research a topic
task-master research "your topic here"
```

## Directory Structure
```
.taskmaster/
├── docs/           # Project documentation
│   └── axon-prd.txt   # Product Requirements Document
├── tasks/          # Task definitions
│   └── tasks.json     # All project tasks
├── reports/        # Generated reports
├── templates/      # Task templates
├── config.json     # Task Master configuration
├── state.json      # Current state and tags
├── CLAUDE.md       # Context for Claude AI
└── setup.sh        # Environment setup script
```

## Task Tags
- **master** - Main project tasks (10 tasks)
  - Identity & Authentication
  - Infrastructure & Observability
  - Testing & CI/CD
  
- **development** - Implementation tasks (5 tasks)
  - Solution structure setup
  - Core domain primitives
  - Identity module implementation

## Configuration
- **AI Model**: Google Gemini 2.5 Pro (via OpenRouter)
- **Fallback Model**: Google Gemini 2.5 Flash
- **API Provider**: OpenRouter

## Common Commands

### Task Management
```bash
# List tasks for specific tag
task-master list --tag development

# Move task between tags
task-master move --from=1 --from-tag=master --to-tag=in-progress

# Update task status
task-master set-status --id=1 --status=in-progress

# Expand task into subtasks
task-master expand --id=1
```

### PRD Processing
```bash
# Parse PRD to generate new tasks
task-master parse-prd .taskmaster/docs/axon-prd.txt --num-tasks 10 --tag feature
```

### Research
```bash
# Research with AI assistance
task-master research "Clean Architecture best practices for .NET"
```

## MCP Integration
Task Master is configured as an MCP server in:
- `~/.claude/config/mcp.json` - Claude Code
- `~/.cursor/mcp.json` - Cursor
- `~/.vscode/mcp.json` - VS Code

## Environment Variables
The OpenRouter API key is configured in multiple locations:
1. MCP configuration files
2. `.taskmaster/setup.sh` (for CLI usage)

## Troubleshooting

### API Key Issues
If you see "API key not set" errors:
```bash
export OPENROUTER_API_KEY="your-key-here"
```

### Task Display Issues
Some task names may show as "undefined" in certain views. This is a known issue with the current version. Tasks are still functional and can be accessed by ID.

## Project Context
Axon Backend is a modular monolith built with:
- .NET 10 (preview)
- Clean Architecture + DDD + CQRS
- FastEndpoints, MediatR, EF Core 9
- PostgreSQL, OpenTelemetry
- Result Pattern, Strong IDs

See `.taskmaster/docs/axon-prd.txt` for full project details.