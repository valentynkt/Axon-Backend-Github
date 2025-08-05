#!/bin/bash

# Post-create script - runs after container is created but before VS Code attaches
# This script sets up the development environment

set -e

echo "🚀 Post-create: Setting up development environment..."

# Update package lists and install additional tools
echo "📦 Installing additional development tools..."
sudo apk update 2>/dev/null || true

# Install Oh My Zsh if not already installed
if [ ! -d "$HOME/.oh-my-zsh" ]; then
    echo "🎨 Installing Oh My Zsh..."
    sh -c "$(curl -fsSL https://raw.github.com/ohmyzsh/ohmyzsh/master/tools/install.sh)" "" --unattended
    
    # Install useful zsh plugins
    git clone https://github.com/zsh-users/zsh-autosuggestions ${ZSH_CUSTOM:-~/.oh-my-zsh/custom}/plugins/zsh-autosuggestions 2>/dev/null || true
    git clone https://github.com/zsh-users/zsh-syntax-highlighting.git ${ZSH_CUSTOM:-~/.oh-my-zsh/custom}/plugins/zsh-syntax-highlighting 2>/dev/null || true
    
    # Update .zshrc with useful plugins
    sed -i 's/plugins=(git)/plugins=(git dotnet docker docker-compose zsh-autosuggestions zsh-syntax-highlighting)/' ~/.zshrc
fi

# Restore .NET packages
echo "📦 Restoring .NET packages..."
cd /workspace
if [ -f "Axon.Backend.slnx" ]; then
    dotnet restore Axon.Backend.slnx --verbosity quiet
else
    echo "⚠️  Solution file not found, skipping restore"
fi

# Install/update EF Core tools
echo "🔧 Installing Entity Framework tools..."
dotnet tool update --global dotnet-ef --verbosity quiet

# Install project-specific global tools
echo "🛠️  Installing global .NET tools..."
dotnet tool update --global dotnet-outdated-tool --verbosity quiet
dotnet tool update --global dotnet-format --verbosity quiet
dotnet tool update --global Microsoft.dotnet-httprepl --verbosity quiet

# Set up git configuration helpers
echo "🔧 Setting up Git configuration..."
git config --global --add safe.directory /workspace
git config --global init.defaultBranch main

# Wait for dependencies to be ready
echo "⏳ Waiting for services to be ready..."
until pg_isready -h postgres -p 5432 -U postgres -d axon_dev; do
    echo "Waiting for PostgreSQL..."
    sleep 2
done

until curl -f http://eventstore:2113/health/live >/dev/null 2>&1; do
    echo "Waiting for EventStore..."
    sleep 2
done

until redis-cli -h redis ping >/dev/null 2>&1; do
    echo "Waiting for Redis..."
    sleep 2
done

# Run initial database migrations if they exist
echo "🗄️  Setting up databases..."
if [ -d "/workspace/src/Modules/Identity/src/Data/Migrations" ]; then
    echo "Running Identity migrations..."
    cd /workspace
    dotnet ef database update --project src/Modules/Identity/src/Identity.csproj --connection "Host=postgres;Port=5432;Database=axon_identity_dev;Username=postgres;Password=postgres" --verbose || true
fi

if [ -d "/workspace/src/Modules/Chat/Infrastructure/Persistence/Migrations" ]; then
    echo "Running Chat migrations..."
    cd /workspace
    dotnet ef database update --project src/Modules/Chat/Infrastructure/Axon.Modules.Chat.Infrastructure.csproj --connection "Host=postgres;Port=5432;Database=axon_chat_dev;Username=postgres;Password=postgres" --verbose || true
fi

# Build the solution to ensure everything is working
echo "🔨 Building solution..."
cd /workspace
if [ -f "Axon.Backend.slnx" ]; then
    dotnet build Axon.Backend.slnx --configuration Debug --verbosity quiet --no-restore
    echo "✅ Solution built successfully"
else
    echo "⚠️  Solution file not found, skipping build"
fi

# Create helpful aliases
echo "🔧 Setting up helpful aliases..."
cat >> ~/.zshrc << 'EOF'

# Axon Backend Development Aliases
alias ll='ls -la'
alias dps='docker ps'
alias dcup='docker-compose up -d'
alias dcdown='docker-compose down'
alias dclogs='docker-compose logs -f'
alias psql-dev='psql -h postgres -U postgres -d axon_dev'
alias psql-test='psql -h postgres-test -U postgres -d axon_test'
alias build='dotnet build Axon.Backend.slnx'
alias test='dotnet test Axon.Backend.slnx'
alias run='dotnet run --project src/Api/Axon.Api.csproj'
alias ef='dotnet ef'
alias watch='dotnet watch --project src/Api/Axon.Api.csproj'
alias format='dotnet format Axon.Backend.slnx'
alias outdated='dotnet outdated'

# Quick navigation
alias api='cd /workspace/src/Api'
alias chat='cd /workspace/src/Modules/Chat'
alias identity='cd /workspace/src/Modules/Identity'
alias booking='cd /workspace/src/Modules/Booking'
alias bb='cd /workspace/src/BuildingBlocks'
alias tests='cd /workspace/tests'
EOF

echo "🎉 Post-create setup completed successfully!"
echo ""
echo "🔧 Available commands:"
echo "  build     - Build the solution"
echo "  test      - Run all tests"
echo "  run       - Start the API"
echo "  watch     - Start API with hot reload"
echo "  format    - Format code"
echo "  psql-dev  - Connect to development database"
echo ""
echo "🚀 Ready for development!"