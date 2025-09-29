# Product Brief: Axon AI Backend

**Foundational infrastructure enabling conversational on-chain experiences for Solana newcomers**

---

**Date:** 2025-09-30
**Author:** Valentyn Kit (Principal Software Engineer)
**Status:** Hackathon MVP (60% Complete)
**Version:** 1.0 (Grounded Reality)

---

## Executive Summary

**Axon AI Backend** is the foundational infrastructure that will power conversational on-chain experiences for Solana newcomers through a B2B2C model. Built as a robust, flexible backend with excellent DX and best practices, Axon provides the Identity and Chat infrastructure layer that B2B partners will integrate via API or SDK to serve end-users who are new to crypto.

**Current State (60% Complete):**
- ✅ Identity module with multi-wallet linking (Dynamic.xyz JWT auth)
- ✅ Chat module with conversations and messaging
- ✅ Clean Architecture + CQRS + DDD foundation (.NET 10, FastEndpoints, MediatR, PostgreSQL)

**Hackathon MVP Goal (40% Remaining - 1 Month):**
- Stabilize existing Identity + Chat for production readiness
- Add AI workflow integration (OpenAI-based conversation intelligence)
- Enhance integrations (Helius for on-chain data enrichment)
- Submit to Solana Hackathon for funding

**Post-Hackathon Vision (Phase 2):**
- Full Intent Layer (natural language → on-chain actions)
- Two-layer policy engine (safety-first execution)
- Zero-Message Magic (instant wallet insights)
- B2B SDK for partner integrations

---

## The Problem We're Solving

### The Newcomer's Dilemma

**Current State:**
Newcomers to Solana face overwhelming complexity:
- Confusing wallet setups and seed phrase anxiety
- Cryptic error messages and failed transactions
- No memory of past conversations or preferences
- Disconnected tools requiring constant re-authentication
- Fear of making costly mistakes

**Quantified Impact:**
- 70%+ of crypto newcomers abandon after first failed transaction
- Average onboarding time: 3-5 hours (wallets, funding, first action)
- Support tickets: 60%+ are basic "how do I?" questions
- Trust gap: Newcomers fear scams, hacks, and irreversible mistakes

**Why Existing Solutions Fail:**
- Crypto tools assume technical knowledge
- No conversational interface to guide newcomers
- No persistent memory of user context
- Each tool requires separate authentication
- Generic experiences don't adapt to skill level

**Axon's Approach:**
Provide a **stable, flexible backend infrastructure** that B2B partners can integrate to deliver:
- Conversational AI that remembers context
- Multi-wallet identity that persists across sessions
- Safe, guided experiences tailored to newcomers
- Best-in-class DX (20/80 rule, Clean Architecture, comprehensive docs)

---

## Target Users

### Primary: **Newcomers to Solana (End Users via B2B)**

**NOT power users** - We serve people making their first steps in crypto:

**Profile:**
- 0-6 months in crypto experience
- Intimidated by technical jargon
- Seeking guidance, not full control
- Want safety and simplicity first
- Mobile-first, expect chat-style interfaces

**Pain Points:**
- "I don't understand what I'm doing"
- "I'm afraid I'll lose my money"
- "Why did my transaction fail?"
- "How do I know if this is a scam?"
- "I have to start over every time I use a new app"

**Axon Backend Enables:**
- Persistent identity across partner apps
- Conversational AI that explains in plain English
- Multi-wallet management without complexity
- Context that travels with the user

---

### Secondary: **Solana dApp Builders (B2B Partners)**

**Profile:**
- Early-stage Solana projects building consumer apps
- Teams of 2-5; need to ship fast
- Want to focus on their unique value prop, not rebuild auth/chat/AI

**Pain Points:**
- "Building auth + chat + AI from scratch takes months"
- "We don't have crypto-native expertise"
- "Our users drop off because onboarding is too hard"
- "We can't afford a full backend team"

**Axon Value:**
- Production-ready Identity + Chat infrastructure via API/SDK
- Excellent DX (Clean Architecture, comprehensive docs, 20/80 patterns)
- Flexible integration (REST API today, SDK post-hackathon)
- Focus on their product, we handle the foundation

---

## Current State: What Exists Today (60% Complete)

### ✅ Identity Module (Production Foundation)

**What Works:**
- **AxonPrincipal Aggregate:** User identity with multi-wallet support
- **Wallet Management:** Add, verify, set primary wallet
- **Dynamic.xyz Integration:** JWT authentication working
- **Multi-Wallet Linking:** Users can link multiple wallets to single identity
- **Domain Events:** Cross-module communication via events

**Architecture:**
- Clean Architecture layers (Domain → Application → Infrastructure → API)
- CQRS pattern with MediatR commands/queries
- Result<T, Error> for railway-oriented error handling
- StrongId<T> for type-safe identifiers

**API Endpoints:**
```
POST /auth/login           - JWT authentication via Dynamic.xyz
GET  /auth/me              - Current user profile
POST /wallets/add          - Link new wallet to profile
POST /wallets/verify       - Verify wallet ownership
PUT  /wallets/set-primary  - Set primary wallet
```

---

### ✅ Chat Module (Production Foundation)

**What Works:**
- **Conversation Aggregate:** Multi-participant conversations with business rules
- **Message System:** Messages with AI/User roles
- **Participant Management:** Add/remove participants with ownership rules
- **Metadata Support:** Extensible metadata for conversations

**Business Rules (20+ implemented):**
- Conversation must have owner
- Messages must belong to active conversation
- Participants must be verified AxonPrincipals
- Only owner can modify conversation settings

**API Endpoints:**
```
POST /conversations/create          - Start new conversation
POST /conversations/{id}/messages   - Send message
GET  /conversations/{id}            - Get conversation details
GET  /conversations/list            - List user's conversations
POST /conversations/{id}/participants - Add participant
```

---

### ✅ Technical Foundation (Best Practices)

**Architecture:**
- **Modular Monolith:** Single deployable, clear module boundaries
- **Clean Architecture:** Domain at center, dependencies flow inward
- **CQRS:** Separate read/write models via MediatR
- **DDD:** Aggregates enforce business invariants
- **Result Pattern:** No exceptions for business logic

**Tech Stack:**
- .NET 10 Preview + C# 13 (modern language features)
- FastEndpoints (performance over controllers)
- MediatR (CQRS orchestration)
- FluentValidation (request validation)
- EF Core 9 + PostgreSQL (persistence)
- OpenTelemetry + Serilog (observability)

**Code Quality:**
- File-scoped namespaces, records, target-typed new
- Nullable reference types enabled
- 90%+ test coverage target
- Architecture compliance tests
- Comprehensive documentation (Docs/ENGINEERING/)

**Developer Experience:**
- Hub-and-spoke documentation (progressive disclosure)
- 00-QUICK-REFERENCE.md hot path (80% of patterns)
- Task-to-doc mapping for AI agents
- Clear module boundaries and coding standards

---

## Phase 1 MVP: Hackathon Submission (40% Remaining - 1 Month)

### Goal: Production-Ready Foundation + Initial AI Integration

**Focus Areas:**

### 1. Stabilization & Production Readiness (2 weeks)

**Identity Module:**
- [ ] Add refresh token flow (Dynamic.xyz JWT rotation)
- [ ] Implement rate limiting on auth endpoints
- [ ] Add wallet verification via Helius (on-chain signature check)
- [ ] Comprehensive error handling with user-friendly messages
- [ ] Integration tests for all auth flows

**Chat Module:**
- [ ] Add message pagination (cursor-based)
- [ ] Implement conversation archiving
- [ ] Add message search/filtering
- [ ] Real-time delivery status (sent/delivered/read)
- [ ] Integration tests for conversation lifecycle

**Infrastructure:**
- [ ] Connection pooling optimization (PostgreSQL)
- [ ] Redis caching layer for hot queries
- [ ] Health check endpoints (readiness/liveness)
- [ ] Structured logging with correlation IDs
- [ ] Error tracking (Sentry or similar, within $5K budget)

---

### 2. AI Workflow Integration (1.5 weeks)

**OpenAI Integration:**
- [ ] Conversation context management (maintain chat history)
- [ ] Prompt engineering for newcomer-friendly responses
- [ ] Streaming response support (for real-time chat UX)
- [ ] Cost controls (token limits, caching frequent queries)
- [ ] Fallback handling (if OpenAI down)

**Chat Intelligence:**
- [ ] Inject user context (wallet balances, past transactions) into AI prompts
- [ ] Explain technical concepts in simple terms
- [ ] Detect when user is stuck or confused
- [ ] Suggest next actions based on conversation flow

**API Enhancement:**
```
POST /conversations/{id}/ai-message  - Send message, get AI-enhanced response
POST /conversations/{id}/explain     - Explain last transaction/error in plain English
```

---

### 3. Helius Integration (0.5 weeks)

**On-Chain Data Enrichment:**
- [ ] Wallet balance queries (SOL + SPL tokens)
- [ ] Transaction history retrieval (last 10 transactions)
- [ ] Wallet verification via on-chain signature
- [ ] Error context enrichment (failed tx → readable explanation)

**API Endpoints:**
```
GET /wallets/{id}/balance     - Current wallet balances
GET /wallets/{id}/history     - Recent transaction history
POST /wallets/{id}/verify     - Verify wallet ownership via signature
```

**Budget Impact:** Helius free tier (100K requests/day) fits hackathon demo + early traction

---

### 4. Documentation & DX Polish (Throughout)

- [ ] Update API documentation (OpenAPI/Swagger)
- [ ] Create integration guide for B2B partners (Docs/PRODUCT/integration-guide.md)
- [ ] Record demo video showing newcomer flow
- [ ] Write Solana Hackathon submission (problem/solution/demo)

---

## Success Criteria (Hackathon MVP)

### Technical Completeness
- ✅ All Identity + Chat endpoints production-ready
- ✅ AI-enhanced conversations working (OpenAI integration)
- ✅ Helius on-chain data enrichment functional
- ✅ 90%+ test coverage maintained
- ✅ Zero critical security issues
- ✅ API response times <500ms (p95)

### Demo Quality
- ✅ 3-5 minute walkthrough video showing:
  1. Newcomer connects wallet (Dynamic.xyz)
  2. AI chat explains wallet balance in plain English
  3. User asks "how do I send SOL?" → AI guides step-by-step
  4. Multi-wallet linking demonstrated
  5. Context persists across conversations
- ✅ Clear differentiation: NOT just another chatbot, but **persistent identity + on-chain context**

### Hackathon Submission
- ✅ Problem statement resonates (newcomer onboarding pain)
- ✅ Solution demonstrates technical depth (Clean Architecture, DDD, CQRS)
- ✅ Live demo works flawlessly
- ✅ Code quality impresses judges (docs, tests, architecture)
- ✅ Roadmap shows clear path to Phase 2 (Intent Layer)

---

## Phase 2 Vision: Post-Hackathon (If Funded)

### Funding Goal: $50K-100K (Solana Foundation Grant + Investors)

**Enables:**
- 3-6 months runway
- Hire 1-2 additional engineers
- Build full Intent Layer
- Acquire first 5-10 B2B partners

---

### Phase 2 Roadmap (Post-Funding)

**Months 1-2: Intent Layer Foundation**
- [ ] Natural language intent parsing (GPT-4 Turbo)
- [ ] Intent → Transaction pipeline (Jupiter for swaps, etc.)
- [ ] Transaction simulation (Helius) before execution
- [ ] Unified `/intent` endpoint: `{ text, userWallet } → { transaction, preview }`

**Months 3-4: Policy Engine & Safety**
- [ ] Two-layer policy engine:
  - **Project Layer:** B2B partners set global rules (spend caps, allowlists)
  - **User Layer:** Newcomers set personal preferences (risk tolerance)
- [ ] Human-readable transaction preview before signing
- [ ] MEV protection verification
- [ ] Clear error messages for failed simulations

**Months 5-6: B2B SDK & Zero-Message Magic**
- [ ] TypeScript SDK for B2B integration (1-day setup)
- [ ] React components (chat widget, wallet selector)
- [ ] Zero-Message Magic: Analyze wallet on first connect → instant insights
- [ ] Cross-app memory (user authenticates once, benefits everywhere)

**Month 6: Launch**
- [ ] 5-10 B2B design partners integrated
- [ ] 100-500 end-users (newcomers) onboarded
- [ ] Validate unit economics (infra cost per user)
- [ ] Prepare Series A materials

---

## Technical Architecture

### Current State (What Exists)

```
Axon Backend (Modular Monolith)
├── src/Api (FastEndpoints)
│   ├── Auth endpoints (Dynamic.xyz JWT)
│   └── Chat endpoints (Conversations, Messages)
│
├── src/Modules/Identity/
│   ├── Domain: AxonPrincipal, Wallet aggregates
│   ├── Application: Commands/Queries (MediatR)
│   └── Infrastructure: EF Core repositories
│
├── src/Modules/Chat/
│   ├── Domain: Conversation, Message, Participant aggregates
│   ├── Application: Commands/Queries (MediatR)
│   └── Infrastructure: EF Core repositories
│
└── src/BuildingBlocks/
    ├── Result<T, Error> pattern
    ├── StrongId<T> source generators
    └── Domain event infrastructure
```

**Database:** PostgreSQL with `identity.*` and `chat.*` schemas (module isolation)

---

### Phase 1 Additions (Hackathon MVP)

```
NEW: AI Integration Layer
├── src/Modules/Intelligence/ (NEW)
│   ├── OpenAI service (conversation context management)
│   ├── Prompt templates (newcomer-friendly explanations)
│   └── Token usage tracking (cost control)
│
NEW: On-Chain Data Layer
├── src/Infrastructure/ExternalServices/
│   ├── HeliusService (wallet balance, transaction history)
│   └── DynamicService (JWT auth - already exists)
│
ENHANCED: Chat Module
├── AI-enhanced message responses
├── Context injection (wallet data → AI prompts)
└── Real-time streaming responses
```

---

### Phase 2 Additions (Post-Funding)

```
NEW: Intent Layer
├── src/Modules/Intent/
│   ├── IntentParser (GPT-4 Turbo classification)
│   ├── ActionOrchestrator (route to Jupiter, etc.)
│   ├── TransactionSimulator (Helius simulation)
│   └── PolicyEngine (two-layer rules)
│
NEW: B2B SDK
├── @axon/sdk-typescript/
│   ├── AxonClient (REST API wrapper)
│   ├── React components (ChatWidget, WalletSelector)
│   └── Hooks (useAxonAuth, useConversation)
```

---

## Go-to-Market Strategy

### Phase 1: Hackathon → Early Validation

**Goals:**
1. Win Solana Hackathon (Top 10 finish)
2. Secure $50K-100K funding (grants + investors)
3. Sign 2-3 LOIs (letters of intent) from B2B partners

**Tactics:**
- **Demo Focus:** Newcomer onboarding flow (wallet connect → AI chat → guided action)
- **Differentiation:** NOT just AI chatbot—persistent identity + on-chain context + multi-wallet
- **Technical Depth:** Showcase Clean Architecture, test coverage, docs quality
- **Roadmap Clarity:** Show clear path from MVP → Intent Layer → B2B SDK

**Target Partners (Post-Hackathon):**
- Solana wallet providers (want to reduce support tickets)
- NFT marketplaces (want to onboard newcomers faster)
- DeFi protocols (want to guide users through complex flows)

---

### Phase 2: B2B SDK Launch (Post-Funding)

**Wedge Strategy:**
1. **Design Partners (Month 1-3):** 3-5 early adopters, free integration support
2. **Pilot Program (Month 4-5):** 10-15 partners, paid pilots ($500-1K/month)
3. **General Availability (Month 6+):** Self-service SDK, tiered pricing

**Pricing Model (Post-Funding):**
- **Free Tier:** 1K users/month, basic chat + identity
- **Starter ($500/month):** 10K users/month, AI-enhanced responses, support
- **Growth ($2K/month):** 50K users/month, Intent Layer, custom policies
- **Enterprise (Custom):** White-label, dedicated infra, SLA

**Unit Economics Target:**
- Cost per user (infra): $0.10-0.50/month (OpenAI, Helius, hosting)
- Revenue per user (B2B): $1-2/month (amortized across tier pricing)
- Contribution margin: 50-80% at scale

---

## Constraints & Assumptions

### Constraints (Hackathon MVP)

1. **Timeline:** 1 month until submission (sprint mode)
2. **Team:** Solo Principal Engineer (no designer, no PM, no marketing)
3. **Budget:** $5K max total (infra, tools, services)
4. **Scope:** Must ruthlessly prioritize (stabilization + AI + Helius only)

**Budget Breakdown ($5K):**
```
OpenAI API (GPT-4):          $1,500 (hackathon demo + early testing)
Helius Pro:                  $0 (free tier sufficient for demo)
Dynamic.xyz:                 $0 (free tier)
Hosting (Fly.io/Railway):    $500 (PostgreSQL + API)
Domain + SSL:                $100
Monitoring (Sentry free):    $0
Buffer:                      $2,900 (safety margin)
```

---

### Assumptions (To Validate)

**User Behavior:**
- ✅ Newcomers prefer conversational interfaces (validated via user research)
- 🔄 Newcomers will trust AI guidance for on-chain actions (must validate during demo)
- 🔄 Multi-wallet linking is valuable for newcomers (assumption - may not be critical for MVP)

**Market:**
- ✅ Solana ecosystem growing (2024-2025 data confirms)
- 🔄 B2B partners willing to integrate experimental tech (2-3 LOIs needed to validate)
- ❌ Competition won't launch similar solution before hackathon (RISK - no control)

**Technical:**
- ✅ OpenAI can parse crypto-specific intents (validated via prototyping)
- ✅ Dynamic.xyz JWT auth reliable (already working)
- 🔄 Helius free tier sufficient for demo + early traction (must monitor usage)
- ✅ .NET 10 production-ready (Microsoft backing, stable preview)

**Monetization (Post-Hackathon):**
- 🔄 B2B partners will pay $500-2K/month (must validate via LOIs)
- 🔄 Unit economics positive at 10K+ users per partner (must instrument costs)
- ❌ Free tier sustainable long-term (RISK - likely need premium tiers)

---

## Risks & Mitigation

### Critical Risks (Hackathon)

1. **Time Crunch (HIGH IMPACT, MEDIUM LIKELIHOOD)**
   - **Risk:** 1 month insufficient to stabilize + add AI + Helius + demo
   - **Mitigation:**
     - Cut scope aggressively (AI chat only, skip policy engine)
     - Reuse existing code patterns (no new architecture)
     - Record demo video early (iterate until perfect)

2. **OpenAI Costs Overrun (MEDIUM IMPACT, MEDIUM LIKELIHOOD)**
   - **Risk:** Demo testing burns through $1.5K budget
   - **Mitigation:**
     - Implement caching for frequent queries
     - Use GPT-3.5 Turbo for testing, GPT-4 only for demo
     - Set hard token limits per request

3. **Technical Complexity (MEDIUM IMPACT, LOW LIKELIHOOD)**
   - **Risk:** AI integration introduces bugs, breaks existing functionality
   - **Mitigation:**
     - Keep AI as separate module (don't touch Identity/Chat)
     - Maintain 90%+ test coverage
     - Manual smoke tests before demo recording

4. **Competition Ships First (MEDIUM IMPACT, UNKNOWN LIKELIHOOD)**
   - **Risk:** Similar project launches during hackathon
   - **Mitigation:**
     - Focus on differentiation (multi-wallet + persistent identity, not just AI)
     - Emphasize code quality (architecture, tests, docs)
     - Build relationships with judges early (Twitter, Discord)

---

### Post-Hackathon Risks (Phase 2)

5. **Funding Rejection (HIGH IMPACT, MEDIUM LIKELIHOOD)**
   - **Risk:** Hackathon judges don't fund; no path to Phase 2
   - **Mitigation:**
     - Apply to multiple grants (Solana Foundation, Colosseum, SuperteamDAO)
     - Prepare investor pitch deck (show traction from hackathon demo)
     - Have Plan B: Open-source core, monetize consulting/custom integrations

6. **B2B Partner Acquisition (HIGH IMPACT, MEDIUM LIKELIHOOD)**
   - **Risk:** Can't find 5 partners willing to integrate
   - **Mitigation:**
     - Start outreach during hackathon (don't wait for funding)
     - Offer free integration support (first 5 partners)
     - Build public demo site (self-serve trial)

---

## Open Questions (To Answer During Hackathon)

### Must Answer Before Submission

1. **Q:** Which use case resonates most with judges?
   - A) Newcomer onboarding assistant
   - B) Multi-wallet identity manager
   - C) Conversational on-chain explainer
   - **Approach:** Test with 5-10 people, pick strongest narrative

2. **Q:** Should AI responses stream (real-time) or batch (faster to build)?
   - **Approach:** Prototype both, measure UX impact vs. time cost

3. **Q:** Is multi-wallet linking impressive enough for demo, or over-engineered?
   - **Approach:** User test with newcomers, validate if they understand value

---

### Can Defer to Phase 2

4. **Q:** What's the right B2B pricing model? (SaaS tiers, usage-based, hybrid)
5. **Q:** How granular should policy engine be? (per-user vs. global caps)
6. **Q:** Should SDK be open-source or proprietary?
7. **Q:** Which blockchain to expand to next? (Ethereum L2s, Polygon, Base)

---

## Next Steps (Week-by-Week Plan)

### Week 1: Stabilization Sprint
- [ ] **Days 1-2:** Identity module production hardening (error handling, tests)
- [ ] **Days 3-4:** Chat module production hardening (pagination, search)
- [ ] **Days 5-7:** Infrastructure (Redis caching, health checks, monitoring)

### Week 2: AI Integration Sprint
- [ ] **Days 8-10:** OpenAI service (context management, prompt templates)
- [ ] **Days 11-12:** Chat intelligence (AI-enhanced responses, streaming)
- [ ] **Days 13-14:** Cost controls (token limits, caching)

### Week 3: Helius Integration + Polish
- [ ] **Days 15-17:** Helius service (balance, history, verification)
- [ ] **Days 18-19:** Error enrichment (readable transaction failures)
- [ ] **Days 20-21:** API docs (OpenAPI/Swagger), integration guide

### Week 4: Demo Prep + Submission
- [ ] **Days 22-24:** Record demo video (multiple takes until perfect)
- [ ] **Days 25-26:** Write hackathon submission (problem/solution/roadmap)
- [ ] **Days 27-28:** Final testing, bug fixes, polish
- [ ] **Day 29:** Submit to Solana Hackathon
- [ ] **Day 30:** Buffer (unexpected issues)

---

## Conclusion

**Axon AI Backend** is a production-ready foundation for conversational on-chain experiences, built with best practices (Clean Architecture, CQRS, DDD) and excellent DX. With 60% complete (Identity + Chat modules), the remaining 40% focuses on stabilization, AI integration, and Helius enrichment to create a compelling Solana Hackathon demo.

**Immediate Goal (1 Month):** Win hackathon funding to unlock Phase 2 (Intent Layer, B2B SDK, Zero-Message Magic).

**Long-Term Vision:** Become the default backend infrastructure for Solana projects serving crypto newcomers—persistent identity, conversational AI, and guided on-chain experiences at scale.

---

**Document Status:** Grounded Reality (Validated Assumptions)
**Version:** 1.0
**Last Updated:** 2025-09-30
**Maintained By:** Valentyn Kit (Principal Software Engineer)

---

**Related Documentation:**
- [System Overview](../ENGINEERING/guides/architecture/system-overview.md) - Current architecture
- [Quick Reference](../ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md) - Code patterns
- [Identity Module](../ENGINEERING/modules/identity/00-INDEX.md) - Identity docs
- [Chat Module](../ENGINEERING/modules/chat/00-INDEX.md) - Chat docs
- [Getting Started](../ENGINEERING/guides/workflows/getting-started.md) - Local setup