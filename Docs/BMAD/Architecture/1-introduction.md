# 1. Introduction

This document describes the **Axon Identity Module** architecture—a production-ready stateless backend service built with **DDD**, **CQRS**, and **Clean Architecture**. The service provides two core REST endpoints with comprehensive security and performance features.

## Current Status & Next Focus

**Production System (Epics 1-3 Complete):**
* `POST /auth/exchange` — Idempotent principal creation with **wallet-first resolution** preventing duplicate identities
* `GET /auth/me` — Principal snapshots with **ETag conditional GET** optimization

**Epic 4: Smart User Context Caching (Active Implementation):**
* **Progressive Cache Hierarchy**: Request-scoped (0ms) → Memory cache (<1ms) → Database (20-50ms)
* **AxonUserId Unification**: Type rename for semantic clarity across Identity and Chat modules
* **50x Performance Improvement**: Eliminate 50ms database lookups with intelligent caching
* **Zero New Infrastructure**: Leverages existing `IMemoryCache` in both modules

## Core Architectural Principles

* **Stateless Design**: Provider-issued JWT validation only, no server tokens/cookies
* **Privacy-First**: No contact identifiers persisted or logged
* **Wallet-First Resolution**: Unified identity across authentication methods
* **Production-Hardened**: Rate limiting, ETag caching, OpenTelemetry observability

---
