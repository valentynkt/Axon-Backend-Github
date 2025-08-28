# Axon AI MVP Feature Blueprint

**Version:** 2.0 Consolidated  
**Date:** August 28, 2025  
**Source:** Extracted from The MVP Feature Blueprint

## MVP Mission & Hypotheses

### Core Mission
Ship a functional AI Co-Pilot that validates product hypotheses and establishes market viability for the Solana ecosystem.

### Success Hypotheses
1. **UX Hypothesis**: Conversational interface reduces research-to-execution workflow time by 50%+
2. **B2B Hypothesis**: Intent-based API secures 3-5 pilot B2B partner commitments  
3. **Moat Hypothesis**: Persistent on-chain identity creates sticky user experience with high week-2 retention

## Two-Pillar Architecture

### Pillar I: Intelligence Layer ("Memory")
Proprietary data moat providing context-aware personalization

### Pillar II: Action Layer ("Hands")  
Execution engine turning intelligence into safe on-chain actions

---

## Intelligence Layer Features

### Feature 1.1: On-Chain Identity & Persistent Profile

**User Story**: "As Nova, I want Axon to remember my entire history and preferences across any dApp"

**Core Functionality**:
- Non-custodial authentication via "Sign-In with Solana" (EIP-4361 compliant)
- Encrypted persistent profile keyed to wallet address
- Stores conversation history, user preferences, and AI-inferred insights
- Foundation for network effect and data sovereignty

**MVP Success Criteria**: User sets preference, returns day later, Axon recalls both context and preference

### Feature 1.2: Instant On-Chain Snapshot ("Wow Moment")

**User Story**: "As new user, I want immediate magical value without lengthy onboarding"

**Core Functionality**:
- Immediate transaction history analysis via Helius Enhanced Transactions API
- "First Contact" intelligence report identifying:
  - P&L analysis (top 5 profitable/unprofitable trades)
  - Protocol affinity (most used DeFi protocols)
  - Token profile (top held/traded SPL tokens)  
  - NFT activity (notable collections)
- Proactive insight presentation in chat

**MVP Success Criteria**: Within 30 seconds, new user receives accurate, valuable, non-obvious insight

### Feature 1.3: Multi-Wallet Personality & Linking

**User Story**: "As sophisticated user with multiple wallets, I want unified view without compromising security"

**Core Functionality**:
- Secure wallet linking flow with dual-signature confirmation
- Holistic analysis across all linked wallets
- Wallet-specific action specification capability
- One-to-many database relationship support

**MVP Success Criteria**: User links secondary wallet, successfully queries aggregated data across wallets

---

## Action Layer Features

### Feature 2.1: Unified `/intent` Endpoint

**B2B Story**: "As dApp developer, I want single API to integrate powerful AI layer in hours"

**Core Functionality**:
- Single endpoint: `POST /v1/intent`
- Simple request: `{ "text": "...", "userWallet": "...", "conversationId": "..." }`
- Handles: intent parsing, data fetching, simulation, response structuring
- Returns structured `actionPlan` for executable intents
- API key authentication for B2B partners

**MVP Success Criteria**: Partner developer uses Postman to get structured, actionable response

### Feature 2.2: Safety-First Execution Engine

**User Story**: "I want clear summary of transaction outcome before signing anything"

**Core Functionality**:
- Always-on transaction simulation via Helius before execution
- Returns in `actionPlan`:
  - Plain English summary
  - Unsigned transaction object ready for wallet
  - Human-readable details breakdown
  - Clear error explanations for failed simulations
- Embodies "Safety is the Engine" principle

**MVP Success Criteria**: User gets clear summary, executes with single signature; failed transactions show understandable errors before signing

### Feature 2.3: Foundational On-Chain Primitives

**User Story**: "I want to perform common on-chain tasks through simple conversation"

**Core Functionality**:
- **Swaps**: Jupiter API integration with best routing and MEV protection
- **Transfers**: Standard SPL token and SOL transfers  
- **Scheduled DCA**: Integration with on-chain scheduling (Clockwork or equivalent)
- Leverages best-in-class existing protocols
- Focus on orchestration IP, not primitive reinvention

**MVP Success Criteria**: Successfully parse, plan, and execute each of the three primitive actions via natural language

---

## Explicitly Out of Scope

**Strategic Exclusions for MVP Speed**:

### Post-MVP Features
- **Proactive Monitoring & Alerts**: MVP uses request-response model only
- **Complex Multi-Step Strategies**: Focus on mastering primitives first  
- **Full Data Sovereignty Dashboard**: Backend APIs built, UI dashboard later

### Fast-Follow Priorities  
1. Server-pushed proactive notifications
2. Multi-protocol complex workflows
3. Comprehensive data management dashboard
4. Advanced trading strategies and automation

---

## Technical Architecture Implications

### Intelligence Layer Requirements
- Encrypted profile storage with wallet-keyed access
- Helius integration for transaction history analysis
- Multi-wallet relationship management
- Real-time conversation context persistence

### Action Layer Requirements  
- Jupiter API integration for swap execution
- Helius simulation capabilities
- On-chain scheduling protocol integration
- Robust error handling and user feedback

### B2B API Requirements
- RESTful `/intent` endpoint design
- API key authentication and rate limiting
- Structured response formats for partner integration
- Clear documentation and developer experience

## Success Metrics

### User Validation
- Time reduction in research-to-execution workflows (target: 50%+)
- Week-2 retention rates for alpha testers
- Accuracy of insights and predictions

### B2B Validation
- Number of pilot partner commitments (target: 3-5)
- API adoption and usage metrics
- Developer satisfaction and integration speed

### Technical Validation
- Transaction success rates and simulation accuracy
- Response time performance under load  
- Security audit results and compliance verification