# Axon TypeScript SDK - Technical Architecture

**Code-grounded technical specification aligned with actual backend implementation**

---

**Date:** 2025-10-07
**Author:** Engineering Team
**Status:** Implementation Ready
**Version:** 2.0 (Backend-Aligned)
**Related:** [SDK Product Brief](../../../PRODUCT/sdk-product-brief.md)

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Development Infrastructure](#development-infrastructure)
3. [Package Architecture](#package-architecture)
4. [Core SDK Implementation](#core-sdk-implementation)
5. [Authentication System](#authentication-system)
6. [State Management](#state-management)
7. [API Client Architecture](#api-client-architecture)
8. [Error Handling & Resilience](#error-handling--resilience)
9. [Plugin System](#plugin-system)
10. [Chat Integration](#chat-integration)
11. [React Integration](#react-integration)
12. [Type Generation from OpenAPI](#type-generation-from-openapi)
13. [Testing Strategy](#testing-strategy)
14. [Build & Release Pipeline](#build--release-pipeline)
15. [Performance & Optimization](#performance--optimization)
16. [Security Architecture](#security-architecture)
17. [Observability & Debugging](#observability--debugging)
18. [Future Roadmap](#future-roadmap)

---

## Architecture Overview

### System Context

```
┌─────────────────────────────────────────────────────────────┐
│                     Axon AI Platform                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Identity   │  │     Chat     │  │   AI Core    │      │
│  │   Module     │  │   Module     │  │              │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
│         │                  │                  │               │
│         └──────────────────┴──────────────────┘              │
│                            │                                  │
│                    ┌───────▼────────┐                        │
│                    │   REST API     │                        │
│                    │   /api/v1      │                        │
│                    └───────┬────────┘                        │
└────────────────────────────┼──────────────────────────────────┘
                             │
                    ┌────────▼────────┐
                    │  Axon SDK       │
                    │  (TypeScript)   │
                    └────────┬────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
┌───────▼────────┐  ┌────────▼────────┐  ┌───────▼────────┐
│  Web Browser   │  │    Node.js      │  │ React Native   │
│  Applications  │  │    Server       │  │  Mobile Apps   │
└────────────────┘  └─────────────────┘  └────────────────┘
```

### Design Principles

1. **Framework-Agnostic Core** - Business logic independent of React/Vue/etc.
2. **Zero Peer Dependencies** - Core package has no runtime dependencies
3. **Tree-Shakeable** - Import only what you use
4. **Type-Safe** - 100% TypeScript with strict mode
5. **Observable** - Event-driven for external integrations
6. **Extensible** - Plugin system for custom behavior
7. **Backend-Aligned** - Types generated from OpenAPI spec

### Technology Stack Summary

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| **Language** | TypeScript 5.x (strict) | Type safety, autocomplete, refactoring |
| **Build** | tsup + esbuild | 50-100x faster than webpack |
| **Monorepo** | Turborepo | Smart caching, parallel builds |
| **Package Manager** | Bun (primary), pnpm (fallback) | 5-10x faster installs |
| **State** | Zustand | Lightweight, no React dependency |
| **Testing** | Vitest + Playwright | Fast, Jest-compatible |
| **Linting** | Biome (primary), ESLint (fallback) | 25x faster |
| **CI/CD** | GitHub Actions | Native integration |
| **Type Generation** | openapi-typescript | Backend types → SDK types |

---

## Development Infrastructure

### Package Manager: Bun

**Why Bun?**

```bash
# Installation speed comparison:
npm install      → ~45 seconds
yarn install     → ~35 seconds
pnpm install     → ~18 seconds
bun install      → ~8 seconds ⚡ (5.6x faster)
```

**Features:**
- Native performance (Zig + JavaScriptCore)
- Built-in tooling (test runner, bundler, transpiler)
- Global cache with hardlinks
- Binary lockfile (`bun.lockb`)
- Node.js compatible

**Configuration:**

```json
// package.json (SDK root)
{
  "name": "@axon-ai/sdk",
  "packageManager": "bun@1.1.38",
  "workspaces": ["packages/*"],
  "engines": {
    "bun": ">=1.1.0",
    "node": ">=20.0.0"
  }
}
```

**Alternative: pnpm** for environments with Bun restrictions (corporate policies, legacy Node.js).

---

### Build Tool: tsup (esbuild)

**Configuration:**

```typescript
// tsup.config.ts (shared)
import { defineConfig } from 'tsup';

export default defineConfig({
  entry: ['src/index.ts'],
  format: ['cjs', 'esm'],        // Dual format
  dts: true,                     // Generate .d.ts
  sourcemap: true,
  clean: true,
  splitting: true,               // Code splitting
  treeshake: true,
  minify: true,
  target: 'es2022',
  platform: 'neutral',           // Browser + Node.js
});
```

**Performance:**

| Tool | Build Time | Use Case |
|------|-----------|----------|
| tsup | ~1.2s | Production bundles |
| esbuild | ~0.8s | Development (watch) |
| tsc | ~8.5s | Type-checking only |

---

### Monorepo: Turborepo

**Project Structure:**

```
axon-sdk/
├── packages/
│   ├── sdk-core/              → @axon-ai/sdk-core
│   ├── sdk-react/             → @axon-ai/sdk-react
│   ├── sdk-chat-ui/           → @axon-ai/sdk-chat-ui
│   ├── sdk-dynamic/           → @axon-ai/sdk-dynamic
│   ├── sdk-wallet-adapter/    → @axon-ai/sdk-wallet-adapter
│   ├── sdk-viem/              → @axon-ai/sdk-viem (future)
│   └── sdk-devtools/          → @axon-ai/sdk-devtools
├── apps/
│   ├── docs/                  → Documentation (Nextra)
│   └── examples/
│       ├── next-app/
│       ├── vite-react/
│       └── vanilla-js/
├── turbo.json
├── package.json
└── bun.lockb
```

**Turborepo Pipeline:**

```json
// turbo.json
{
  "pipeline": {
    "build": {
      "dependsOn": ["^build"],
      "outputs": ["dist/**"],
      "cache": true
    },
    "test": {
      "dependsOn": ["build"],
      "cache": true
    },
    "dev": {
      "cache": false,
      "persistent": true
    }
  }
}
```

---

### TypeScript Configuration

**Root tsconfig.json:**

```json
{
  "compilerOptions": {
    // Strict Type-Checking
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "noImplicitOverride": true,
    "exactOptionalPropertyTypes": true,

    // Modern ES
    "target": "ES2022",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "Bundler",

    // Imports
    "esModuleInterop": true,
    "allowSyntheticDefaultImports": true,
    "resolveJsonModule": true,
    "isolatedModules": true,

    // Path Aliases
    "baseUrl": ".",
    "paths": {
      "@axon-ai/sdk-core": ["./packages/sdk-core/src"],
      "@axon-ai/sdk-react": ["./packages/sdk-react/src"]
    },

    // Output
    "declaration": true,
    "declarationMap": true,
    "sourceMap": true,
    "skipLibCheck": true
  }
}
```

---

## Package Architecture

### Package Dependency Graph

```
@axon-ai/sdk-chat-ui
  │
  └── @axon-ai/sdk-react (Context + Hooks)
        └── @axon-ai/sdk-core

@axon-ai/sdk-core (Framework-agnostic)
  │
  ├── @axon-ai/sdk-dynamic (Dynamic.xyz adapter)
  ├── @axon-ai/sdk-wallet-adapter (Solana wallets)
  └── @axon-ai/sdk-viem (EVM chains - future)
```

### Core Package: @axon-ai/sdk-core

**Responsibilities:**
- Authentication lifecycle (challenge, verify, exchange, refresh)
- Token management (storage, rotation, expiry tracking)
- API client (HTTP)
- Event system (auth state changes, token refresh)
- Plugin architecture
- Error handling

**Bundle Size:** ~12KB gzipped (including Zustand)

**Exports:**

```typescript
// Main SDK client
export { AxonClient } from './client';

// Authentication
export { AuthManager } from './auth/AuthManager';
export type { AuthState, ChallengeResponse, AuthTokenResponse } from './auth/types';

// API Client
export { ApiClient } from './api/ApiClient';
export { ChatClient } from './api/ChatClient';

// State Management
export { useAuthStore } from './state/authStore';

// Plugin System
export type { AxonPlugin, PluginContext } from './plugins/types';
export { retryPlugin, rateLimitPlugin, telemetryPlugin } from './plugins';

// Errors
export { AxonError, ErrorCode } from './errors';

// Types
export type { AxonConfig, Environment, StorageAdapter } from './types';
```

---

### React Package: @axon-ai/sdk-react

**Responsibilities:**
- React Context provider
- Hooks for auth and chat
- SSR compatibility
- TypeScript-first API

**Bundle Size:** ~4KB gzipped

**Exports:**

```typescript
// Provider
export { AxonProvider } from './AxonProvider';

// Hooks
export { useAxonAuth } from './hooks/useAxonAuth';
export { useConversation } from './hooks/useConversation';
export { useConversations } from './hooks/useConversations';
export { useMessages } from './hooks/useMessages';

// Types
export type { AxonProviderProps } from './types';
```

**Usage:**

```tsx
// App root
import { AxonProvider } from '@axon-ai/sdk-react';

function App() {
  return (
    <AxonProvider config={{ environment: 'mainnet' }}>
      <YourApp />
    </AxonProvider>
  );
}

// Child component
import { useAxonAuth } from '@axon-ai/sdk-react';

function AuthButton() {
  const { isAuthenticated, login, logout } = useAxonAuth();

  if (isAuthenticated) {
    return <button onClick={logout}>Logout</button>;
  }

  return <button onClick={login}>Login</button>;
}
```

---

## Core SDK Implementation

### AxonClient: Main SDK Class

```typescript
// packages/sdk-core/src/client/AxonClient.ts
import { AuthManager } from '../auth/AuthManager';
import { ApiClient } from '../api/ApiClient';
import { ChatClient } from '../api/ChatClient';
import { EventEmitter } from '../events/EventEmitter';
import type { AxonConfig, AxonPlugin } from '../types';

export class AxonClient {
  public readonly auth: AuthManager;
  public readonly api: ApiClient;
  public readonly chat: ChatClient;
  public readonly events: EventEmitter;

  private readonly config: AxonConfig;
  private readonly plugins: AxonPlugin[] = [];

  constructor(config: AxonConfig) {
    this.config = this.validateConfig(config);

    // Initialize event system
    this.events = new EventEmitter();

    // Initialize core managers
    this.auth = new AuthManager(this.config, this.events);
    this.api = new ApiClient(this.config, this.auth, this.events);
    this.chat = new ChatClient(this.api, this.events);

    // Load plugins
    if (config.plugins) {
      this.plugins = config.plugins;
      this.plugins.forEach(plugin => this.loadPlugin(plugin));
    }
  }

  private validateConfig(config: AxonConfig): AxonConfig {
    // Validate required fields
    if (!config.environment) {
      throw new Error('AxonClient: environment is required');
    }

    // Apply defaults
    return {
      storage: config.storage ?? 'memory',
      retryAttempts: config.retryAttempts ?? 3,
      timeout: config.timeout ?? 30000,
      ...config
    };
  }

  private loadPlugin(plugin: AxonPlugin): void {
    if (plugin.onInit) {
      plugin.onInit(this);
    }
  }

  public async destroy(): Promise<void> {
    // Cleanup resources
    await this.auth.logout();
    this.events.removeAllListeners();
  }
}
```

**Configuration Interface:**

```typescript
// packages/sdk-core/src/types/config.ts
export interface AxonConfig {
  // Required
  environment: 'mainnet' | 'devnet' | 'testnet' | 'local';

  // Optional
  apiUrl?: string;                    // Custom API endpoint
  storage?: StorageType | StorageAdapter;  // 'memory' | 'localStorage' | custom
  retryAttempts?: number;             // Default: 3
  timeout?: number;                   // Request timeout (ms), default: 30s
  plugins?: AxonPlugin[];             // Custom plugins
  debug?: boolean;                    // Enable debug logging
  telemetry?: TelemetryConfig;        // Sentry/Datadog integration
}

export type StorageType = 'memory' | 'localStorage' | 'sessionStorage';

export interface StorageAdapter {
  save(key: string, value: string): Promise<void> | void;
  load(key: string): Promise<string | null> | string | null;
  remove(key: string): Promise<void> | void;
}
```

---

## Authentication System

### Authentication Flow Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                   Authentication Paths                       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Path 1: Manual Wallet Sign-In (2 steps - SIMPLIFIED)       │
│  ┌──────────┐  ┌──────────┐                                 │
│  │ Challenge│─→│  Verify  │─→ Access + Refresh Tokens       │
│  └──────────┘  └──────────┘                                 │
│                                                              │
│  Path 2: Dynamic.xyz Integration (1 step)                   │
│  ┌──────────┐                                               │
│  │ Exchange │─→ Access + Refresh Tokens                      │
│  └──────────┘                                               │
│                                                              │
│  Path 3: Refresh Token (silent refresh)                     │
│  ┌──────────┐                                               │
│  │ Refresh  │─→ New Access + New Refresh Token              │
│  └──────────┘                                               │
└─────────────────────────────────────────────────────────────┘
```

### Actual Backend API Contracts (Code-Verified)

**All types generated from backend OpenAPI spec** (see [Type Generation](#type-generation-from-openapi))

```typescript
// ✅ VERIFIED: ChallengeResponseDto.cs (SIMPLIFIED - no MAC/mkv)
interface ChallengeResponse {
  message: string;         // Canonical JSON to sign
  chainId: string;         // "solana-mainnet" (compound format)
  address: string;         // Wallet address
  issuedAt: number;        // Unix seconds
  expiresAt: number;       // Unix seconds (5min TTL)
  nonce: string;           // UUID for replay protection
  audience: string | null; // Optional audience
}

// ✅ VERIFIED: VerifySignatureRequestDto.cs (SIMPLIFIED - no MAC/mkv)
interface VerifySignatureRequest {
  chainId: string;         // "solana" or "solana-mainnet"
  address: string;         // Wallet address
  signedMessage: string;   // Exact JSON that was signed
  signature: string;       // Ed25519 signature (base58 or base64)
}

// ✅ VERIFIED: AuthTokenResponseDto.cs
interface AuthTokenResponse {
  accessToken: string;       // Axon JWT (15min)
  refreshToken: string | null; // 30-day refresh token (nullable)
  tokenType: string;         // "Bearer"
  expiresIn: number;         // Seconds until access token expires
  axonUserId: string;        // User's principal ID (UUID)
  created: boolean;          // Was this a new user registration
  walletsLinked: number;     // Number of wallets linked
  conflicts: number;         // Number of ownership conflicts
}

// ✅ VERIFIED: RefreshTokenResponseDto.cs
interface RefreshTokenResponse {
  success: boolean;              // Operation success flag
  accessToken?: string;          // New access token
  refreshToken?: string;         // New refresh token (rotated)
  tokenType?: string;            // "Bearer"
  expiresIn?: number;            // Seconds until expiry
  issuedAt?: string;             // ISO 8601 timestamp
  accessTokenExpiresAt?: string; // ISO 8601 timestamp
  refreshTokenExpiresAt?: string;// ISO 8601 timestamp
  error?: string;                // Error message (if success=false)
  errorCode?: string;            // Error code (if success=false)
}
```

### AuthManager Implementation

```typescript
// packages/sdk-core/src/auth/types.ts
export interface AuthState {
  accessToken: string;
  refreshToken: string;       // Empty string if null
  expiresAt: string;           // ISO 8601 timestamp for JSON serialization
  refreshExpiresAt: string;    // ISO 8601 timestamp
  axonUserId: string;          // User's principal ID (UUID)
}

// packages/sdk-core/src/auth/AuthManager.ts
import { create } from 'zustand';
import type { AuthState, ChallengeResponse, AuthTokenResponse } from './types';
import type { AxonConfig } from '../types';
import type { EventEmitter } from '../events/EventEmitter';

interface AuthStore {
  authState: AuthState | null;
  isAuthenticated: boolean;
  isRefreshing: boolean;
  setAuthState: (state: AuthState | null) => void;
}

export class AuthManager {
  private store: ReturnType<typeof create<AuthStore>>;
  private storageAdapter: StorageAdapter;
  private refreshTimer: NodeJS.Timeout | null = null;

  constructor(
    private config: AxonConfig,
    private events: EventEmitter
  ) {
    // Initialize Zustand store
    this.store = create<AuthStore>((set) => ({
      authState: null,
      isAuthenticated: false,
      isRefreshing: false,
      setAuthState: (state) => set({
        authState: state,
        isAuthenticated: !!state
      })
    }));

    // Initialize storage
    this.storageAdapter = this.createStorageAdapter(config.storage);

    // Load persisted auth state
    this.loadPersistedAuth();
  }

  /**
   * Path 1: Manual Wallet Sign-In - Step 1
   * Get challenge from backend (SIMPLIFIED - no MAC)
   */
  public async getChallenge(params: {
    chainId: string;
    walletAddress: string;
    audience?: string;
  }): Promise<ChallengeResponse> {
    const response = await fetch(`${this.getApiUrl()}/api/v1/auth/challenge`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(params)
    });

    if (!response.ok) {
      throw new AxonError('Failed to get challenge', {
        code: 'CHALLENGE_FAILED',
        status: response.status
      });
    }

    return response.json();
  }

  /**
   * Path 1: Manual Wallet Sign-In - Step 2
   * Verify signature and get tokens (SIMPLIFIED - no MAC/mkv)
   */
  public async verifySignature(params: {
    chainId: string;
    address: string;
    signedMessage: string;
    signature: string;
  }): Promise<AuthTokenResponse> {
    const response = await fetch(`${this.getApiUrl()}/api/v1/auth/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(params)
    });

    if (!response.ok) {
      const error = await response.json();
      throw new AxonError('Signature verification failed', {
        code: error.code || 'VERIFICATION_FAILED',
        status: response.status,
        details: error
      });
    }

    const authResponse: AuthTokenResponse = await response.json();
    await this.setTokens(authResponse);
    return authResponse;
  }

  /**
   * Path 2: Dynamic.xyz Integration
   * Exchange Dynamic JWT for Axon token
   */
  public async exchangeDynamicToken(dynamicJwt: string): Promise<AuthTokenResponse> {
    const response = await fetch(`${this.getApiUrl()}/api/v1/auth/exchange`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${dynamicJwt}`,
        'Content-Type': 'application/json'
      }
    });

    if (!response.ok) {
      throw new AxonError('Token exchange failed', {
        code: 'EXCHANGE_FAILED',
        status: response.status
      });
    }

    const authResponse: AuthTokenResponse = await response.json();
    await this.setTokens(authResponse);
    return authResponse;
  }

  /**
   * Path 3: Refresh Token
   * Silently refresh access token with token rotation
   */
  public async refreshAccessToken(): Promise<AuthTokenResponse> {
    const currentState = this.store.getState().authState;
    if (!currentState?.refreshToken) {
      throw new AxonError('No refresh token available', {
        code: 'NO_REFRESH_TOKEN'
      });
    }

    this.store.setState({ isRefreshing: true });

    try {
      const response = await fetch(`${this.getApiUrl()}/api/v1/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          refreshToken: currentState.refreshToken
        })
      });

      if (!response.ok) {
        await this.logout();
        throw new AxonError('Refresh failed', {
          code: 'REFRESH_FAILED',
          status: response.status
        });
      }

      const refreshResponse: RefreshTokenResponse = await response.json();

      // Handle new response shape
      if (!refreshResponse.success || !refreshResponse.accessToken || !refreshResponse.refreshToken) {
        throw new AxonError(refreshResponse.error ?? 'Refresh failed', {
          code: refreshResponse.errorCode ?? 'REFRESH_FAILED'
        });
      }

      // Map to AuthTokenResponse
      const authResponse: AuthTokenResponse = {
        accessToken: refreshResponse.accessToken,
        refreshToken: refreshResponse.refreshToken,
        tokenType: refreshResponse.tokenType ?? 'Bearer',
        expiresIn: refreshResponse.expiresIn ?? 900,
        axonUserId: currentState.axonUserId,
        created: false,
        walletsLinked: 0,
        conflicts: 0
      };

      await this.setTokens(authResponse);
      this.events.emit('tokenRefreshed', authResponse);
      return authResponse;

    } finally {
      this.store.setState({ isRefreshing: false });
    }
  }

  /**
   * Get current access token (with automatic refresh)
   */
  public async getAccessToken(): Promise<string | null> {
    const state = this.store.getState().authState;
    if (!state) return null;

    // Check if token needs refresh (< 5 minutes until expiry)
    const expiresAt = new Date(state.expiresAt);
    const fiveMinutesFromNow = new Date(Date.now() + 5 * 60 * 1000);

    if (expiresAt < fiveMinutesFromNow && state.refreshToken) {
      // Proactive refresh
      await this.refreshAccessToken();
      return this.store.getState().authState?.accessToken ?? null;
    }

    return state.accessToken;
  }

  /**
   * Logout (clear tokens)
   */
  public async logout(): Promise<void> {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }

    this.store.getState().setAuthState(null);
    await this.storageAdapter.remove('axon_auth_state');
    this.events.emit('logout');
  }

  /**
   * Internal: Set tokens and persist
   */
  private async setTokens(response: AuthTokenResponse): Promise<void> {
    const authState: AuthState = {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken ?? '',
      expiresAt: new Date(Date.now() + response.expiresIn * 1000).toISOString(),
      refreshExpiresAt: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
      axonUserId: response.axonUserId
    };

    this.store.getState().setAuthState(authState);

    await this.storageAdapter.save(
      'axon_auth_state',
      JSON.stringify(authState)
    );

    this.scheduleTokenRefresh(new Date(authState.expiresAt));
    this.events.emit('authStateChanged', authState);
  }

  /**
   * Schedule automatic token refresh
   */
  private scheduleTokenRefresh(expiresAt: Date): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
    }

    const oneMinuteBeforeExpiry = expiresAt.getTime() - Date.now() - 60 * 1000;
    if (oneMinuteBeforeExpiry > 0) {
      this.refreshTimer = setTimeout(() => {
        this.refreshAccessToken().catch((err) => {
          console.error('Auto-refresh failed:', err);
        });
      }, oneMinuteBeforeExpiry);
    }
  }

  /**
   * Load persisted auth from storage
   */
  private async loadPersistedAuth(): Promise<void> {
    try {
      const stored = await this.storageAdapter.load('axon_auth_state');
      if (stored) {
        const authState = JSON.parse(stored) as AuthState;

        // Parse ISO 8601 strings back to Date objects
        const expiresAt = new Date(authState.expiresAt);
        const refreshExpiresAt = new Date(authState.refreshExpiresAt);
        const now = new Date();

        if (refreshExpiresAt > now) {
          this.store.getState().setAuthState(authState);

          if (expiresAt < now) {
            await this.refreshAccessToken();
          } else {
            this.scheduleTokenRefresh(expiresAt);
          }
        } else {
          await this.storageAdapter.remove('axon_auth_state');
        }
      }
    } catch (error) {
      console.error('Failed to load persisted auth:', error);
    }
  }

  private createStorageAdapter(storage: StorageType | StorageAdapter): StorageAdapter {
    if (typeof storage === 'object') {
      return storage;
    }

    switch (storage) {
      case 'localStorage':
        return {
          save: (key, value) => localStorage.setItem(key, value),
          load: (key) => localStorage.getItem(key),
          remove: (key) => localStorage.removeItem(key)
        };
      case 'sessionStorage':
        return {
          save: (key, value) => sessionStorage.setItem(key, value),
          load: (key) => sessionStorage.getItem(key),
          remove: (key) => sessionStorage.removeItem(key)
        };
      case 'memory':
      default:
        const memoryStore = new Map<string, string>();
        return {
          save: (key, value) => memoryStore.set(key, value),
          load: (key) => memoryStore.get(key) ?? null,
          remove: (key) => memoryStore.delete(key)
        };
    }
  }

  private getApiUrl(): string {
    return this.config.apiUrl ?? this.getDefaultApiUrl();
  }

  private getDefaultApiUrl(): string {
    switch (this.config.environment) {
      case 'mainnet': return 'https://api.axon.ai';
      case 'devnet': return 'https://api-devnet.axon.ai';
      case 'testnet': return 'https://api-testnet.axon.ai';
      case 'local': return 'http://localhost:5000';
    }
  }
}
```

---

## State Management

### Zustand State Architecture

**Why Zustand?**

- Lightweight (~3KB)
- No React dependency (works in core package)
- Simple API
- TypeScript-friendly
- No boilerplate

**Auth Store:**

```typescript
// packages/sdk-core/src/state/authStore.ts
import { create } from 'zustand';
import type { AuthState } from '../auth/types';

interface AuthStore {
  // State
  authState: AuthState | null;
  isAuthenticated: boolean;
  isRefreshing: boolean;

  // Actions
  setAuthState: (state: AuthState | null) => void;
  setRefreshing: (isRefreshing: boolean) => void;
}

export const useAuthStore = create<AuthStore>((set) => ({
  authState: null,
  isAuthenticated: false,
  isRefreshing: false,

  setAuthState: (state) => set({
    authState: state,
    isAuthenticated: !!state
  }),

  setRefreshing: (isRefreshing) => set({ isRefreshing })
}));
```

**React Integration:**

```typescript
// packages/sdk-react/src/hooks/useAxonAuth.ts
import { useContext } from 'react';
import { AxonContext } from '../AxonProvider';
import { useAuthStore } from '@axon-ai/sdk-core';

export function useAxonAuth() {
  const client = useContext(AxonContext);
  const { isAuthenticated, isRefreshing, authState } = useAuthStore();

  return {
    // State
    isAuthenticated,
    isRefreshing,
    userId: authState?.axonUserId ?? null,
    expiresAt: authState?.expiresAt ?? null,

    // Actions
    getChallenge: client.auth.getChallenge.bind(client.auth),
    verifySignature: client.auth.verifySignature.bind(client.auth),
    exchangeDynamicToken: client.auth.exchangeDynamicToken.bind(client.auth),
    logout: client.auth.logout.bind(client.auth)
  };
}
```

---

## API Client Architecture

### HTTP Client (No Streaming in V1)

```typescript
// packages/sdk-core/src/api/ApiClient.ts
import type { AuthManager } from '../auth/AuthManager';
import type { EventEmitter } from '../events/EventEmitter';

export class ApiClient {
  constructor(
    private config: AxonConfig,
    private auth: AuthManager,
    private events: EventEmitter
  ) {}

  /**
   * HTTP request with automatic auth injection
   */
  public async request<T>(
    endpoint: string,
    options?: RequestInit
  ): Promise<T> {
    const accessToken = await this.auth.getAccessToken();

    const response = await fetch(`${this.getBaseUrl()}${endpoint}`, {
      ...options,
      headers: {
        ...options?.headers,
        ...(accessToken && { 'Authorization': `Bearer ${accessToken}` }),
        'Content-Type': 'application/json'
      }
    });

    if (response.status === 401) {
      // Token expired, try refresh
      await this.auth.refreshAccessToken();

      // Retry request
      return this.request<T>(endpoint, options);
    }

    if (!response.ok) {
      throw new AxonError('API request failed', {
        code: 'API_ERROR',
        status: response.status,
        endpoint
      });
    }

    return response.json();
  }

  private getBaseUrl(): string {
    return this.config.apiUrl ?? this.getDefaultApiUrl();
  }

  private getDefaultApiUrl(): string {
    switch (this.config.environment) {
      case 'mainnet': return 'https://api.axon.ai';
      case 'devnet': return 'https://api-devnet.axon.ai';
      case 'testnet': return 'https://api-testnet.axon.ai';
      case 'local': return 'http://localhost:5000';
    }
  }
}
```

---

## Error Handling & Resilience

### AxonError Class

```typescript
// packages/sdk-core/src/errors/ErrorCodes.ts
export const ErrorCode = {
  // Network errors
  NETWORK_ERROR: 'NETWORK_ERROR',
  TIMEOUT: 'TIMEOUT',

  // Auth errors
  AUTH_INVALID_CHALLENGE: 'AUTH.INVALID_CHALLENGE',
  AUTH_AUTHENTICATION_FAILED: 'AUTH.AUTHENTICATION_FAILED',
  AUTH_TOKEN_EXPIRED: 'AUTH.TOKEN_EXPIRED',
  AUTH_CHALLENGE_EXPIRED: 'AUTH.CHALLENGE_EXPIRED',
  AUTH_INVALID_SIGNATURE: 'AUTH.INVALID_SIGNATURE',
  AUTH_NONCE_REPLAY: 'AUTH.NONCE_REPLAY',
  AUTH_MISSING_TOKEN: 'AUTH.MISSING_TOKEN',
  AUTH_TOKEN_REQUIRED: 'AUTH.TOKEN_REQUIRED',

  // Wallet errors
  WALLET_OWNERSHIP_CONFLICT: 'WALLET.OWNERSHIP_CONFLICT',
  WALLET_NOT_CONNECTED: 'WALLET.NOT_CONNECTED',

  // Conversation errors
  CONVERSATION_MESSAGE_LIMIT: 'CONVERSATION.MESSAGE_LIMIT',
  CONVERSATION_TURN_TAKING_VIOLATION: 'CONVERSATION.TURN_TAKING_VIOLATION',

  // Rate limiting
  RATE_LIMIT_EXCEEDED: 'RATE_LIMIT.EXCEEDED',

  // Challenge errors
  CHALLENGE_MISSING_CHAIN_ID: 'CHALLENGE.MISSING_CHAIN_ID',
  CHALLENGE_MISSING_ADDRESS: 'CHALLENGE.MISSING_ADDRESS',
  CHALLENGE_INVALID_JSON: 'CHALLENGE.INVALID_JSON',
  CHALLENGE_INVALID: 'CHALLENGE.INVALID',
  CHALLENGE_VALIDATION_ERROR: 'CHALLENGE.VALIDATION_ERROR',

  // System errors
  INTERNAL_ERROR: 'INTERNAL_ERROR',
  CONFIGURATION_ERROR: 'CONFIGURATION_ERROR',
  EXTERNAL_ERROR: 'EXTERNAL_ERROR',
  UNAVAILABLE: 'UNAVAILABLE',
  PERSISTENCE_ERROR: 'PERSISTENCE_ERROR',

  // SDK-specific errors
  CHALLENGE_FAILED: 'CHALLENGE_FAILED',
  VERIFICATION_FAILED: 'VERIFICATION_FAILED',
  EXCHANGE_FAILED: 'EXCHANGE_FAILED',
  REFRESH_FAILED: 'REFRESH_FAILED',
  NO_REFRESH_TOKEN: 'NO_REFRESH_TOKEN',
  RETRY_BUDGET_EXCEEDED: 'RETRY_BUDGET_EXCEEDED',
  API_ERROR: 'API_ERROR',
} as const;

export type ErrorCode = typeof ErrorCode[keyof typeof ErrorCode];

// packages/sdk-core/src/errors/AxonError.ts
import { ErrorCode } from './ErrorCodes';

export class AxonError extends Error {
  public readonly code: ErrorCode | string;
  public readonly statusCode?: number;
  public readonly details?: unknown;

  constructor(
    message: string,
    config: {
      code: ErrorCode | string;
      status?: number;
      details?: unknown;
    }
  ) {
    super(message);
    this.name = 'AxonError';
    this.code = config.code;
    this.statusCode = config.status;
    this.details = config.details;
  }
}

// Usage example
try {
  await axon.auth.verifySignature({ ... });
} catch (error) {
  if (error instanceof AxonError) {
    switch (error.code) {
      case 'AUTH.AUTHENTICATION_FAILED':
        console.log('Invalid signature');
        break;
      case 'AUTH.INVALID_CHALLENGE':
        console.log('Challenge expired or tampered');
        break;
      case 'WALLET.OWNERSHIP_CONFLICT':
        showConflictDialog();
        break;
      case 'RATE_LIMIT.EXCEEDED':
        const retryAfter = error.details?.retryAfter ?? 60;
        await sleep(retryAfter * 1000);
        break;
      default:
        console.error('Unexpected error:', error);
    }
  }
}
```

### Error Codes & Remediation Table

| Status | Backend Error Code | SDK Action | Developer Action |
|--------|-------------------|------------|------------------|
| 400 | `AUTH.INVALID_CHALLENGE` | None | Regenerate challenge |
| 401 | `AUTH.AUTHENTICATION_FAILED` | None | Show error, allow retry |
| 401 | `AUTH.TOKEN_EXPIRED` | **Auto-refresh** | None (SDK handles) |
| 409 | `WALLET.OWNERSHIP_CONFLICT` | Emit event | Display conflict UI |
| 422 | `CONVERSATION.MESSAGE_LIMIT` | None | Start new conversation |
| 429 | `RATE_LIMIT.EXCEEDED` | **Retry with backoff** | None (SDK handles) |
| 500 | `INTERNAL_ERROR` | **Retry with backoff** | Log, show user message |

### Retry Logic Implementation

```typescript
// packages/sdk-core/src/utils/retry.ts
import { ErrorCode } from '../errors/ErrorCodes';
import type { AxonError } from '../errors/AxonError';

const retriableErrors: (ErrorCode | string)[] = [
  ErrorCode.NETWORK_ERROR,
  ErrorCode.INTERNAL_ERROR,
  ErrorCode.RATE_LIMIT_EXCEEDED,
  ErrorCode.TIMEOUT,
  ErrorCode.UNAVAILABLE,
  ErrorCode.EXTERNAL_ERROR,
];

function isRetriable(error: unknown): boolean {
  if (error instanceof AxonError) {
    return retriableErrors.includes(error.code);
  }
  return false;
}

async function requestWithRetry(fn: () => Promise<any>, maxRetries = 3) {
  for (let attempt = 1; attempt <= maxRetries; attempt++) {
    try {
      return await fn();
    } catch (error) {
      if (!isRetriable(error) || attempt === maxRetries) {
        throw error;
      }

      const backoff = Math.min(1000 * 2 ** (attempt - 1), 10000);
      await sleep(backoff);
    }
  }
}

// Rate limit handling
if (response.status === 429) {
  const retryAfter = parseInt(response.headers.get('Retry-After') ?? '60', 10);
  await sleep(retryAfter * 1000);
  return requestWithRetry(fn, maxRetries - 1);
}
```

---

## Plugin System

### Plugin Interface

```typescript
// packages/sdk-core/src/plugins/types.ts
import type { AxonClient } from '../client/AxonClient';
import type { AxonConfig } from '../types';
import type { Logger } from '../logging/Logger';

export interface AxonPlugin {
  name: string;
  version?: string;

  // Lifecycle hooks
  onInit?: (context: PluginContext) => void | Promise<void>;

  // Request pipeline (middleware-style)
  onRequest?: (
    ctx: RequestContext,
    next: () => Promise<ResponseContext>
  ) => Promise<ResponseContext>;

  onResponse?: (ctx: ResponseContext) => ResponseContext | Promise<ResponseContext>;
  onError?: (ctx: ErrorContext) => ErrorContext | Promise<ErrorContext>;

  // Cleanup
  onDestroy?: () => void | Promise<void>;
}

export interface PluginContext {
  client: AxonClient;
  config: AxonConfig;
  logger: Logger;
}

export interface RequestContext {
  url: string;
  method: string;
  headers: Record<string, string>;
  body?: unknown;
  metadata: Record<string, unknown>;
  startTime: number;
}

export interface ResponseContext {
  status: number;
  statusText: string;
  headers: Record<string, string>;
  body: unknown;
  duration: number;
  metadata: Record<string, unknown>;
}

export interface ErrorContext {
  error: Error | AxonError;
  request: RequestContext;
  response?: ResponseContext;
  metadata: Record<string, unknown>;
}
```

### Built-In Plugins

```typescript
// packages/sdk-core/src/plugins/index.ts
export const retryPlugin = (config: {
  maxRetries?: number;
  retryableStatusCodes?: number[];
  backoffMultiplier?: number;
}): AxonPlugin => ({
  name: 'retry',
  async onResponse(ctx) {
    if (config.retryableStatusCodes?.includes(ctx.status)) {
      // Implement retry logic
    }
    return ctx;
  }
});

export const rateLimitPlugin = (config: {
  maxRequestsPerMinute?: number;
  strategy?: 'sliding-window' | 'fixed-window';
}): AxonPlugin => ({
  name: 'rate-limit',
  async onRequest(ctx) {
    // Check if rate limit exceeded, delay if needed
    return ctx;
  }
});

export const analyticsPlugin = (config: {
  track: (event: string, data: any) => void;
}): AxonPlugin => ({
  name: 'analytics',
  async onRequest(ctx) {
    config.track('sdk.request', { url: ctx.url, method: ctx.method });
    return ctx;
  },
  async onResponse(ctx) {
    config.track('sdk.response', { status: ctx.status, duration: ctx.duration });
    return ctx;
  }
});
```

---

## Chat Integration

### ChatClient Implementation (Synchronous)

```typescript
// packages/sdk-core/src/api/ChatClient.ts
import type { ApiClient } from './ApiClient';
import type { EventEmitter } from '../events/EventEmitter';

export interface ChatTurnRequest {
  conversationId?: string;  // UUID, omit to start new conversation
  message: string;
}

export interface ChatTurnResponse {
  conversationId: string;    // UUID
  userMessageId: string;     // UUID
  assistantMessageId: string;// UUID
  assistantMessage: string;  // Complete response text (NOT streamed)
  timestamp: string;         // ISO 8601
}

export class ChatClient {
  constructor(
    private api: ApiClient,
    private events: EventEmitter
  ) {}

  /**
   * Send message and get complete response (synchronous)
   * Backend returns complete message, no streaming in V1
   */
  public async sendMessage(params: ChatTurnRequest): Promise<ChatTurnResponse> {
    this.events.emit('messageSending', params);

    const response = await this.api.request<ChatTurnResponse>(
      '/api/v1/chat/turns',
      {
        method: 'POST',
        body: JSON.stringify(params)
      }
    );

    this.events.emit('messageReceived', response);
    return response;
  }

  /**
   * Get conversation messages
   */
  public async getMessages(conversationId: string): Promise<Message[]> {
    return this.api.request<Message[]>(
      `/api/v1/chat/conversations/${conversationId}/messages`
    );
  }

  /**
   * List conversations
   */
  public async getConversations(): Promise<Conversation[]> {
    return this.api.request<Conversation[]>('/api/v1/chat/conversations');
  }
}
```

**React Hook:**

```typescript
// packages/sdk-react/src/hooks/useConversation.ts
import { useState, useCallback } from 'react';
import { useAxonClient } from './useAxonClient';

export function useConversation(conversationId?: string) {
  const client = useAxonClient();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const sendMessage = useCallback(async (message: string) => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await client.chat.sendMessage({
        conversationId,
        message
      });
      return response;
    } catch (err) {
      setError(err as Error);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [client, conversationId]);

  return {
    sendMessage,
    isLoading,
    error
  };
}
```

---

## React Integration

### AxonProvider

```typescript
// packages/sdk-react/src/AxonProvider.tsx
import { createContext, useContext, useMemo } from 'react';
import { AxonClient } from '@axon-ai/sdk-core';
import type { AxonConfig } from '@axon-ai/sdk-core';

export const AxonContext = createContext<AxonClient | null>(null);

export interface AxonProviderProps {
  config: AxonConfig;
  children: React.ReactNode;
}

export function AxonProvider({ config, children }: AxonProviderProps) {
  const client = useMemo(() => new AxonClient(config), [config]);

  return (
    <AxonContext.Provider value={client}>
      {children}
    </AxonContext.Provider>
  );
}

export function useAxonClient() {
  const client = useContext(AxonContext);
  if (!client) {
    throw new Error('useAxonClient must be used within AxonProvider');
  }
  return client;
}
```

---

## Type Generation from OpenAPI

### Setup

**Install openapi-typescript:**

```bash
bun add -D openapi-typescript
```

**Add to package.json:**

```json
{
  "scripts": {
    "generate:types": "openapi-typescript http://localhost:5000/swagger/v1/swagger.json -o packages/sdk-core/src/types/api.generated.ts",
    "prebuild": "bun run generate:types"
  }
}
```

**CI Integration:**

```yaml
# .github/workflows/ci.yml
- name: Generate types from OpenAPI
  run: bun run generate:types

- name: Check for type drift
  run: |
    if git diff --exit-code packages/sdk-core/src/types/api.generated.ts; then
      echo "✅ Types match backend OpenAPI spec"
    else
      echo "❌ Types out of sync with backend!"
      exit 1
    fi
```

### Usage

```typescript
// packages/sdk-core/src/auth/types.ts
import type { components } from '../types/api.generated';

// Use backend-generated types
export type ChallengeResponse = components['schemas']['ChallengeResponseDto'];
export type AuthTokenResponse = components['schemas']['AuthTokenResponseDto'];
export type RefreshTokenResponse = components['schemas']['RefreshTokenResponseDto'];
export type ChatTurnRequest = components['schemas']['ChatTurnRequestDto'];
export type ChatTurnResponse = components['schemas']['ChatTurnResponseDto'];

// Types automatically match backend reality
// No manual type drift possible
```

**Benefits:**
- ✅ Single source of truth (backend OpenAPI)
- ✅ Zero manual type drift
- ✅ Auto-updates on backend changes
- ✅ CI fails if types mismatch
- ✅ 100% type safety guarantee

---

## Testing Strategy

### Unit Tests (Vitest)

```typescript
// packages/sdk-core/src/auth/AuthManager.test.ts
import { describe, test, expect, beforeEach, vi } from 'vitest';
import { AuthManager } from './AuthManager';
import { EventEmitter } from '../events/EventEmitter';

describe('AuthManager', () => {
  let authManager: AuthManager;
  let events: EventEmitter;

  beforeEach(() => {
    events = new EventEmitter();
    authManager = new AuthManager(
      { environment: 'testnet', storage: 'memory' },
      events
    );
  });

  describe('getChallenge', () => {
    test('should fetch challenge from API', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          message: '{"chain_id":"solana-devnet","address":"..."}',
          nonce: 'test-nonce',
          expiresAt: Date.now() + 300000
        })
      });

      const challenge = await authManager.getChallenge({
        chainId: 'solana-devnet',
        walletAddress: 'test-wallet'
      });

      expect(challenge.nonce).toBe('test-nonce');
    });
  });

  describe('token refresh', () => {
    test('should automatically refresh expiring token', async () => {
      const mockAuthState = {
        accessToken: 'old-token',
        refreshToken: 'refresh-token',
        expiresAt: new Date(Date.now() + 3 * 60 * 1000), // 3 minutes
        axonUserId: 'user-123'
      };

      authManager['store'].getState().setAuthState(mockAuthState);

      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          accessToken: 'new-token',
          refreshToken: 'new-refresh-token',
          expiresIn: 900,
          axonUserId: 'user-123'
        })
      });

      const token = await authManager.getAccessToken();

      expect(token).toBe('new-token');
      expect(fetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/auth/refresh'),
        expect.any(Object)
      );
    });
  });
});
```

### Integration Tests (Playwright)

```typescript
// apps/examples/e2e/auth-flow.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Authentication Flow', () => {
  test('should complete manual wallet sign-in', async ({ page }) => {
    await page.goto('http://localhost:3000');

    await page.click('[data-testid="connect-wallet"]');
    await page.click('[data-testid="wallet-phantom"]');

    // Mock wallet signature
    await page.evaluate(() => {
      window.solana = {
        signMessage: async () => new Uint8Array([/* mock signature */])
      };
    });

    await expect(page.locator('[data-testid="user-profile"]')).toBeVisible();
  });

  test('should handle token refresh automatically', async ({ page, context }) => {
    // Setup: inject expiring token
    await context.addCookies([{
      name: 'axon_auth',
      value: 'expiring-token',
      domain: 'localhost'
    }]);

    await page.goto('http://localhost:3000');

    await page.waitForResponse(
      (response) => response.url().includes('/api/v1/auth/refresh')
    );

    await expect(page.locator('[data-testid="user-profile"]')).toBeVisible();
  });
});
```

### Coverage Requirements

| Package | Line Coverage | Branch Coverage |
|---------|--------------|----------------|
| sdk-core | ≥ 90% | ≥ 85% |
| sdk-react | ≥ 85% | ≥ 80% |
| sdk-chat-ui | ≥ 80% | ≥ 75% |

---

## Build & Release Pipeline

### CI/CD Workflow

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [main, develop]
  pull_request:

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 2

      - uses: oven-sh/setup-bun@v1
        with:
          bun-version: 1.1.38

      - name: Install dependencies
        run: bun install --frozen-lockfile

      - name: Generate types from OpenAPI
        run: bun run generate:types

      - name: Type check
        run: bun turbo run typecheck

      - name: Lint
        run: bun turbo run lint

      - name: Test
        run: bun turbo run test -- --coverage

      - name: Build
        run: bun turbo run build

      - name: Bundle size check
        run: bun run size-limit

      - name: Upload coverage
        uses: codecov/codecov-action@v4
        with:
          token: ${{ secrets.CODECOV_TOKEN }}

  e2e:
    runs-on: ubuntu-latest
    needs: test
    steps:
      - uses: actions/checkout@v4
      - uses: oven-sh/setup-bun@v1
      - run: bun install --frozen-lockfile
      - run: bunx playwright install --with-deps
      - run: bun run test:e2e

  release:
    runs-on: ubuntu-latest
    needs: [test, e2e]
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - uses: oven-sh/setup-bun@v1
      - run: bun install --frozen-lockfile
      - run: bun turbo run build
      - run: bunx semantic-release
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          NPM_TOKEN: ${{ secrets.NPM_TOKEN }}
```

**Pipeline Performance:**

| Stage | Time |
|-------|------|
| Install deps | ~8s |
| Generate types | ~2s |
| Typecheck | ~3s |
| Lint | ~0.4s |
| Test | ~2.5s |
| Build | ~2s |
| E2E | ~15s |
| **Total** | **~33s** |

---

## Performance & Optimization

### Bundle Size Monitoring

```json
// .size-limit.json
[
  {
    "name": "@axon-ai/sdk-core",
    "path": "packages/sdk-core/dist/index.mjs",
    "limit": "12 KB",
    "gzip": true,
    "brotli": true
  },
  {
    "name": "@axon-ai/sdk-react",
    "path": "packages/sdk-react/dist/index.mjs",
    "limit": "4 KB"
  },
  {
    "name": "@axon-ai/sdk-chat-ui",
    "path": "packages/sdk-chat-ui/dist/index.mjs",
    "limit": "15 KB"
  }
]
```

### Bundle Size Targets (Revised - No Streaming)

| Package | Gzipped | Notes |
|---------|---------|-------|
| `@axon-ai/sdk-core` | ~12KB | Auth + API client (NO SSE) |
| `@axon-ai/sdk-react` | ~4KB | Context-based hooks |
| `@axon-ai/sdk-chat-ui` | ~15KB | Simple chat UI (NO streaming) |
| **Full SDK** | ~35KB | All packages (reduced) |
| **Minimal** | ~16KB | Core + React (auth only) |

**Why Smaller:**
- No SSE parser (~3KB saved)
- No streaming state management (~2KB saved)
- No streaming animations (~8KB saved)
- Simpler chat UI (~10KB saved)

### Code Splitting Strategy

```typescript
// Lazy load heavy components
const ChatWidget = lazy(() => import('./components/ChatWidget'));

// Dynamic imports for optional features
export async function loadDynamicAdapter() {
  const { DynamicAdapter } = await import('@axon-ai/sdk-dynamic');
  return new DynamicAdapter();
}
```

---

## Security Architecture

### Token Storage Security

**Security Levels:**

| Storage Type | Security | Persistence | Use Case |
|-------------|----------|-------------|----------|
| Memory | High (XSS-proof) | No | Development, short sessions |
| localStorage | Medium (XSS vulnerable) | Yes | Web apps with encryption |
| sessionStorage | Medium | Session only | Temporary web apps |
| Native Keychain | High | Yes | React Native (recommended) |

**Encrypted Storage Example:**

```typescript
import { encryptAES, decryptAES } from './crypto';

const encryptedStorage: StorageAdapter = {
  async save(key, value) {
    const encrypted = await encryptAES(value, userPassword);
    localStorage.setItem(key, encrypted);
  },
  async load(key) {
    const encrypted = localStorage.getItem(key);
    return encrypted ? decryptAES(encrypted, userPassword) : null;
  },
  async remove(key) {
    localStorage.removeItem(key);
  }
};
```

### HTTPS Enforcement

```typescript
// packages/sdk-core/src/client/AxonClient.ts
private validateConfig(config: AxonConfig): AxonConfig {
  if (config.apiUrl && !config.apiUrl.startsWith('https://')) {
    if (config.environment !== 'local' && config.environment !== 'testnet') {
      throw new Error('HTTPS required for production environments');
    }
  }
  return config;
}
```

---

## Observability & Debugging

### Telemetry Integration

```typescript
// packages/sdk-core/src/telemetry/Telemetry.ts
import * as Sentry from '@sentry/browser';

export class Telemetry {
  constructor(private config: TelemetryConfig) {
    if (config.sentry) {
      Sentry.init({
        dsn: config.sentry.dsn,
        environment: config.environment,
        beforeSend: (event) => this.scrubSensitiveData(event)
      });
    }
  }

  public trackError(error: Error, context?: Record<string, unknown>): void {
    if (this.config.sentry) {
      Sentry.captureException(error, { contexts: { custom: context } });
    }
  }

  public trackEvent(name: string, data?: Record<string, unknown>): void {
    if (this.config.sentry) {
      Sentry.captureMessage(name, {
        level: 'info',
        contexts: { custom: data }
      });
    }
  }

  private scrubSensitiveData(event: Sentry.Event): Sentry.Event {
    const sensitiveKeys = ['accessToken', 'refreshToken', 'signature', 'privateKey'];
    if (event.extra) {
      sensitiveKeys.forEach(key => delete event.extra?.[key]);
    }
    return event;
  }
}
```

### Debug Mode

```typescript
// Enable debug logging
const axon = new AxonClient({
  environment: 'devnet',
  debug: true
});

// Output:
// [Axon SDK] POST /api/v1/auth/challenge → 200 (123ms)
// [Axon SDK] Token expires in 14m 32s
// [Axon SDK] Scheduling auto-refresh in 13m 32s
```

---

## Future Roadmap

### V2.0 Features (Planned)

**SSE Streaming Support:**
- Real-time AI responses via Server-Sent Events
- Token-by-token streaming
- `useStreamingMessage()` React hook
- Streaming animations in chat UI

**Additional Auth Methods:**
- EVM chain support (Ethereum, Polygon, etc.)
- Email/password authentication
- Social login (Google, Apple)

**Performance Optimizations:**
- Request deduplication
- Background refetching
- Optimistic UI updates with TanStack Query

**Developer Experience:**
- React DevTools integration
- Debug mode visualization
- Token expiry timeline

---

## Implementation Phases

### Phase 1: Core + Auth (Weeks 1-3)

**Deliverables:**
- ✅ Core SDK architecture
- ✅ AuthManager with simplified flow (no MAC)
- ✅ Token lifecycle management
- ✅ Storage adapters
- ✅ Event system
- ✅ 90%+ test coverage

### Phase 2: Data Layer (Weeks 4-7)

**Deliverables:**
- ✅ ApiClient with retry logic
- ✅ ChatClient (synchronous)
- ✅ Plugin system
- ✅ Telemetry hooks
- ✅ OpenAPI type generation

### Phase 3: UI Components (Weeks 8-10)

**Deliverables:**
- ✅ React hooks
- ✅ Chat UI component (simple, no streaming)
- ✅ Theming system

### Phase 4: Polish (Weeks 11-14)

**Deliverables:**
- ✅ Documentation (Nextra)
- ✅ Example apps
- ✅ E2E tests
- ✅ Bundle optimization
- ✅ Production release

---

## References

- **Product Brief:** `Docs/PRODUCT/sdk-product-brief.md`
- **Backend API:** `Docs/ENGINEERING/api/`
- **Authentication:** `Docs/ENGINEERING/modules/identity/03-authentication.md`
- **API Contracts:** `Docs/ENGINEERING/modules/identity/05-api-contracts.md`
- **Chat Module:** `Docs/ENGINEERING/modules/chat/`

---

**Document Version:** 2.0 (Backend-Aligned)
**Last Updated:** 2025-10-07
**Maintained By:** Axon Engineering Team
