-- Database initialization script for Axon Backend
-- This script runs automatically when the database container starts for the first time

-- Database: axon_db (configured via POSTGRES_DB environment variable)
-- The database is automatically created by PostgreSQL using the POSTGRES_DB env var

-- Enable required PostgreSQL extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Schemas are managed by EF Core migrations:
-- - identity: Domain entities (AxonPrincipal, Wallet) + ASP.NET Identity tables
-- - chat: Domain entities (Conversation, Messages)
-- - shared: MassTransit outbox/inbox tables (shared across modules)

-- Log that initialization completed
SELECT 'Database initialization completed for axon_db' AS status;