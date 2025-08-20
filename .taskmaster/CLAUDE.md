# Task Master Context for Claude

## Project Overview
Axon Backend - A modular monolith built with .NET 10, following Clean Architecture, DDD, and CQRS patterns.

## Task Management Configuration
- **AI Models**: Using Google Gemini 2.5 Pro via OpenRouter for task generation and research
- **Primary Tag**: master
- **Task Priority Levels**: high, medium, low
- **Task Status**: pending, in-progress, done, blocked, deferred, cancelled

## Key Project Patterns
1. **Result Pattern**: All operations return Result<T> for explicit error handling
2. **Strong IDs**: Type-safe identifiers (UserId, OrderId, etc.)
3. **Clean Architecture**: Domain → Application → Infrastructure → API
4. **CQRS**: Commands modify state, Queries read data
5. **FastEndpoints**: Minimal API approach instead of controllers

## Current Focus Areas
1. Identity Module - Authentication & Authorization
2. OpenTelemetry Integration - Observability
3. Integration Testing Infrastructure
4. CI/CD Pipeline Setup

## Development Guidelines
- File-scoped namespaces required
- Records for DTOs
- Target-typed new expressions
- Nullable reference types enabled
- Warnings treated as errors in Release mode

## Task Generation Preferences
- Focus on implementation tasks with clear subtasks
- Include technical details relevant to .NET 10 and Clean Architecture
- Prioritize security, testing, and observability
- Consider module boundaries and dependencies