#!/bin/bash

# Post-start script - runs every time the container starts
# This script performs startup checks and preparations

set -e

echo "🌟 Post-start: Container startup checks..."

# Check if all services are healthy
echo "🏥 Checking service health..."

# PostgreSQL health check
if pg_isready -h postgres -p 5432 -U postgres -d axon_dev >/dev/null 2>&1; then
    echo "✅ PostgreSQL is ready"
else
    echo "⚠️  PostgreSQL is not ready yet"
fi

# EventStore health check
if curl -f http://eventstore:2113/health/live >/dev/null 2>&1; then
    echo "✅ EventStore is ready"
else
    echo "⚠️  EventStore is not ready yet"
fi

# Redis health check
if redis-cli -h redis ping >/dev/null 2>&1; then
    echo "✅ Redis is ready"
else
    echo "⚠️  Redis is not ready yet"
fi

# Update PATH for .NET tools
export PATH="$PATH:/home/vscode/.dotnet/tools"

# Display useful information
echo ""
echo "🎯 Development Environment Ready!"
echo ""
echo "📊 Service Endpoints:"
echo "  🌐 API:        https://localhost:5001 (HTTPS) | http://localhost:5000 (HTTP)"
echo "  🗄️  PostgreSQL: localhost:5432 (dev) | localhost:5433 (test)"
echo "  📊 EventStore: http://localhost:2113 (admin:changeit)"
echo "  💾 Redis:      localhost:6379"
echo "  📧 MailDev:    http://localhost:1080"
echo ""
echo "🛠️  Quick Commands:"
echo "  build && run    - Build and start the API"
echo "  test            - Run all tests"
echo "  psql-dev        - Connect to development database"
echo "  dclogs          - View service logs"
echo ""

# Check if this is the first run by looking for packages
if [ ! -d "/workspace/src/Api/bin" ]; then
    echo "🔄 First run detected - packages will be restored automatically"
fi

echo "✨ Ready for development!"