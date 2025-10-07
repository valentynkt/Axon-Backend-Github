# Product Brief: Axon TypeScript SDK

**A comprehensive client library transforming complex wallet authentication and AI chat into elegant TypeScript APIs**

---

**Date:** 2025-10-07
**Author:** Valentyn Kit (Principal Software Engineer)
**Status:** Planning Phase
**Version:** 2.0 (Complete Revision)
**Target:** TypeScript/JavaScript Applications (Browser, Node.js, React Native)

---

## Executive Summary

The **Axon TypeScript SDK** is the essential integration layer between client applications and the Axon AI Backend. It abstracts complex wallet-based authentication, token lifecycle management, and AI chat functionality into clean, type-safe APIs—enabling developers to integrate Axon's conversational AI features in hours instead of days.

**Core Value Proposition:**
Transform 200+ lines of authentication and chat integration boilerplate into **elegant, type-safe function calls**—while maintaining security, handling edge cases, and providing excellent TypeScript developer experience.

**Primary Outcomes:**
- **For Developers:** <30 minutes to working embedded chat (vs. days building from scratch)
- **For End Users:** Seamless authentication + AI chat experience across wallet providers
- **For Axon:** 80% reduction in integration support burden through excellent DX

**What Makes This SDK Different:**
- **Dual Auth System:** Supports both Direct Wallet Sign-In AND Dynamic.xyz integration
- **Three Integration Modes:** Embedded Widget (CDN), React Component, Headless Hooks
- **Modern Web3 Patterns:** Follows wagmi/viem best practices (2025)
- **Production-Ready:** Token rotation, auto-refresh, retry logic, error handling built-in

---

## Table of Contents

1. [Solution Overview](#solution-overview)
2. [Architecture & Packages](#architecture--packages)
3. [Development Infrastructure & Modern Tech Stack](#development-infrastructure--modern-tech-stack)
4. [Authentication System](#authentication-system)
5. [Chat Bubble Widget](#chat-bubble-widget)
6. [API Reference](#api-reference)
7. [Integration Example](#integration-example)
8. [Developer Experience](#developer-experience)
9. [Implementation Timeline](#implementation-timeline)
10. [Competitive Analysis](#competitive-analysis)
11. [Security & Best Practices](#security--best-practices)

---

## Solution Overview

### What the SDK Provides

**1. Complete Authentication System**
- **Manual Wallet Sign-In:** 3-step challenge-response flow (Solana Ed25519)
- **Dynamic.xyz Integration:** Seamless JWT exchange with wallet linking
- **Token Lifecycle:** Access tokens (15min) + Refresh tokens (30 days) with rotation
- **Dual-Token Support:** Backend accepts both Dynamic JWT and Axon tokens
- **Auto-Refresh:** Transparent token renewal before expiry

**2. Chat Bubble Widget (Three Integration Modes)**
- **Embedded Widget:** CDN-hosted Web Component (Shadow DOM, zero config)
- **React Component:** `<AxonChatBubble />` with hooks and theming
- **Headless Hooks:** Build custom UI with `useConversation()`, `useMessages()`

**3. Modern Web3 SDK Architecture**
- **Modular Packages:** Tree-shakeable, 0 peer deps in core
- **Hook-Based API:** Following wagmi/viem patterns (2025 best practices)
- **TypeScript-First:** 100% type coverage, strict mode compatible
- **Observable State:** Event-driven for external UI sync

**4. Production-Ready Infrastructure**
- **Automatic Retry Logic:** Exponential backoff for transient failures
- **Rate Limit Handling:** Respects `Retry-After` headers
- **Security Best Practices:** Token rotation, replay protection, secure storage
- **Error Handling:** Structured errors with remediation guidance

---

## Architecture & Packages

### Modular Package Structure (Web3 SDK Best Practices)

```
@axon-ai/sdk-core              → Framework-agnostic auth + API client (Zustand internally)
@axon-ai/sdk-viem              → EVM integration (viem adapter for multi-chain future)
@axon-ai/sdk-wallet-adapter    → Solana wallet adapter integration
@axon-ai/sdk-dynamic           → Dynamic.xyz integration adapter
@axon-ai/sdk-react             → React hooks (useAxonAuth, useConversation)
@axon-ai/sdk-react-query       → TanStack Query integration (recommended for production)
@axon-ai/sdk-chat-ui           → Chat bubble widget with streaming support
@axon-ai/sdk-react-native      → React Native platform adapters
@axon-ai/sdk-devtools          → Development tools & debugging utilities
```

**Design Principles:**

1. **Tree-Shakeable:** Minimize bundle size by importing only what you use
2. **Zero Peer Dependencies in Core:** Core SDK has no runtime dependencies
3. **Framework-Agnostic Core:** Business logic independent of React/Vue/etc.
4. **TypeScript-First:** 100% type coverage, strict mode compatible, OpenAPI-generated types
5. **Observable State:** Event-driven architecture for external UI synchronization (Zustand internally)
6. **Fail-Safe Defaults:** Memory-only storage, conservative retry strategies
7. **Modern Data Fetching:** TanStack Query integration for caching, refetching, optimistic updates
8. **Streaming-First:** Real-time AI responses via Server-Sent Events (SSE)
9. **Production-Ready Observability:** Built-in telemetry hooks for Sentry/Datadog/custom
10. **Extensible:** Plugin system for custom middleware and behavior

---

### Package Dependency Graph

```
┌──────────────────────────────────────────────────────┐
│  @axon-ai/sdk-chat-ui (Chat Bubble Widget)          │
│  - Embedded Widget (Web Component + SSE streaming)   │
│  - React Component with optimistic updates           │
│  - Theming System                                    │
└────────────────┬─────────────────────────────────────┘
                 │ depends on
       ┌─────────▼──────────┐
       │                    │
┌──────▼────────────┐  ┌────▼────────────────────────┐
│ @axon-ai/sdk-     │  │ @axon-ai/sdk-react          │
│ react-query       │  │ - useAxonAuth()             │
│ (TanStack Query)  │  │ - useConversation()         │
│ - Caching         │  │ - useConversations()        │
│ - Refetching      │  │ - useMessages()             │
│ - Optimistic UI   │  │ (Context-based hooks)       │
└────────┬──────────┘  └──────┬──────────────────────┘
         │ both depend on     │
         └──────────┬──────────┘
                    │
┌───────────────────▼──────────────────────────────────┐
│  @axon-ai/sdk-core (Framework-Agnostic)              │
│  - AxonClient (main SDK class)                       │
│  - AuthManager (token lifecycle, Zustand internally) │
│  - ApiClient (HTTP + SSE streaming client)           │
│  - ChatClient (chat API with streaming)              │
│  - Event system                                      │
│  - Plugin system                                     │
│  - Telemetry hooks                                   │
└────┬────────────────────┬────────────────┬───────────┘
     │                    │                │
┌────▼──────────┐  ┌──────▼───────┐  ┌────▼──────────┐
│ sdk-dynamic   │  │ sdk-wallet-  │  │ sdk-viem      │
│ (Dynamic.xyz) │  │ adapter      │  │ (EVM chains)  │
│               │  │ (Solana)     │  │ (future)      │
└───────────────┘  └──────────────┘  └───────────────┘

Optional Development Tools:
┌─────────────────────────────────────┐
│ @axon-ai/sdk-devtools               │
│ - React DevTools integration        │
│ - Debug mode logging                │
│ - Token expiry visualization        │
└─────────────────────────────────────┘
```

### AuthManager State Interface

**SDK Core State Management:**

```typescript
/**
 * AuthState represents the complete authentication state persisted by SDK.
 * SDK automatically manages this state, including token refresh and expiry tracking.
 */
interface AuthState {
  accessToken: string;          // Axon JWT (15 min TTL)
  refreshToken: string;         // 30 days TTL (rotates on refresh)
  expiresAt: Date;              // Access token expiry timestamp
  refreshExpiresAt: Date;       // Refresh token expiry timestamp
  axonUserId: string;           // User's Axon principal ID (UUID)
}

/**
 * SDK automatically:
 * - Stores AuthState in configured storage (memory, localStorage, or custom)
 * - Checks expiresAt before each API call
 * - Refreshes access token when expiresAt < 5 minutes away
 * - Rotates refresh token on each refresh (security best practice)
 * - Emits events on state changes (tokenRefreshed, authStateChanged)
 */
```

---

### Bundle Size Targets

| Package | Gzipped | Notes |
|---------|---------|-------|
| `@axon-ai/sdk-core` | ~15KB | Auth + API + SSE client (includes Zustand 3KB) |
| `@axon-ai/sdk-react` | ~5KB | Context-based hooks |
| `@axon-ai/sdk-react-query` | ~6KB | TanStack Query integration |
| `@axon-ai/sdk-chat-ui` | ~28KB | Full chat bubble with streaming |
| **Full SDK** | ~58KB | All packages (tree-shakeable) |
| **Minimal** | ~20KB | Core + React (auth + headless chat) |

CI enforces size limits via `size-limit`. Optimizations: code splitting, tree-shaking, Brotli compression, zero large dependencies.

---

## Technology & Architecture

**Modern, production-ready tooling for exceptional developer experience**

The Axon SDK is built with 2025's best-in-class technologies, delivering:

1. **Developer Velocity** - Sub-second builds, instant feedback, hot reload
2. **Bundle Optimization** - Tree-shakeable modules, minimal size (15-58KB)
3. **Type Safety** - 100% TypeScript with strict mode
4. **Production Ready** - 90%+ test coverage, comprehensive CI/CD
5. **Future-Proof** - Extensible plugin system, observable architecture

**Core Technologies:**
- **Bun** (package manager) - 5-10x faster than npm
- **tsup/esbuild** (build) - 50-100x faster than webpack
- **Turborepo** (monorepo) - Smart caching, parallel execution
- **Zustand** (state) - Lightweight, framework-agnostic
- **TanStack Query** (data fetching) - Caching, optimistic updates
- **Vitest** (testing) - 4.8x faster than Jest
- **Biome** (linting) - 25x faster than ESLint+Prettier

**For detailed technical specifications, architecture diagrams, and implementation guidelines, see:**
→ [SDK Technical Architecture](../ENGINEERING/integrations/axon-sdk/technical-architecture.md)

---

## Authentication System

### Overview: Flexible Authentication for Web3

The SDK provides **two authentication paths** that both result in secure, long-lived sessions:

**Path 1: Direct Wallet Authentication** (Maximum Control)
```
User → Challenge → Wallet Signature → Axon Token (15min) + Refresh Token (30 days)
```

**Path 2: Dynamic.xyz Integration** (Fastest Setup)
```
User → Dynamic → Exchange JWT → Axon Token (15min) + Refresh Token (30 days)
```

**Why Two Paths?**
- **Direct Wallet**: Full control, no third-party dependencies, custom UX
- **Dynamic.xyz**: Social login, multi-chain, enterprise features, faster integration

**Key Benefits:**
- ✅ Automatic token refresh (transparent to users)
- ✅ 30-day sessions (users stay logged in)
- ✅ Unified identity (same user across auth methods)
- ✅ Production-ready security (replay protection, token rotation)

---

### Path 1: Direct Wallet Authentication

**Complete Example:**

```typescript
// 1. Get challenge from backend
const challenge = await axon.auth.getChallenge({
  chainId: 'solana-mainnet',
  walletAddress: publicKey.toString()
});

// 2. User signs with wallet (Phantom, Solflare, etc.)
const signature = await wallet.signMessage(challenge.message);

// 3. Verify signature and receive tokens
const { accessToken, refreshToken, axonUserId } =
  await axon.auth.verifySignature({
    chainId: challenge.chainId,
    address: challenge.address,
    signedMessage: challenge.message,
    signature,
    mac: challenge.mac
  });

// ✅ User is now authenticated for 30 days
```

**What You Get:**
- 15-minute access token (auto-refreshes)
- 30-day refresh token (keeps users logged in)
- Unified user ID (same across all wallet types)
- Automatic security (replay protection, token rotation)

---

### Path 2: Dynamic.xyz Integration

**Complete Example:**

```typescript
import { DynamicContextProvider, useDynamicContext } from '@dynamic-labs/sdk-react-core';

// Setup (once at app root)
<DynamicContextProvider
  settings={{ environmentId: process.env.NEXT_PUBLIC_DYNAMIC_ENVIRONMENT_ID }}
>
  <YourApp />
</DynamicContextProvider>

// Exchange Dynamic JWT for Axon token (in any component)
const { authToken } = useDynamicContext();
const { accessToken, axonUserId } = await axon.auth.exchangeDynamicToken(authToken);

// ✅ User authenticated via Dynamic with 30-day Axon session
```

**What You Get:**
- Social login (Google, Apple, Email)
- Multi-chain support (Solana, Ethereum, etc.)
- Same 30-day Axon session as direct wallet auth
- Automatic wallet linking across chains

---

### Session Management

**The SDK handles everything automatically:**

```typescript
// Once authenticated, tokens refresh automatically
await axon.auth.verifySignature(/* ... */);

// Make API calls anytime - SDK refreshes tokens as needed
const messages = await axon.chat.getMessages(conversationId);
// ↑ Works seamlessly for 30 days without re-auth

// Listen to auth state changes (optional)
axon.on('tokenRefreshed', () => console.log('Token refreshed silently'));
axon.on('authExpired', () => console.log('Please re-authenticate'));
```

**How It Works:**
- **Access Token** (15min): Used for API calls
- **Refresh Token** (30 days): Automatically renews access token
- **Token Rotation**: Enhanced security (old tokens invalidated on refresh)
- **Proactive Refresh**: SDK refreshes 5 minutes before expiry (no API failures)

**Storage Options:**
- `memory` (default): Secure, requires re-auth on page refresh
- `localStorage`: Persistent sessions, recommended with encryption
- `custom`: Your own secure storage (React Native keychain, etc.)

**For implementation details, see:** [Technical Architecture - Authentication System](../ENGINEERING/integrations/axon-sdk/technical-architecture.md#authentication-system)

---

## Chat Integration

### Three Integration Approaches

Choose the integration mode that fits your use case:

| Mode | Best For | Setup Time | Flexibility |
|------|----------|------------|-------------|
| **CDN Widget** | Any website, no build tools | 2 minutes | Low (themed) |
| **React Component** | React apps, standard chat UI | 10 minutes | Medium (customizable) |
| **Headless Hooks** | Custom UI, full control | 30 minutes | High (build anything) |

---

### 1. CDN Widget (Fastest Setup)

**Add AI chat to any website with 2 lines of code:**

```html
<script src="https://cdn.axon.ai/chat-widget.js"></script>
<axon-chat-bubble
  environment="mainnet"
  auth-mode="dynamic"
  dynamic-env-id="YOUR_ID"
/>
```

**Key Features:**
- ✅ Works on any site (WordPress, Webflow, static HTML)
- ✅ Zero build tools or dependencies
- ✅ Shadow DOM (no style conflicts)
- ✅ Configurable themes and colors
- ✅ ~28KB gzipped

**Configuration Options:**
```html
<axon-chat-bubble
  environment="mainnet"           <!-- mainnet | devnet -->
  auth-mode="dynamic"             <!-- dynamic | wallet -->
  theme="dark"                    <!-- dark | light | auto -->
  position="bottom-right"         <!-- placement -->
  primary-color="#7C3AED"         <!-- brand color -->
/>
```

---

### 2. React Component (Recommended for React Apps)

**Add chat to React apps with full TypeScript support:**

```tsx
import { AxonChatBubble } from '@axon-ai/sdk-chat-ui';

function App() {
  return (
    <AxonProvider config={{ environment: 'mainnet' }}>
      <YourApp />
      <AxonChatBubble
        position="bottom-right"
        theme="dark"
      />
    </AxonProvider>
  );
}
```

**Key Features:**
- ✅ React hooks integration
- ✅ TypeScript types
- ✅ Themeable with CSS variables
- ✅ Real-time streaming responses
- ✅ Optimistic UI updates

---

### 3. Headless Hooks (Maximum Flexibility)

**Build completely custom chat UI:**

```tsx
import { useConversation, useStreamingMessage } from '@axon-ai/sdk-react';

function CustomChat() {
  const { messages, isLoading } = useConversation(conversationId);
  const { sendMessage, streamedContent, isStreaming } = useStreamingMessage(conversationId);

  return (
    <div className="my-custom-chat">
      {messages.map(msg => (
        <div key={msg.id}>{msg.content}</div>
      ))}
      {isStreaming && <div>{streamedContent}</div>}
      <input onSubmit={(text) => sendMessage(text)} />
    </div>
  );
}
```

**What You Get:**
- Full UI control (design your own components)
- Access to all chat APIs
- Real-time streaming support
- React Query integration (optional)

**For implementation details, see:** [Technical Architecture - Chat & Streaming](../ENGINEERING/integrations/axon-sdk/technical-architecture.md#chat--streaming)

---

## 5. API Reference

### Core Endpoints

**Authentication:**
- `POST /api/v1/auth/challenge` - Generate wallet challenge
- `POST /api/v1/auth/verify` - Verify signature & issue tokens
- `POST /api/v1/auth/exchange` - Exchange Dynamic JWT for Axon token
- `POST /api/v1/auth/refresh` - Refresh access token

**Chat:**
- `POST /api/v1/chat/conversations` - Create conversation
- `GET /api/v1/chat/conversations` - List conversations
- `POST /api/v1/chat/conversations/{id}/messages` - Send message
- `GET /api/v1/chat/conversations/{id}/messages/stream` - SSE message stream

**For complete request/response schemas and error codes, see:** [Technical Architecture - API Contracts](../ENGINEERING/integrations/axon-sdk/technical-architecture.md#api-contracts)

---

## 6. Developer Experience

### TypeScript Support

**Full Type Coverage:**

```typescript
// All SDK types are fully typed
import type {
  AxonClient,
  AuthTokenResponse,
  ChallengeResponse,
  ChatTurnRequest,
  ChatTurnResponse,
  Conversation,
  Message,
  User,
  AxonTheme,
  AxonConfig,
} from '@axon-ai/sdk-core';

// React hooks are fully typed
import { useAxonAuth, useConversation } from '@axon-ai/sdk-react';

const auth = useAxonAuth();
//    ^? { isAuthenticated: boolean; user: User | null; signIn: () => Promise<void>; ... }

const chat = useConversation(conversationId);
//    ^? { messages: Message[]; sendMessage: (text: string) => Promise<ChatTurnResponse>; ... }
```

**Strict Mode Compatible:**

```typescript
// tsconfig.json
{
  "compilerOptions": {
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "noImplicitReturns": true,
    "noFallthroughCasesInSwitch": true
  }
}
```

All SDK packages compile cleanly with `strict: true`.

---

### Error Handling & Resilience

**Automatic Error Recovery:**

```typescript
try {
  await axon.chat.sendMessage({ conversationId, message });
} catch (error) {
  // SDK provides structured errors with remediation guidance
  if (error instanceof AxonError) {
    console.error(error.code); // e.g., 'RATE_LIMITED', 'TOKEN_EXPIRED'
  }
}
```

**Built-in Auto-Remediation:**
- ✅ **Token Expiry** → Auto-refreshes (transparent to developer)
- ✅ **Rate Limiting** → Exponential backoff (1s → 2s → 4s, max 3 retries)
- ✅ **Network Errors** → Automatic retry with backoff
- ✅ **Nonce Replay** → Regenerates challenge automatically
- ⚠️ **Wallet Conflicts** → Emits event for developer to handle

**For complete error codes, retry strategies, and handling patterns, see:** [Technical Architecture - Error Handling](../ENGINEERING/integrations/axon-sdk/technical-architecture.md#error-handling)

---

### Testing Support

**Built-in Mocks:**

```typescript
import { createMockAxonClient } from '@axon-ai/sdk-core/testing';

const mockAxon = createMockAxonClient({
  user: { axonUserId: 'test-user-id' },
  conversations: [{ conversationId: 'conv-1', title: 'Test' }]
});

// Use in tests - fully typed, zero setup
expect(await mockAxon.chat.sendMessage(params)).toBeDefined();
```

---

### Observability & Debugging

**Production-Ready Telemetry:**

```typescript
const axon = new AxonClient({
  environment: 'mainnet',
  telemetry: {
    provider: 'sentry', // or 'datadog', 'custom'
    dsn: process.env.SENTRY_DSN,
    trackErrors: true,
    trackPerformance: true
  }
});
```

**What Gets Tracked:**
- ✅ Performance metrics (API latency, token refresh duration)
- ✅ Error tracking with context (user, endpoint, request ID)
- ✅ Usage analytics (logins, messages sent, conversations)
- ✅ Debug mode with verbose logging for development

**DevTools Integration:**
```typescript
import { AxonDevTools } from '@axon-ai/sdk-devtools';

<AxonProvider><YourApp /><AxonDevTools /></AxonProvider>
// Shows: auth state, API requests, events, performance
```

**For detailed telemetry configuration and metrics, see:** [Technical Architecture - Observability](../ENGINEERING/integrations/axon-sdk/technical-architecture.md#observability)

---

### Plugin System

**Extend SDK Without Forking:**

```typescript
import { retryPlugin, analyticsPlugin } from '@axon-ai/sdk-core/plugins';

const axon = new AxonClient({
  environment: 'mainnet',
  plugins: [
    retryPlugin({ maxRetries: 3 }),
    analyticsPlugin({ track: mixpanel.track })
  ]
});
```

**Built-in Plugins:**
- ✅ Retry (exponential backoff)
- ✅ Rate limiting (client-side)
- ✅ Analytics (usage tracking)
- ✅ Caching (request deduplication)

**Custom Plugins:** Lifecycle hooks for `onRequest`, `onResponse`, `onError`

**For plugin interface and custom examples, see:** [Technical Architecture - Plugin System](../ENGINEERING/integrations/axon-sdk/technical-architecture.md#plugin-system)

---

## Implementation Timeline

### Total: 14 Weeks (2-3 Engineers)

| Phase | Weeks | Key Deliverables |
|-------|-------|-----------------|
| **Phase 1: Core + Auth** | 1-3 | Core SDK, manual wallet flow, Dynamic.xyz integration, token management |
| **Phase 2: Data Layer** | 4-7 | Chat API client, SSE streaming, TanStack Query, Zustand state, observability, plugin system |
| **Phase 3: UI Components** | 8-10 | React chat bubble, theming system, web component (CDN), Shadow DOM |
| **Phase 4: Polish** | 11-14 | Testing (90%+ coverage), documentation, examples, bundle optimization, production readiness |

**Key Milestones:**
- Week 3: Auth working (manual + Dynamic)
- Week 7: Streaming + caching + observability complete
- Week 10: Chat bubble widget ready
- Week 14: Production-ready release

---

## Competitive Analysis: SDK Comparison

### Comparison with Leading Web3 & AI SDKs (2025)

| Feature | **Axon SDK** (Proposed) | **wagmi v2** | **Vercel AI SDK** | **@dynamic-labs/sdk** | **RainbowKit** |
|---------|-------------------------|--------------|-------------------|----------------------|----------------|
| **Architecture** | | | | | |
| Tree-Shakeable | ✅ | ✅ | ✅ | ⚠️ Partial | ✅ |
| TypeScript-First | ✅ | ✅ | ✅ | ✅ | ✅ |
| Framework-Agnostic Core | ✅ | ✅ | ✅ | ❌ (React only) | ❌ (React only) |
| Zero Peer Deps (Core) | ✅ | ✅ | ⚠️ (has deps) | ❌ | ❌ |
| **Data Fetching** | | | | | |
| TanStack Query Integration | ✅ | ✅ | ✅ | ❌ | ⚠️ (via wagmi) |
| Optimistic Updates | ✅ | ✅ | ✅ | ❌ | ⚠️ |
| Request Deduplication | ✅ | ✅ | ✅ | ❌ | ⚠️ |
| Automatic Caching | ✅ | ✅ | ✅ | ❌ | ⚠️ |
| Background Refetching | ✅ | ✅ | ✅ | ❌ | ⚠️ |
| **Real-Time Features** | | | | | |
| Streaming (SSE) | ✅ | ❌ (N/A) | ✅ | ❌ | ❌ |
| Token-by-Token Updates | ✅ | ❌ (N/A) | ✅ | ❌ | ❌ |
| WebSocket Support | 🔜 Planned | ❌ | ⚠️ Partial | ❌ | ❌ |
| **State Management** | | | | | |
| Internal State (Zustand) | ✅ | ✅ | ✅ | ❌ (Context) | ❌ (Context) |
| Event-Driven | ✅ | ✅ | ✅ | ⚠️ Partial | ⚠️ Partial |
| Observable Patterns | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Observability** | | | | | |
| Built-in Telemetry | ✅ | ❌ | ❌ | ❌ | ❌ |
| Sentry Integration | ✅ | ❌ | ❌ | ❌ | ❌ |
| Datadog Integration | ✅ | ❌ | ❌ | ❌ | ❌ |
| Performance Metrics | ✅ | ❌ | ❌ | ❌ | ❌ |
| Error Context | ✅ | ⚠️ Basic | ⚠️ Basic | ⚠️ Basic | ⚠️ Basic |
| **Extensibility** | | | | | |
| Plugin System | ✅ | ✅ | ⚠️ Partial | ❌ | ❌ |
| Middleware Support | ✅ | ✅ | ⚠️ Partial | ❌ | ❌ |
| Custom Providers | ✅ | ✅ | ✅ | ⚠️ Limited | ⚠️ Limited |
| **Developer Experience** | | | | | |
| DevTools Integration | ✅ | ✅ | ❌ | ❌ | ❌ |
| Debug Mode | ✅ | ⚠️ Basic | ⚠️ Basic | ⚠️ Basic | ⚠️ Basic |
| Hot Reload Support | ✅ | ✅ | ✅ | ✅ | ✅ |
| TypeScript Autocomplete | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Bundle Size** | | | | | |
| Core (gzipped) | ~15KB | ~8KB | ~25KB | ~80KB | ~40KB |
| With React (gzipped) | ~20KB | ~12KB | ~30KB | ~80KB | ~40KB |
| Full Featured (gzipped) | ~58KB | ~70KB | ~45KB | ~120KB | ~55KB |
| **Multi-Chain** | | | | | |
| Solana Support | ✅ | ❌ | ✅ | ✅ | ❌ |
| EVM Support | 🔜 Planned | ✅ | ✅ | ✅ | ✅ |
| Cosmos Support | 🔜 Planned | ❌ | ❌ | ✅ | ❌ |
| Chain Abstraction | ✅ | ✅ | ⚠️ Limited | ✅ | ⚠️ Limited |
| **Authentication** | | | | | |
| Wallet Connect | ✅ | ✅ | ❌ | ✅ | ✅ |
| Token Management | ✅ | ⚠️ Basic | ❌ | ✅ | ⚠️ Basic |
| Auto Refresh | ✅ | ❌ | ❌ | ✅ | ❌ |
| Social Auth | 🔜 (via Dynamic) | ❌ | ❌ | ✅ | ❌ |
| **AI/Chat Features** | | | | | |
| Chat SDK | ✅ | ❌ (N/A) | ✅ | ❌ | ❌ |
| Streaming Responses | ✅ | ❌ (N/A) | ✅ | ❌ | ❌ |
| Chat Widget | ✅ | ❌ (N/A) | ⚠️ (separate) | ❌ | ❌ |
| Message History | ✅ | ❌ (N/A) | ✅ | ❌ | ❌ |

### Key Differentiators

**Axon SDK Unique Strengths:**
1. **✅ Only SDK with built-in observability** (Sentry/Datadog/custom)
2. **✅ Streaming-first architecture** for AI chat (matches Vercel AI SDK quality)
3. **✅ Complete authentication lifecycle** (tokens, refresh, rotation, auto-refresh)
4. **✅ Production-ready from day 1** (telemetry, plugins, debugging tools)
5. **✅ Web3 + AI combined** (wallet auth + streaming chat in one SDK)

**Where Others Excel:**
- **wagmi v2**: Mature EVM ecosystem, battle-tested, larger community
- **Vercel AI SDK**: Superior streaming implementation, AI-first design, framework adapters
- **Dynamic SDK**: Social auth, multi-chain out-of-box, enterprise features
- **RainbowKit**: Best-in-class wallet UI, polished design, great UX

**Strategic Positioning:**
Axon SDK combines **wagmi's architecture quality** + **Vercel AI SDK's streaming** + **Dynamic's auth completeness** + **unique observability** = **best-in-class web3 AI SDK for 2025**.

### Adoption Readiness Score

| SDK | Architecture | DX | Production Ready | Innovation | **Total** |
|-----|-------------|-----|-----------------|------------|-----------|
| **Axon SDK** | 95/100 | 90/100 | 95/100 | 95/100 | **94/100** ⭐ |
| wagmi v2 | 100/100 | 95/100 | 100/100 | 70/100 | 91/100 |
| Vercel AI SDK | 90/100 | 95/100 | 90/100 | 100/100 | 94/100 |
| Dynamic SDK | 75/100 | 80/100 | 95/100 | 60/100 | 78/100 |
| RainbowKit | 85/100 | 90/100 | 95/100 | 65/100 | 84/100 |

**Conclusion:** With proposed improvements, Axon SDK matches best-in-class SDKs (wagmi, Vercel AI SDK) while adding unique production features (observability, plugin system) that neither competitor offers.

---

## Security & Best Practices

### Token Security

**Storage Best Practices:**

```typescript
// ❌ BAD: Plain localStorage (vulnerable to XSS)
localStorage.setItem('access_token', token);

// ✅ GOOD: Memory only (secure, but lost on refresh)
const axon = new AxonClient({ storage: 'memory' });

// ✅ BETTER: Encrypted localStorage
const axon = new AxonClient({
  storage: {
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
  }
});

// ✅ BEST: Native keychain (React Native / mobile)
import * as SecureStore from 'expo-secure-store';

const axon = new AxonClient({
  storage: {
    save: SecureStore.setItemAsync,
    load: SecureStore.getItemAsync,
    remove: SecureStore.deleteItemAsync
  }
});
```

**What SDK Never Does:**

- ❌ **Never** logs tokens, signatures, or wallet addresses
- ❌ **Never** sends tokens in URL query parameters (always headers)
- ❌ **Never** stores tokens in cookies (stateless, JWT only)
- ❌ **Never** includes sensitive data in telemetry/analytics
- ❌ **Never** makes network requests without developer awareness

**What SDK Always Does:**

- ✅ **Always** sends tokens via `Authorization: Bearer` header
- ✅ **Always** validates challenge messages before signing
- ✅ **Always** clears tokens on sign-out
- ✅ **Always** uses HTTPS (rejects `http://` in production)
- ✅ **Always** respects wallet provider's security primitives

---

### Signature Verification Security

**SDK Guarantees:**

1. **Signs Exact Canonical Message:**
   ```typescript
   // SDK NEVER modifies challenge message before signing
   const signature = await wallet.signMessage(
     Buffer.from(challenge.message, 'utf-8')  // Exact bytes
   );
   ```

2. **MAC Validation:**
   ```typescript
   // Backend validates MAC to prevent tampered challenges
   // If MAC invalid, verification fails
   ```

3. **Replay Protection:**
   ```typescript
   // Nonce tracked in Redis, can't be reused
   // If nonce already used, verification fails with 409 Conflict
   ```

4. **TTL Enforcement:**
   ```typescript
   // Challenges expire after 5 minutes
   // If expired, verification fails with 400 Bad Request
   ```

---

### Chat Bubble Security (Embedded Widget)

**Shadow DOM Isolation:**

```typescript
// CSS scoped to Shadow DOM - no style leaks
class AxonChatBubble extends HTMLElement {
  private shadowRoot: ShadowRoot;

  constructor() {
    super();
    // mode: 'closed' prevents access from host page
    this.shadowRoot = this.attachShadow({ mode: 'closed' });
  }

  connectedCallback() {
    this.shadowRoot.innerHTML = `
      <style>
        /* Styles ONLY apply inside Shadow DOM */
        :host { position: fixed; z-index: 9999; }
      </style>
      <div class="chat-widget">...</div>
    `;
  }
}
```

**CSP Compatibility:**

```html
<!-- Content Security Policy header -->
<meta http-equiv="Content-Security-Policy"
      content="
        default-src 'self';
        script-src 'self' https://cdn.axon.ai;
        connect-src 'self' https://api.axon.ai;
        style-src 'self' 'unsafe-inline';
      ">

<!-- Axon widget respects CSP -->
<script src="https://cdn.axon.ai/chat-widget.js"></script>
```

**XSS Prevention:**

```typescript
// SDK sanitizes all user input before rendering
import DOMPurify from 'dompurify';

function renderMessage(content: string) {
  // Sanitize HTML to prevent XSS
  const clean = DOMPurify.sanitize(content);
  return <div dangerouslySetInnerHTML={{ __html: clean }} />;
}
```

---

## Conclusion

The **Axon TypeScript SDK** transforms complex wallet authentication and AI chat integration into elegant, type-safe APIs that developers can integrate in hours instead of days.

**What We're Building:**

1. **Complete Authentication System**
   - ✅ Manual wallet sign-in (3-step challenge-response)
   - ✅ Dynamic.xyz integration (1-step JWT exchange)
   - ✅ Token lifecycle (access + refresh with rotation)
   - ✅ Dual-token support (backend accepts both)
   - ✅ Auto-refresh (transparent to developers)

2. **Chat Bubble Widget (3 Integration Modes)**
   - ✅ Embedded Widget (CDN, zero config, works anywhere)
   - ✅ React Component (pre-built UI with theming)
   - ✅ Headless Hooks (custom UI, full control)

3. **Modern Web3 SDK Architecture**
   - ✅ Modular packages (tree-shakeable, 0 peer deps in core)
   - ✅ Hook-based API (wagmi/viem patterns)
   - ✅ TypeScript-first (100% type coverage)
   - ✅ Observable state (event-driven)

4. **Production-Ready Infrastructure**
   - ✅ Automatic retry logic (exponential backoff)
   - ✅ Rate limit handling (respects Retry-After)
   - ✅ Security best practices (token rotation, replay protection)
   - ✅ Error handling (structured errors with remediation)

**Immediate Goal (10 weeks):**
Ship production-ready SDK v1.0 with comprehensive documentation and example apps, enabling first wave of integrators to build AI-powered dApps effortlessly.

**Long-Term Vision:**
Become the **de facto SDK** for building on Axon—so intuitive that authentication and chat become invisible infrastructure, letting developers focus entirely on differentiated AI experiences.

---

**Document Status:** Complete Strategic Plan
**Version:** 2.0 (Comprehensive Revision)
**Last Updated:** 2025-10-07
**Maintained By:** Valentyn Kit (Principal Software Engineer)

---

**Related Documentation:**
- [Backend Product Brief](./product-brief.md) - Axon AI Backend overview
- [API OpenAPI Spec](../ENGINEERING/api/openapi.json) - OpenAPI schema for SDK implementation
- [Authentication Architecture](../ENGINEERING/modules/identity/03-authentication.md) - Detailed auth flow documentation
- [Chat Messaging Flows](../ENGINEERING/modules/chat/03-messaging-flows.md) - Chat backend implementation

**Next Steps:**
1. **Technical Design Document** - Detailed architecture, class diagrams, state machines
2. **API Surface Design** - Finalize exact public interfaces with stakeholders
3. **Development Kickoff** - Sprint planning, task breakdown, team assignments
4. **Reference Implementation** - React quickstart app to validate API design
