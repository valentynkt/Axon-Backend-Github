# **Project Brief — Axon AI**

**Version:** 1.2 “Full-Spectrum”
**Date:** 08 August 2025
**Author:** Mary — Business Analyst

> **Purpose of this document**
> Provide every stakeholder—Founders, Product, Engineering, Design, Sales, Investors—with a single source of truth that fully answers **why** Axon AI should exist, **what** it must deliver in MVP, and **how** we will turn it into a category-leading business. The brief is intentionally exhaustive so the subsequent PRD can stay laser-focused on granular execution details.

---

## 1 Context & Opportunity

### 1.1 Macro Tailwinds

| Trend                                                 | Evidence                                                                     | Implication for Axon                                                                      |
| ----------------------------------------------------- | ---------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| **Solana outpaces EVM in retail throughput**          | 110 m+ monthly active addresses, 400 ms block times, 0.002 USD avg. fee      | Infrastructure exists for real-time, chat-driven trading; UX layer is the bottleneck.     |
| **Chat-native workflows “consumerise” complex tasks** | 82 % YoY growth in B2B chat assistants (Gartner, 2025)                       | Market primed to accept chat as the default command surface—even for high-stakes finance. |
| **MEV awareness reaches mainstream traders**          | **>60 %** of Solana trades now route through Jupiter’s protected feature set | A safety-first narrative resonates; early mover advantage is still available.             |

### 1.2 Market Sizing (Top-Down)

1. **Solana spot volume** averages **≈ \$156 B** per month (CoinGecko, trailing six-month mean).
2. **Active DeFi “power-users”** (\~2 % of wallets) generate \~55 % of volume.
3. If Axon captures **0.15 %** of routed volume via partner apps at a blended **5 bps fee-share**, annual gross revenue ≈ **\$14 M**.
4. Even a **10 % attach-rate** across 100 mid-tier Solana dApps (each 3-5 k DAU) yields a conservative **\$3–5 M ARR** in year 2.

*Takeaway:* A modest penetration yields venture-scale upside with minimal hardware or escrow risk.

### 1.3 Competitive Landscape

| Competitor           | Positioning                                  | Gaps Axon Exploits                                                                       |
| -------------------- | -------------------------------------------- | ---------------------------------------------------------------------------------------- |
| **Phantom Wallet**   | Top Solana wallet; limited “dApp browser” UX | No unified analysis, no MEV fallback error messaging, SDK focus lacking.                 |
| **Birdeye**          | Real-time charts & alerts                    | Research only; no execution, no chat, no API for white-label.                            |
| **Jupiter Terminal** | Best-price aggregator with MEV guard         | Execution only; users still leave to research; API is trade-centric not insight-centric. |
| **dexscreener.ai**   | LLM overlay on multi-chain data              | Lacks protected routing; EVM-first focus means low Solana depth; no B2B licensing story. |

*Differentiator:* **End-to-end flow** (insight → protected execution) *and* **Utility-as-a-Service** white-label model.

---

## 2 Problem Deep-Dive

### 2.1 User Pain Archeology

| Step in Current Journey | Pain                                                  | Consequence                                      |
| ----------------------- | ----------------------------------------------------- | ------------------------------------------------ |
| **Data Gathering**      | Flip between Birdeye, X/Twitter, Telegram alpha rooms | Delayed decision; information asymmetry vs. bots |
| **Token Verification**  | Copy/paste contract → manual checks in Solscan        | Fear of fat-finger loss; inhibits quick entry    |
| **Route Selection**     | Manual “compare” across AMMs → hope for no MEV        | Hidden slippage; eroded trust                    |
| **Execution**           | Sign in wallet, hope RPC isn’t congested              | Anxiety loop; lost alpha if network spikes       |

### 2.2 B2B Pain

| Persona            | Pain                 | Current Work-around      | Why It Fails                          |
| ------------------ | -------------------- | ------------------------ | ------------------------------------- |
| **Meme-Coin Team** | Need “utility” day-1 | Embed TradingView iframe | Feels generic; no stickiness          |
| **Analytics dApp** | Want AI overlay      | Hire ML contractor       | Slow + costly                         |
| **DeFi Protocol**  | Need chat support    | Use Discord bot          | No on-chain context; high maintenance |

*Core insight:* Each team *knows* they need smarter UX, but **build-time > market window**.

---

## 3 Solution Overview

> **Mission:** *Turn every Solana action—learn, decide, trade—into a frictionless chat exchange.*

### 3.1 System-of-One Sentence

“Ask Axon: *‘What moved SOL in the last hour?’* → get fused on-chain analytics → click **Buy 500 USD SOL** → receive MEV-safe swap receipt in the same chat.”

### 3.2 Architecture (High-Level)

```
┌──────────────┐    NL Query     ┌──────────────────┐
|  Frontend(s) | ───────────────▶| Axon REST API    |
└──────────────┘                 |  Layer          |
     ▲  ▲                        | • Parse & route |
     |  |  Protected Tx          | • Fetch data    |──┐
     |  └────────────────────────| • Compose JSON  |  |
     |                            └──────────────────┘  |
Wallet Sign                                     |       |
     |                                          ▼       |
┌──────────────┐        Liquidity    ┌──────────────────┐
|   User WAL   |◀────────────────────| Jupiter Router   |
└──────────────┘                     └──────────────────┘
                                             │ Tx Relay
                                             ▼
                                      ┌──────────────┐
                                      |  Helius RPC   |
                                      └──────────────┘
```

*Off-chain sentiment, DAO, proprietary risk engine plug into future horizontal layers.*

### 3.3 Key Functional Modules

1. **Intent Parser** — lightweight LLM prompt engineered for token extraction.
2. **Data Synthesiser** — merges price, volume, liquidity, holder-count from CoinGecko into “digestible English + tag blocs.”
3. **Action Mapper** — attaches `BUY/SELL/DETAILS` to tokens; returned as JSON hints.
4. **Route Oracle (client-side)** — fetches protected route from Jupiter; confirms status.
5. **Error Guardrail** — if `MEV_protection=false`, API returns `code: 503` with “Retry when protection available” UX copy.

---

## 4 User Personas & Journeys

### 4.1 Persona Snapshots

| Name                                     | “Job” To Be Done                         | Quote                        | Daily Tools                      | Success Signal                   |
| ---------------------------------------- | ---------------------------------------- | ---------------------------- | -------------------------------- | -------------------------------- |
| **“Nova” – Power-User Trader**           | Enter/exit memecoins before TikTok crowd | “Speed = edge.”              | Birdeye, Dexscreener, X, Excel   | Executes swap <15 s after signal |
| **“Val” – Meme-Coin Founder**            | Show token utility to retain holders     | “Chart alone isn’t utility.” | Canva, Telegram bot, simple site | Holders share new Axon widget    |
| **“Tori” – Retail Explorer** *(Phase 2)* | Safely buy first SOL bag                 | “I don’t trust DeFi tabs.”   | Coinbase, Reddit                 | Completes first swap w/out panic |

### 4.2 End-to-End Journey (Nova)

1. **Trigger** – sees whale address inflow to \$FOX on Solscan.
2. Opens Axon chat, types: “\$FOX volume spike legit?” ➜ API returns 3-line digest + `BUY` button.
3. Clicks **BUY 2 SOL FOX** ➜ wallet prompt ➜ protected route executed.
4. Axon posts confirmation + real PnL tracker card.
5. Nova tweets screenshot → organic word-of-mouth loop.

---

## 5 Go-to-Market Strategy

### 5.1 Launch Wave (Months 0-6)

| Work-Stream              | Tactics                                                                               | KPI                       |
| ------------------------ | ------------------------------------------------------------------------------------- | ------------------------- |
| **B2B Pilot Recruiting** | Hand-pick 10 meme-coin teams from Solana Hacker House alumni; offer 90-day free quota | 3–5 signed, paying pilots |
| **Showcase Growth**      | Incentivise power-users with referral leaderboard; giveaway Jupiter gas rebates       | 2 000 activated users     |
| **Thought Leadership**   | Weekly X threads: “Chat vs tabs series”, podcast guest spots                          | +5 k showcase waitlist    |

### 5.2 Marketing Stack

*HubSpot* for partner funnel → *Google Tag Manager* in showcase → *Segment* pipes to *Mixpanel* for activation tracking.

---

## 6 Business Model Details

| Revenue Stream            | Pricing Thought-Process                                | Target 12-Mo Split |
| ------------------------- | ------------------------------------------------------ | ------------------ |
| **B2B API Licences**      | Start at **\$0.02 / call** tiered; floor min. \$1 k/mo | 70 %               |
| **Trade Rev-Share**       | 2–5 bps share from Jupiter & Helius                    | 20 %               |
| **B2C Premium** (Phase 2) | Flat \$9.99/mo for multi-alert bots                    | 10 %               |

*Margins:* Gross 85 % (API infra + LLM usage primary COGS).

---

## 7 Success Metrics Framework

1. **North-Star** – *Daily Routed Volume via Partner Keys*
2. **Activation** – % users who press an action button in first session
3. **Depth** – Avg. trades per activated user / week
4. **Partner Health** – MAU retention curve of each pilot dApp
5. **Quality** – P95 API latency & protected-route success %

---

## 8 MVP Scope & Acceptance Criteria

### 8.1 Feature Checklist

| Must-Have                     | Criteria for “Done”                                              |
| ----------------------------- | ---------------------------------------------------------------- |
| `GET /analyze` endpoint       | Responds <3 s with: token, priceΔ24h, volume, liquidity, holders |
| Action tags                   | JSON field `actions:["BUY","SELL","DETAILS"]`                    |
| Wallet Connect (web showcase) | Phantom & Backpack deep-link                                     |
| Protected Trade Flow          | End-to-end SOL↔USDC successful on devnet, mainnet                |
| Error Guardrail               | 100 % cases return 503 if protection false                       |

### 8.2 Out-of-Scope

Off-chain sentiment, DAO tokenomics, LP optimisation, regulatory KYC suite.

---

## 9 Risk Register (Expanded)

| Risk                                       | Likelihood | Impact | Owner        | Mitigation                                          |
| ------------------------------------------ | ---------- | ------ | ------------ | --------------------------------------------------- |
| CoinGecko API quota burst                  | Medium     | Medium | Backend Lead | Paid tier + Redis cache; auto-switch to stale cache |
| Rapid Solana fee market change             | Low        | Medium | Product      | Monitor gov-proposal 75; prep dynamic fee buffer    |
| LLM cost spike (OpenAI)                    | Medium     | Low    | Finance      | Token-level caching; explore Anthropic backup       |
| Brand confusion vs. Axon neuroscience SaaS | Low        | Low    | Marketing    | SEO + trademark filing Q4                           |

---

## 10 Roadmap (18-Month Horizon)

| Phase            | Theme                    | High-Level Deliverables                            |
| ---------------- | ------------------------ | -------------------------------------------------- |
| **α (0-6 mo)**   | **“Prove Value”**        | MVP, 5 pilots, showcase launch                     |
| **β (6-12 mo)**  | **“Deepen Insight”**     | Off-chain sentiment, alert bots, scam-token engine |
| **γ (12-18 mo)** | **“Ecosystem Flywheel”** | SDK, widget marketplace, rev-share agreements      |

---

## 11 Budget & Resource Plan

| Category        | Allocation (6 mo) | Notes                              |
| --------------- | ----------------- | ---------------------------------- |
| Engineering     | \$320 k           | 3 FT + 2 contractors               |
| Cloud & API     | \$45 k            | Azure, OpenAI, CoinGecko paid tier |
| GTM & Community | \$60 k            | Events, KOL incentives             |
| Misc Ops        | \$25 k            | Legal, accounting                  |

Runway: **10 months** post-seed (assuming \$1.6 M raise closes Sept 2025).

---

## 12 Open Questions to Resolve in PRD

1. Finalise **API rate-limit tiers** for pilot contracts.
2. Decide **LLM vendor fallback** (Anthropic vs. Mistral).
3. Choose **design system** (Tailwind vs. custom) for showcase.
4. Lock **incident-response SLA** aligned with partner expectations.

---

## 13 Decision Log & Approvals

| Decision             | Owner | Date      | Status |
| -------------------- | ----- | --------- | ------ |
| On-chain-only scope  | CEO   | 06 Aug 25 | ✅      |
| MEV fallback = error | CTO   | 07 Aug 25 | ✅      |
| User research skip   | CPO   | 07 Aug 25 | ✅      |
| DAO postponed        | Board | 07 Aug 25 | ✅      |

---

### 🔒 Ready for PRD Handoff

All strategic, functional and commercial parameters are now defined with sufficient depth. The Product team may proceed to decompose this brief into epics, user stories and engineering tasks.
