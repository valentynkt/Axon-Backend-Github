# Database Setup Guide

This guide explains how to set up and manage the local PostgreSQL database for the Axon Backend project using Docker.

## Prerequisites

- Docker and Docker Compose installed on your machine
- .NET 10 SDK installed
- Git bash or equivalent shell (for Windows users)

## Quick Start

1. **Start the database:**
   ```bash
   ./scripts/db-up.sh
   ```

2. **Apply migrations:**
   ```bash
   ./scripts/db-migrate.sh
   ```

3. **Start developing!**
   The API will connect to `localhost:5432` with the database `axon_chat`.

## Available Scripts

### `./scripts/db-up.sh`
Starts the PostgreSQL database container. The script will:
- Check if Docker is running
- Start PostgreSQL container with health checks
- Wait for the database to be ready
- Display connection information

### `./scripts/db-down.sh`
Stops the database containers while preserving data in Docker volumes.

### `./scripts/db-reset.sh`
⚠️ **DESTRUCTIVE OPERATION** - Completely resets the database:
- Stops all containers
- Removes the database volume (deletes all data)
- Starts a fresh database
- Requires explicit confirmation

### `./scripts/db-migrate.sh`
Applies Entity Framework Core migrations to the database:
- Checks if database is running
- Builds the solution
- Applies Chat module migrations
- Shows connection information

## Database Configuration

### Connection String
```
Host=localhost;Database=axon_chat;Username=postgres;Password=postgres
```

### Environment Variables
Configuration is stored in `.env.docker`:
- `POSTGRES_DB=axon_chat`
- `POSTGRES_USER=postgres`
- `POSTGRES_PASSWORD=postgres`
- `POSTGRES_PORT=5432`
- `ADMINER_PORT=8080`

## Database Administration

### Adminer (Web Interface)
Start the Adminer web interface:
```bash
docker-compose --env-file .env.docker up -d adminer
```

Access at: http://localhost:8080
- Server: `postgres`
- Username: `postgres`
- Password: `postgres`
- Database: `axon_chat`

### Command Line Access
Connect to PostgreSQL via command line:
```bash
docker-compose --env-file .env.docker exec postgres psql -U postgres -d axon_chat
```

## Data Persistence

Data is automatically persisted in a Docker volume named `axon-backend_postgres_data`. 

### Backup Data
```bash
docker-compose --env-file .env.docker exec postgres pg_dump -U postgres axon_chat > backup.sql
```

### Restore Data
```bash
docker-compose --env-file .env.docker exec -T postgres psql -U postgres axon_chat < backup.sql
```

## Troubleshooting

### Database won't start
1. Check if Docker is running: `docker info`
2. Check for port conflicts: `lsof -i :5432`
3. View logs: `docker-compose --env-file .env.docker logs postgres`

### Connection refused
1. Wait for health check to pass (may take 30 seconds)
2. Verify container is running: `docker ps`
3. Check if migrations were applied: `./scripts/db-migrate.sh`

### Migrations fail
1. Ensure database is running: `./scripts/db-up.sh`
2. Build solution first: `dotnet build`
3. Check Entity Framework tools: `dotnet ef --version`

### Fresh start needed
If you encounter persistent issues:
```bash
./scripts/db-reset.sh
./scripts/db-migrate.sh
```

## Architecture Notes

- **Module Structure**: The project uses a modular monolith approach
- **Schema Separation**: Each module uses its own database schema (e.g., "chat")
- **EF Core**: Migrations are managed per module
- **Clean Architecture**: Database context is in the Infrastructure layer

## Security Notes

- **Development Only**: These credentials are for local development only
- **Production**: Use proper secrets management in production environments
- **Network**: Database is only accessible from localhost by default