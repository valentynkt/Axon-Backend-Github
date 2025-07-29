#!/bin/bash
# Setup script to configure Git hooks for auto-updating documentation index

set -euo pipefail

echo "🔧 Setting up Git hooks for documentation auto-update..."

# Configure Git to use our custom hooks directory
git config core.hooksPath .githooks

echo "✅ Git hooks configured!"
echo "📝 Documentation INDEX.md will now auto-update when docs are committed"
echo ""
echo "Manual usage:"
echo "  ./scripts/update-docs-index.sh       # Update index now"
echo "  ./scripts/update-docs-index.ps1      # PowerShell version (cross-platform)"