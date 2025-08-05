#!/bin/bash

# Pre-build script - runs on host before container is created
# This script ensures host prerequisites are met

set -e

echo "🔧 Pre-build: Preparing host environment..."

# Ensure .nuget directory exists for caching
mkdir -p .nuget

# Create local logs directory
mkdir -p logs

# Ensure proper permissions for Docker socket (macOS/Linux)
if [[ "$OSTYPE" == "darwin"* ]]; then
    echo "🍎 macOS detected - Docker socket should be available"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    echo "🐧 Linux detected - checking Docker socket permissions"
    if [ -S /var/run/docker.sock ]; then
        sudo chmod 666 /var/run/docker.sock 2>/dev/null || true
    fi
fi

echo "✅ Pre-build completed successfully"