# Axon Backend Development Container

This development container provides a complete, consistent development environment for the Axon Backend project using **VS Code Dev Containers**.

## 🚀 Quick Start

1. **Prerequisites:**
   - VS Code with the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
   - Docker Desktop

2. **Open in Dev Container:**
   - Open this project in VS Code
   - Press `F1` → "Dev Containers: Reopen in Container"
   - Wait for the container to build and initialize

3. **Start Development:**
   ```bash
   build && run
   ```

## 🏗️ Architecture

### Stack
- **.NET 10 Preview** - Latest .NET with cutting-edge features
- **PostgreSQL 16** - Primary database with full-text search extensions
- **EventStore 24.2** - Event sourcing and CQRS support
- **Redis 7** - Caching and session storage
- **Docker** - Containerized development environment

### Project Structure
```
Axon Backend (Clean Architecture + Modular Monolith)
├── src/
│   ├── Api/                 # Web API layer
│   ├── BuildingBlocks/      # Shared infrastructure
│   ├── Modules/
│   │   ├── Chat/           # Chat module
│   │   ├── Identity/       # User management
│   │   └── Booking/        # Booking system
│   └── Shared/             # Cross-cutting concerns
└── tests/                  # Test projects
```

## 🛠️ Development Tools

### Built-in VS Code Extensions
- **C# Dev Kit** - Modern C# development experience
- **GitHub Copilot** - AI-powered coding assistance
- **Docker** - Container management
- **PostgreSQL** - Database management
- **Git** - Enhanced Git integration

### Global .NET Tools
- `dotnet-ef` - Entity Framework migrations
- `dotnet-format` - Code formatting
- `dotnet-outdated` - Package updates
- `dotnet-httprepl` - API testing

### Helpful Aliases
```bash
# Building & Running
build     # Build the solution
test      # Run all tests
run       # Start the API
watch     # Start with hot reload

# Database
psql-dev  # Connect to development database
psql-test # Connect to test database

# Docker
dps       # docker ps
dcup      # docker-compose up -d
dcdown    # docker-compose down
dclogs    # docker-compose logs -f

# Navigation
api       # cd to API project
chat      # cd to Chat module
identity  # cd to Identity module
bb        # cd to BuildingBlocks
```

## 🗄️ Database Setup

### Databases Created Automatically
- `axon_dev` - Main development database
- `axon_identity_dev` - Identity module database
- `axon_chat_dev` - Chat module database
- `axon_booking_dev` - Booking module database
- `*_test` variants for testing

### PostgreSQL Extensions Enabled
- `uuid-ossp` - UUID generation
- `pg_trgm` - Full-text search trigrams
- `unaccent` - Accent-insensitive search
- `btree_gin` - Advanced indexing
- `pg_stat_statements` - Query performance monitoring

## 🌐 Service Endpoints

| Service | URL | Credentials |
|---------|-----|-------------|
| **API (HTTPS)** | https://localhost:5001 | - |
| **API (HTTP)** | http://localhost:5000 | - |
| **PostgreSQL** | localhost:5432 | postgres/postgres |
| **PostgreSQL Test** | localhost:5433 | postgres/postgres |
| **EventStore UI** | http://localhost:2113 | admin/changeit |
| **Redis** | localhost:6379 | - |
| **MailDev** | http://localhost:1080 | - |

## 🧪 Testing

### Running Tests
```bash
# All tests
test

# Specific test project
dotnet test tests/Api.Tests/

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Containers
The project uses **Testcontainers** for integration testing:
- PostgreSQL containers for database tests
- EventStore containers for event sourcing tests
- Redis containers for cache tests

## 🚀 Production Considerations

### Environment Variables
```bash
ASPNETCORE_ENVIRONMENT=Development
POSTGRES_HOST=postgres
EVENTSTORE_CONNECTION_STRING=esdb://...
REDIS_CONNECTION_STRING=redis:6379
```

### Performance Monitoring
- Built-in OpenTelemetry support
- PostgreSQL query performance tracking
- EventStore metrics collection

## 🔧 Customization

### Adding VS Code Extensions
Edit `.devcontainer/devcontainer.json`:
```json
"extensions": [
  "your.extension.id"
]
```

### Adding Services
Edit `.devcontainer/docker-compose.yml`:
```yaml
services:
  your-service:
    image: your-image
    # configuration
```

### Custom Initialization
Edit `.devcontainer/scripts/post-create.sh` for one-time setup
Edit `.devcontainer/scripts/post-start.sh` for every startup

## 🐛 Troubleshooting

### Container Won't Start
1. Check Docker Desktop is running
2. Ensure no port conflicts (5432, 2113, 6379)
3. Try: "Dev Containers: Rebuild Container"

### Database Connection Issues
```bash
# Check PostgreSQL
psql-dev
\l  # List databases

# Check EventStore
curl http://localhost:2113/health/live

# Check Redis
redis-cli -h redis ping
```

### Performance Issues
```bash
# Check container resources
docker stats

# Check disk usage
docker system df

# Clean up if needed
docker system prune
```

## 📚 Additional Resources

- [.NET 10 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Clean Architecture Guide](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
- [EventStore Documentation](https://developers.eventstore.com/)
- [Dev Containers Documentation](https://code.visualstudio.com/docs/devcontainers/containers)

---

**Happy Coding! 🎉**