# **The MVP Feature Blueprint: The Solana Co-Pilot**

**Version:** 2.0 (Definitive Edition)
**Date:** August 24, 2025
**Purpose:** To provide a detailed, comprehensive, and actionable specification for the Minimum Viable Product (MVP) of Axon AI. This document will serve as the direct and primary input for creating the technical PRD and architecture.

## **1. The MVP Mission & Core Hypotheses**

The mission of this MVP is to ship a functional, high-value AI Co-Pilot that achieves two primary goals:
1.  **Validate Core Product Hypotheses:** Prove that a conversational, intent-driven interface can dramatically simplify the on-chain user experience for our target "Nova" persona.
2.  **Establish Market Viability:** Demonstrate a compelling "Utility-as-a-Service" offering that is attractive to B2B partners and establishes a clear path to monetization and future growth, making it an exciting proposition for the Solana Hackathon and VCs.

We will measure success by validating these core hypotheses:
* **Hypothesis 1 (UX):** A conversational interface can reduce the time and cognitive load of a standard research-to-execution workflow by over 50%.
* **Hypothesis 2 (B2B):** A simple, intent-based API is a compelling enough value proposition to secure letters of intent from at least 3-5 pilot B2B partners.
* **Hypothesis 3 (Moat):** A persistent, intelligent on-chain identity creates a sticky user experience, demonstrated by a high week-2 retention rate for our initial alpha testers.

## **2. The Two Pillars of the Solana Co-Pilot**

The MVP is architected around two foundational, synergistic pillars. The **Intelligence Layer** provides the context-aware "memory," and the **Action Layer** provides the intent-driven "hands." This structure ensures that every action is informed by deep user insight.

---

## **Pillar I: The Intelligence Layer (The "Memory")**

This is our proprietary data moat and the core of our competitive advantage. It solves the critical personalization and context problem that plagues Web3 applications.

### **Feature 1.1: On-Chain Identity & Persistent Profile**

* **User Story:** "As a Solana power user ('Nova'), I want to securely connect my wallet so that Axon remembers my entire history, preferences, and context, providing a continuous and personalized experience across any dApp I use it in."
* **Detailed Functionality:**
    * **Non-Custodial Authentication:** The user authenticates by signing a simple, EIP-4361 compliant "Sign-In with Solana" message. This is a secure, non-custodial process that asserts ownership without granting any permissions.
    * **Profile Architecture:** A persistent, encrypted user profile is created in our database, keyed to the user's public wallet address. This profile will be architected from day one to store:
        * Full, indexed conversation history.
        * Explicitly defined user preferences (e.g., default slippage tolerance, risk profile, preferred DEXs).
        * AI-inferred insights (e.g., "user is a meme coin trader," "user is sensitive to gas fees").
        * Data required for our "Data Sovereignty" feature (export/delete functionality).
* **Technical & Strategic Notes:**
    * This is the foundation of our network effect. The more users and B2B partners we have, the richer the contextual data becomes, making the Co-Pilot smarter for everyone.
    * Security is paramount. All sensitive data will be encrypted at rest and in transit.
* **MVP Success Criteria:** A user can have a detailed conversation, set a preference (e.g., "always use 0.5% slippage"), close the session, and have Axon recall both the conversation context and the preference in a new session a day later.

### **Feature 1.2: Instant On-Chain Snapshot (The "Wow" Moment)**

* **User Story:** "As a new user, I want Axon to instantly understand my on-chain identity and activity so that it can provide immediate, 'magical' value without a lengthy onboarding process."
* **Detailed Functionality:**
    * On first sign-in, Axon immediately triggers a backend process to fetch the user's complete transaction history using the **Helius Enhanced Transactions API**.
    * Our proprietary analysis engine will parse this history to generate a "First Contact" intelligence report, identifying:
        * **P&L Analysis:** Top 5 most profitable and least profitable trades.
        * **Protocol Affinity:** Most frequently used DeFi protocols (e.g., Jupiter, Marinade).
        * **Token Profile:** Top 5 most held and traded SPL tokens.
        * **NFT Activity:** Notable NFT collections held or traded.
    * The Co-Pilot will proactively present these insights in the chat, creating a powerful "wow" moment: "Welcome! I've analyzed your on-chain activity. It looks like you made a 15x return on $WIF. Would you like to see a full breakdown of your trading history?"
* **Technical & Strategic Notes:**
    * This feature is our primary hook for user acquisition and a key demo for the hackathon. It immediately proves our value proposition.
    * Our ability to quickly and accurately parse this data is a core competency.
* **MVP Success Criteria:** Within 30 seconds of connecting their wallet, a new user is presented with at least one accurate, non-obvious, and valuable insight derived from their on-chain history.

### **Feature 1.3: Multi-Wallet Personality & Linking**

* **User Story:** "As a sophisticated user with a 'degen' wallet, a 'main' wallet, and a 'farming' wallet, I want to securely link them to a single Axon profile so that I can get a holistic view of my on-chain presence without compromising my security."
* **Detailed Functionality:**
    * A simple, secure UI flow within the chat to "Link another wallet."
    * The user will be prompted to sign a message with the new wallet (proving ownership), and then sign a confirmation message with their main wallet to authorize the link.
    * Axon's intelligence layer will then be able to analyze and act upon the combined context of all linked wallets, while still allowing the user to specify which wallet to use for a given action.
* **Technical & Strategic Notes:**
    * This demonstrates a deep, empathetic understanding of our "Nova" ICP's operational security practices.
    * The database schema must support a one-to-many relationship between a primary user profile and multiple linked wallet addresses.
* **MVP Success Criteria:** A user can link a secondary wallet and successfully ask a query like, "What is my total combined balance of USDC across all my linked wallets?" and receive a correct, aggregated answer.

---

## **Pillar II: The Action Layer (The "Hands")**

This is our execution engine and the core of our "Utility-as-a-Service" B2B offering. It turns the intelligence from Pillar I into safe, efficient, and verifiable on-chain actions.

### **Feature 2.1: The Unified `/intent` Endpoint**

* **B2B Customer Story:** "As a dApp developer, I want a single, dead-simple API endpoint to send my user's natural language requests so that I can integrate a powerful AI layer in hours, not months."
* **Detailed Functionality:**
    * A single, secure API endpoint: `POST /v1/intent`.
    * The request body will be simple and robust: `{ "text": "User's raw text input", "userWallet": "User's public key", "conversationId": "optional_id_for_threading" }`.
    * The API will handle all backend complexity: intent parsing, data fetching, on-chain simulation, and response structuring.
    * The JSON response will be equally simple and powerful, containing a structured `actionPlan` for any executable intent.
* **Technical & Strategic Notes:**
    * This is the cornerstone of our "Build for Builders" principle. The simplicity of this API is a massive competitive advantage.
    * Authentication will be handled via API keys issued to our B2B partners.
* **MVP Success Criteria:** A partner developer can use a tool like Postman to send a text query to the API and receive a structured, actionable response, demonstrating the end-to-end flow.

### **Feature 2.2: The Safety-First Execution Engine**

* **User Story:** "As a user, before I sign any transaction, I want to see a clear, human-readable summary of exactly what will happen so that I can act with confidence and avoid costly mistakes or scams."
* **Detailed Functionality:**
    * For any intent that requires an on-chain action, Axon will **always** perform a transaction simulation first using services like Helius.
    * The `actionPlan` returned by the `/intent` API will contain:
        * A `summary` string in plain English (e.g., "You are about to swap 10 SOL for 15,000 BONK via Jupiter. This transaction is MEV-protected.").
        * The raw, unsigned `transaction` object, ready to be passed to the user's wallet for signing.
        * A `humanReadableDetails` object breaking down key transaction effects (e.g., balance changes, permissions granted).
        * Clear error handling: If a simulation fails, the API will return a descriptive error explaining *why* it failed (e.g., "Simulation failed due to insufficient funds," "This token has a high slippage risk").
* **Technical & Strategic Notes:**
    * This feature directly embodies our "Safety is the Engine" principle and is a core trust-building mechanism.
* **MVP Success Criteria:** A user attempting a swap is presented with a clear summary and only has to perform one action (a single click/signature) to approve and execute the trade. A user attempting a trade that would fail on-chain is shown a clear, understandable error message *before* they sign anything.

### **Feature 2.3: Foundational On-Chain Action Primitives**

* **User Story:** "As a user, I want to perform the most common on-chain tasks—like swapping, sending, and setting up a DCA—through simple, intuitive conversation."
* **Detailed Functionality:** The intent parser will be trained to recognize and the backend will be architected to execute the following high-value actions for the MVP:
    * **Swaps:** "Swap X for Y." Deep integration with the **Jupiter API** to leverage their best-in-class routing and MEV protection.
    * **Transfers:** "Send X to Y." Standard SPL token and SOL transfers.
    * **Scheduled DCA:** "Buy X of Y every Z." Integration with a reliable on-chain scheduling protocol like **Clockwork** or an equivalent.
* **Technical & Strategic Notes:**
    * This demonstrates our strategy of leveraging the best-in-class existing protocols, allowing us to deliver immense value without reinventing the wheel. Our IP is the orchestration, not the underlying primitives.
* **MVP Success Criteria:** The system can successfully parse a natural language request, generate a valid transaction plan, and guide the user through execution for each of the three primitive actions.

## **4. Explicitly Out of Scope for MVP**

To ensure we can "Ship at Lightspeed", the following features are explicitly **OUT OF SCOPE** for the initial MVP, despite their value:

* **Proactive Monitoring & Alerts:** The MVP will operate on a request-response model. This is a strategic choice to reduce initial architectural complexity. Proactive, server-pushed notifications will be a top-priority fast-follow feature.
* **Complex Multi-Step Strategies:** Intents that require complex, multi-protocol workflows (e.g., "Find the best yield farm, deposit my USDC, and auto-compound the rewards") are post-MVP. We will master the primitives first.
* **Full Data Sovereignty Dashboard:** While the backend APIs for data export and deletion (`GET /v1/profile/export`, `DELETE /v1/profile/erase`) will be built to fulfill our Manifesto promise, the user-facing web dashboard for managing this data is post-MVP.

---
