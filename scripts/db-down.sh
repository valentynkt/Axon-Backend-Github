#!/bin/bash

# Stop PostgreSQL database containers
echo "🛑 Stopping PostgreSQL database..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running."
    exit 1
fi

# Stop and remove containers
docker-compose --env-file .env.docker down

echo "✅ Database containers stopped and removed."
echo "📊 Data is preserved in Docker volume 'axon-backend_postgres_data'"
echo ""
echo "💡 To remove data permanently, run: docker volume rm axon-backend_postgres_data"