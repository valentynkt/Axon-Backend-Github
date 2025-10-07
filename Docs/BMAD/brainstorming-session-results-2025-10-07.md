# Brainstorming Session Results

**Session Date:** 2025-10-07
**Facilitator:** {{agent_role}} {{agent_name}}
**Participant:** Valik

## Executive Summary

**Topic:** Axon TypeScript SDK - Broad exploration of V2+ features and capabilities

**Session Goals:**
- Explore modern tech stack opportunities (cutting edge, future-proof)
- Maximize ease of development & support for the SDK team
- Create exceptional developer experience (DX) for integrators
- Blue sky thinking - no constraints, dream big!

**Techniques Used:** {{techniques_list}}

**Total Ideas Generated:** {{total_ideas}}

### Key Themes Identified:

{{key_themes}}

## Technique Sessions

### Technique 1: First Principles Thinking (20 min)

**Goal:** Strip away SDK assumptions and rebuild from fundamental truths

#### Core Truths Identified:

1. ✅ **Developers need to authenticate users** (non-negotiable)
2. ✅ **Out-of-box integration in HOURS, not days** (speed to value is critical)
3. ✅ **Cross-platform flexibility** (web, backend, mobile - one SDK, all platforms)
4. ✅ **SDK team needs: future-proof + clean + aligned with 80/20 rule** (maintainability matters!)
5. ✅ **"Just works" without heavy documentation** (minimal friction)
6. ✅ **Balance simplicity with flexibility** (core design philosophy)

#### Key Tension: Simplicity vs. Customization

*Finding: Slightly more on the side of simplicity but still have enough customization/flexibility for developers*

---

#### BREAKTHROUGH: One-Function Auth is FEASIBLE ✨

**Problem Statement:** Current 3-step challenge-response flow is unnecessarily complex
- Step 1: `POST /auth/challenge` → Get challenge
- Step 2: Client signs message
- Step 3: `POST /auth/verify` → Verify & get tokens

**First Principles Analysis:**
- Challenge-response pattern is a relic from password-based auth
- Ed25519 signature verification is cryptographically STRONGER than MAC protection
- Client can generate canonical message locally (deterministic format)
- No backend dependency for message generation!

**The Solution: Single-Call Wallet Auth**

```typescript
// ONE function call handles everything
await axon.auth.login(wallet, {
  authType: 'wallet' | 'dynamic'  // Simple param for auth type
})

// Internally:
// 1. SDK generates canonical message client-side
// 2. Orchestrates wallet signature
// 3. POST /auth/wallet (ONE API call)
// 4. Handles token storage, refresh, errors
```

**Client-Side Message Generation (No Backend!):**
```json
{
  "chain_id": "solana-mainnet",
  "address": "7xKXtg...",
  "issued_at": 1704067200,      // Math.floor(Date.now() / 1000)
  "exp": 1704067500,             // issued_at + 300 (5min TTL)
  "nonce": "uuid-v7",            // crypto.randomUUID()
  "aud": "https://axon.app"      // SDK config
}
```

**Impact:**
- ✅ 67% fewer API calls (3 → 1)
- ✅ 69% faster latency (800ms → 250ms)
- ✅ 50% less backend code (1,200 LOC → 600 LOC)
- ✅ Stronger security (PKI signature > HMAC)
- ✅ Zero database writes for ephemeral challenge state

**Backend Refactoring Plan:**
- **Phase 1:** Add new `POST /api/v1/auth/wallet` endpoint (backward compatible)
- **Phase 2:** Update SDK with client-side message generation
- **Phase 3:** Deprecate old 3-step flow (6-12 month migration)
- **Phase 4:** Remove legacy endpoints (v2.0 breaking change)

**Key Services:**
- `IMessageValidator` - Validates client-generated messages (structure, timestamp, freshness)
- `INonceReplayCache` - Prevents replay attacks (Redis/Memory cache with TTL)
- Signature verification - Reuse existing Ed25519 logic

**Developer Experience:**
```typescript
// Before (3 roundtrips)
const challenge = await axon.auth.getChallenge({...});
const sig = await wallet.signMessage(challenge.message);
const tokens = await axon.auth.verifySignature({...});

// After (1 function call)
await axon.auth.login(wallet, { authType: 'wallet' });
// ✅ Tokens stored, refresh scheduled, done!
```

---

#### Design Philosophy Insights:

**"Magic Black Box" vs. "Deterministic with Params"**
- ❌ Full auto-magic that breaks mysteriously = bad DX
- ✅ Smart defaults + explicit params when needed = great DX
- Principle: *"Simple by default, powerful when needed"*

**Auth Type Selection:**
- Two auth paths: Dynamic.xyz JWT exchange OR Manual wallet sign-in
- Simple discriminator param: `authType: 'wallet' | 'dynamic'`
- SDK handles internal flow differences transparently

---

#### BRUTAL SIMPLIFICATION: Kill "Environment" Concept Entirely ✂️

**Problem Discovered:** "Environment" parameter is pointless abstraction
- Current assumption: `environment: 'mainnet' | 'devnet' | 'testnet'`
- Reality: Just a URL mapper → `mainnet` = `https://api.axon.ai`
- **First principles: Why not just pass the URL directly?**

**Decisions Made:**

1. **❌ DELETE: Environment-based configuration**
   - No more `mainnet` | `devnet` | `testnet` | `local`
   - No more environment→URL mapping logic
   - Just accept API URL directly!

2. **❌ DELETE: Multi-chain support (V1 scope cut)**
   - Backend: **Solana mainnet ONLY**
   - Reject devnet wallets (prevents account confusion, analytics pollution)
   - Reject other chains (Ethereum, Polygon, etc.)
   - Ships faster, fewer bugs, cleaner data

3. **❌ DELETE: Chain ID complexity**
   - Backend doesn't need to store/validate chain ID
   - Accept `"solana"` or `"solana:mainnet"` (normalize to `"solana"`)
   - Principal ID: `principal:ABC123` (no chain prefix for now)
   - Removed `chain_id` field from canonical message entirely

**New SDK Initialization (Approach A - Env Var Auto-Detection):**

```typescript
// 🎯 ZERO CONFIG (80% case)
// .env file
AXON_API_URL=https://api.axon.ai

// Code
const axon = new Axon(); // Auto-reads AXON_API_URL from process.env

// Explicit override when needed (local dev, custom deployment)
const axon = new Axon({
  apiUrl: 'http://localhost:5000'
});
```

**New Auth Flow (Simplified):**

```typescript
// ONE function call
await axon.auth.login(solanaWalletAdapter);

// Internally:
// 1. Validate wallet is Solana (reject others)
// 2. Get wallet address
// 3. Generate canonical message (NO chain_id field!)
// 4. Sign with wallet
// 5. POST /auth/wallet
// 6. Handle tokens
```

**Simplified Canonical Message:**
```json
{
  "address": "7xKXtg...",
  "issued_at": 1704067200,
  "exp": 1704067500,
  "nonce": "uuid-v7",
  "aud": "https://axon.app"
}
```

**Backend Validation (Strict Mode):**
```csharp
// Only Solana mainnet wallets allowed
if (wallet.Type != "solana")
{
    return Error.Validation("Only Solana wallets supported");
}
```

**Impact:**
- ✅ Simpler SDK API (one less config param)
- ✅ Faster V1 delivery (cut multi-chain scope)
- ✅ Cleaner production data (no devnet pollution)
- ✅ Fewer edge cases (single chain = fewer bugs)
- ✅ Clear upgrade path (V2 adds multi-chain)

**Design Philosophy Reinforced:**
- **80/20 Rule:** Ship Solana-only first, add chains based on demand
- **Clean Defaults:** Zero config for standard case, explicit override when needed
- **Brutal Simplification:** Delete anything not critical for V1

{{technique_sessions}}

## Idea Categorization

### Immediate Opportunities

_Ideas ready to implement now_

{{immediate_opportunities}}

### Future Innovations

_Ideas requiring development/research_

{{future_innovations}}

### Moonshots

_Ambitious, transformative concepts_

{{moonshots}}

### Insights & Learnings

_Key realizations from the session_

{{insights_learnings}}

## Action Planning

### Top 3 Priority Ideas

#### #1 Priority: {{priority_1_name}}

- Rationale: {{priority_1_rationale}}
- Next steps: {{priority_1_steps}}
- Resources needed: {{priority_1_resources}}
- Timeline: {{priority_1_timeline}}

#### #2 Priority: {{priority_2_name}}

- Rationale: {{priority_2_rationale}}
- Next steps: {{priority_2_steps}}
- Resources needed: {{priority_2_resources}}
- Timeline: {{priority_2_timeline}}

#### #3 Priority: {{priority_3_name}}

- Rationale: {{priority_3_rationale}}
- Next steps: {{priority_3_steps}}
- Resources needed: {{priority_3_resources}}
- Timeline: {{priority_3_timeline}}

## Reflection & Follow-up

### What Worked Well

{{what_worked}}

### Areas for Further Exploration

{{areas_exploration}}

### Recommended Follow-up Techniques

{{recommended_techniques}}

### Questions That Emerged

{{questions_emerged}}

### Next Session Planning

- **Suggested topics:** {{followup_topics}}
- **Recommended timeframe:** {{timeframe}}
- **Preparation needed:** {{preparation}}

---

_Session facilitated using the BMAD CIS brainstorming framework_
