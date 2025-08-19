#!/bin/bash

# Start PostgreSQL database with Docker Compose
echo "🐘 Starting PostgreSQL database..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker first."
    exit 1
fi

# Load environment variables and start database
docker-compose --env-file .env.docker up -d postgres

# Wait for database to be healthy
echo "⏳ Waiting for database to be ready..."
timeout=60
counter=0

while [ $counter -lt $timeout ]; do
    if docker-compose --env-file .env.docker exec postgres pg_isready -U postgres -d axon_chat > /dev/null 2>&1; then
        echo "✅ Database is ready!"
        echo "📊 Database URL: localhost:5432"
        echo "🔧 Adminer URL: http://localhost:8080 (run 'docker-compose --env-file .env.docker up -d adminer' to start)"
        echo ""
        echo "💡 To apply migrations, run: ./scripts/db-migrate.sh"
        exit 0
    fi
    
    echo "⏳ Still waiting... ($counter/$timeout)"
    sleep 2
    counter=$((counter + 2))
done

echo "❌ Database failed to start within $timeout seconds"
echo "🔍 Check logs with: docker-compose --env-file .env.docker logs postgres"
exit 1