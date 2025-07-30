# Axon Backend - Project Overview

## Purpose
Axon Backend is a **Modular Monolith** using Clean Architecture + DDD + CQRS patterns. The goal is to ship fast via vertical slices while maintaining seams for potential microservice extraction later.

## Tech Stack
- **.NET 10** (preview 5.25277.114) - `net10.0` target framework
- **Clean Architecture** with Domain-Driven Design (DDD) and CQRS
- **MediatR** for command/query handling (needs to be added)
- **Minimal APIs** for HTTP endpoints
- **OpenAI Direct MCP** for AI capabilities (planned)

## Architecture Philosophy
- **Feature-first modules** under `src/Modules/`
- **Strict dependency rules** (Api → Application, Application → Domain)
- **YAGNI principle** - build what's needed now
- **Vertical slice delivery** approach

## Key Characteristics
- Modern C# practices: records, file-scoped namespaces, nullable reference types
- Strict analyzers with warnings as errors
- Documentation generation enabled for libraries
- Support for AOT analysis (disabled for API project)