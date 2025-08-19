#!/bin/bash

# Apply Entity Framework Core migrations to the database
echo "🚀 Applying Entity Framework Core migrations..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker first."
    exit 1
fi

# Check if database is running
if ! docker-compose --env-file .env.docker exec postgres pg_isready -U postgres -d axon_chat > /dev/null 2>&1; then
    echo "❌ Database is not running. Start it with: ./scripts/db-up.sh"
    exit 1
fi

echo "🔍 Checking current directory..."
if [ ! -f "Axon.Backend.slnx" ]; then
    echo "❌ Please run this script from the project root directory (where Axon.Backend.slnx is located)"
    exit 1
fi

echo "📦 Building solution before applying migrations..."
if ! dotnet build --configuration Debug --verbosity quiet; then
    echo "❌ Build failed. Fix build errors before applying migrations."
    exit 1
fi

echo "🗄️  Applying Chat module migrations..."
if ! dotnet ef database update --project src/Modules/Chat/Infrastructure --startup-project src/Api --context ChatDbContext; then
    echo "❌ Migration failed."
    exit 1
fi

echo "✅ Migrations applied successfully!"
echo "🔗 Database connection: Host=localhost;Database=axon_chat;Username=postgres;Password=postgres"
echo "🌐 Adminer available at: http://localhost:8080 (if started)"
echo ""
echo "💡 To start Adminer: docker-compose --env-file .env.docker up -d adminer"