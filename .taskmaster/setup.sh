#!/bin/bash

# Task Master Environment Setup for Axon Backend
# Usage: source .taskmaster/setup.sh

echo "🚀 Setting up Task Master environment for Axon Backend..."

# Export OpenRouter API key for Task Master
export OPENROUTER_API_KEY="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"

# Verify Task Master is installed
if command -v task-master &> /dev/null; then
    echo "✅ Task Master is installed (version $(task-master --version))"
else
    echo "❌ Task Master not found. Installing..."
    npm install -g task-master-ai
fi

# Set up shell aliases for convenience
alias tm='task-master'
alias tml='task-master list'
alias tmn='task-master next'
alias tms='task-master set-status'
alias tmr='task-master research'

echo "✅ Environment configured successfully!"
echo ""
echo "📋 Available commands:"
echo "  tm        - Task Master (short alias)"
echo "  tml       - List tasks"
echo "  tmn       - Show next task"
echo "  tms       - Set task status"
echo "  tmr       - Research with AI"
echo ""
echo "📁 Project structure:"
echo "  PRD: .taskmaster/docs/axon-prd.txt"
echo "  Tasks: .taskmaster/tasks/tasks.json"
echo "  Config: .taskmaster/config.json"
echo ""
echo "🏷️ Available tags:"
echo "  master      - Main project tasks (10 tasks)"
echo "  development - Development implementation tasks (5 tasks)"