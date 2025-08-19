-- Database initialization script for Axon Backend
-- This script runs automatically when the database container starts for the first time

-- Create the main database if it doesn't exist (though this is usually handled by POSTGRES_DB)
-- SELECT 'CREATE DATABASE axon_chat' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'axon_chat');

-- You can add any initial setup here, such as:
-- Extensions, custom functions, or seed data

-- Example: Enable UUID extension if needed in the future
-- CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Log that initialization completed
SELECT 'Database initialization completed' AS status;