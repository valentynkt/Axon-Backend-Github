#!/bin/bash

# Reset PostgreSQL database (WARNING: This will delete all data!)
echo "⚠️  WARNING: This will completely reset the database and DELETE ALL DATA!"
echo "🔄 This operation cannot be undone."
echo ""

# Prompt for confirmation
read -p "Are you sure you want to continue? (yes/no): " confirm

if [[ $confirm != "yes" ]]; then
    echo "❌ Operation cancelled."
    exit 0
fi

echo "🛑 Stopping database containers..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker first."
    exit 1
fi

# Stop containers
docker-compose --env-file .env.docker down

echo "🗑️  Removing database volume..."
docker volume rm axon-backend_postgres_data 2>/dev/null || echo "Volume was already removed or doesn't exist"

echo "🚀 Starting fresh database..."
docker-compose --env-file .env.docker up -d postgres

# Wait for database to be healthy
echo "⏳ Waiting for database to be ready..."
timeout=60
counter=0

while [ $counter -lt $timeout ]; do
    if docker-compose --env-file .env.docker exec postgres pg_isready -U postgres -d axon_chat > /dev/null 2>&1; then
        echo "✅ Fresh database is ready!"
        echo "💡 Run './scripts/db-migrate.sh' to apply migrations"
        exit 0
    fi
    
    echo "⏳ Still waiting... ($counter/$timeout)"
    sleep 2
    counter=$((counter + 2))
done

echo "❌ Database failed to start within $timeout seconds"
echo "🔍 Check logs with: docker-compose --env-file .env.docker logs postgres"
exit 1