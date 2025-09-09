Project Brief from Analyt:
Here is the final Project Brief for the Axon AI Identity & Memory service.

## **Project Brief: Axon AI - Identity & Memory**

### **1. Executive Summary**

This project will create a canonical user identity layer for Axon AI, called the **Principal**. The goal is to provide a stable, cross-credential identity for each user, associating multiple authentication methods (like a Dynamic JWT or Sign-In with Solana) and various on-chain wallets to this single Principal. The system maintains a minimal "memory" for each user, consisting only of a default wallet for each blockchain and a single, global risk posture (conservative, balanced, or aggressive). This allows the main Axon AI product to act automatically and intelligently on the user's behalf.

This functionality is delivered through a minimal and secure API surface of just two endpoints:
* **`POST /auth/exchange`**: This is the primary endpoint for identity resolution. It accepts a credential, such as a Dynamic JWT, and idempotently creates or updates the Principal, links their verified wallets, and applies default settings. It enforces a critical non-negotiable rule: a wallet address on a specific chain can be owned by exactly one Principal across the entire system to prevent ownership conflicts.
* **`GET /auth/me`**: A simple, read-only endpoint that provides the frontend with a snapshot of the user’s current identity, including their Principal ID, wallets, defaults, and risk posture, enabling the product to act immediately.

The architecture is deliberately stateless, relying on the authentication provider's token for every request and avoiding server-issued access tokens or cookies. This ensures security, simplicity, and makes the system future-proof for adding new authentication providers without changing the core design.

---
### **2. Problem Statement**

For the Axon AI Co-Pilot to deliver on its promise of intelligent, automated action, it requires a foundational understanding of who the user is. The absence of a dedicated identity layer creates several critical, multi-faceted problems:

* **The "Ghost User" Problem (Lack of a Canonical Identity)**
    * **Business Impact**: The core value proposition of an automated "Co-Pilot" fails. Without memory, the AI has amnesia in every session, making it impossible to offer proactive, personalized insights or build a trusted relationship with the user. This undermines the product's competitive moat.
    * **User Experience Impact**: The user is forced to re-establish their context in every session. The experience feels generic, repetitive, and devoid of the "magical" intelligence that should define the product, leading to high friction and low user retention.

* **The "Split Personality" Problem (Fragmented User Representation)**
    * **Cross-Credential Conflict**: A user is not a single credential. As stated in the business requirements, a user might authenticate via a social login (Dynamic) today and a direct wallet signature (SIWS) tomorrow. Without a unifying Principal, the system would incorrectly treat them as two different people, completely fracturing their experience and the AI's understanding of their on-chain history.
    * **Ownership Ambiguity & Security Risk**: The brief specifies a non-negotiable rule that a wallet can only be owned by one Principal. The underlying problem is that without this, the system could allow two different credentials to claim the same wallet, creating a severe identity conflict. A "silent reassignment" of a wallet could enable one user to hijack another's identity and memory within the Axon ecosystem, which is an unacceptable security risk.

* **The "Over-Engineering" Problem (Unnecessary Complexity)**
    * **Increased Attack Surface & Maintenance Cost**: Standard identity solutions often rely on server-issued tokens, cookies, and complex state management. This approach is not only difficult and costly to build and maintain, but it also significantly increases the system's attack surface.
    * **Data Bloat & Privacy Liability**: The brief deliberately excludes free-form user profiles and metadata. The problem being avoided is collecting and persisting data that is not essential for the product's automated functions. This "data bloat" creates a privacy liability and a maintenance burden with no corresponding business value.

---
### **3. Proposed Solution**

The proposed solution is a stateless, secure, and minimalist identity layer designed around a central concept: the **Principal**.

* **Core Concept**: Each user will be represented as a canonical **Principal**. This Principal acts as a unifying anchor that links together multiple authentication **Credentials** (e.g., a Dynamic JWT, a future SIWS signature) and multiple on-chain **Wallets**. The system will persist a minimal "memory" for each Principal, consisting only of a default wallet per-chain and a single global **Risk Posture**.

* **Core Approach**: The entire identity service will be accessible through a minimal API surface of just two endpoints:
    1.  **`POST /auth/exchange`**: An idempotent endpoint that serves as the single point of entry for creating, updating, and linking a user's identity based on a provided credential.
    2.  **`GET /auth/me`**: A simple, read-only endpoint that provides a snapshot of the Principal's current state for the frontend to consume.

* **Key Differentiators**:
    * **Stateless Architecture**: The solution deliberately avoids server-issued access/refresh tokens and cookies. Every API request is authenticated using the credential itself (e.g., the Dynamic JWT), which simplifies the architecture and reduces the attack surface.
    * **Web3-Native Logic**: The system is built on a "verified-first" principle for wallet ownership and a non-negotiable rule of global wallet uniqueness, solving the complex, Web3-specific problem of cross-credential identity resolution.
    * **Minimalism (KISS)**: The solution strictly adheres to persisting only the data required for the product to function, avoiding the complexity and privacy risks of storing free-form user profiles or metadata.

---
### **4. Target Users**

For this foundational service, there are two distinct user segments to consider: the direct consumer of the API (the developer) and the indirect beneficiary whose experience it enables (the end-user).

* **Primary User Segment: The Integrating Developer / Frontend Application**
    * **Profile**: A frontend or full-stack developer building the Axon AI Co-Pilot application or, in the future, a B2B partner's application.
    * **Needs & Pains**: They need a dead-simple, reliable, and secure way to handle user identity without building it from scratch. Their primary pain is the immense complexity, time-cost, and security risk associated with creating a stateful, multi-credential identity system.
    * **Goals**: To integrate a robust identity layer with a minimal API surface in hours, not weeks, allowing them to focus on building the core user-facing features of the Co-Pilot.

* **Secondary User Segment: The End-User ("Nova")**
    * **Profile**: The "Nova" persona—a sophisticated Web3 power user who values efficiency and security.
    * **Behaviors**: This user operates with multiple wallets for different purposes (e.g., a "degen" wallet, a "main" wallet) and will expect to authenticate through various methods (e.g., social logins, direct wallet signatures) over the product's lifetime.
    * **Goals**: To have a seamless and intelligent experience where the Axon Co-Pilot instantly recognizes them as a single, consistent person, regardless of which wallet they connect or how they log in.

---
### **5. Goals & Success Metrics**

The success of the Identity & Memory service will be measured by its ability to deliver on its core promises of simplicity, security, and reliability.

* **Business Objectives**
    * **Implement a Foundational Identity Layer**: Successfully build and launch a service that provides a single, canonical "Principal" for each user, unifying their identity across multiple credentials and wallets.
    * **Enforce Simplicity and Security**: Deliver a minimal API surface (two endpoints) that is stateless and adheres strictly to all "Non-negotiable rules," particularly global wallet uniqueness and no silent reassignments.
    * **Ensure Future-Proof Extensibility**: Create a provider-agnostic system that can support new authentication methods (like SIWS) and additional blockchains in the future without requiring changes to the core API endpoints.

* **User Success Metrics**
    * The primary measure of success is the **full completion of the Acceptance Checklist** provided in the business feature brief. This includes:
        * A new user can successfully perform an `exchange` and have their Principal, wallets, defaults, and risk posture correctly created.
        * Repeating the `exchange` operation is proven to be idempotent.
        * The `GET /auth/me` endpoint correctly returns the user's snapshot with a functional ETag for caching.
        * Cross-credential linking works as designed, and ownership conflicts are correctly surfaced as a `409` error.

* **Key Performance Indicators (KPIs)**
    * **API Health & Reliability**:
        * API uptime and latency for `/auth/exchange` and `/auth/me`.
        * Success vs. failure rate of `exchange` operations.
        * Rate of business-level errors, specifically `409` (wallet conflict) and `401` (invalid token).
    * **System Performance & Efficiency**:
        * ETag hit/miss ratio for `GET /auth/me` to measure caching effectiveness.
        * Rate limiting metrics (e.g., requests per minute per IP) to monitor and prevent abuse.
    * **Business & Adoption Metrics**:
        * Count of new Principals created.
        * Counts of wallets processed, linked, skipped, and conflicted during the exchange process.

---
### **6. MVP Scope**

The scope for this MVP is tightly defined, focusing on delivering a minimal, complete, and secure service.

* **Core Features (Must Have)**
    * **API Endpoints**: The complete API surface will consist of only two endpoints:
        * `POST /auth/exchange`
        * `GET /auth/me`
    * **Core Business Logic**: Implementation of the foundational concepts, including the **Principal**, **Credential**, and **Wallet** (with `verified | pending | revoked` states), along with **Chain Defaults** and the global **Risk Posture**.
    * **Non-Negotiable Rules**: The system must enforce all invariants defined in the brief, especially **global wallet uniqueness**, **idempotency** of the exchange endpoint, **verified-first defaults**, and **no silent reassignments** of wallets.

* **Out of Scope for MVP**
    * Server-issued access/refresh tokens or cookies.
    * Free-form metadata or preference "bags" for users.
    * Additional user profile fields such as display name, language, etc.
    * Any server endpoints beyond `POST /auth/exchange` and `GET /auth/me`.

* **MVP Success Criteria**
    * The MVP will be considered a success when all core features listed above are implemented and the system verifiably passes **every item on the "Acceptance checklist"**.

---
### **7. Post-MVP Vision**

The MVP is designed as a robust foundation. The post-MVP vision focuses on leveraging its pluggable architecture to expand its capabilities without compromising its core simplicity.

* **Phase 2 Features**
    * **SIWS Integration**: Introduce Sign-In with Solana as a first-class credential provider.
    * **OIDC Provider Integration**: Add support for other OpenID Connect providers.
    * **Advanced Policy Controls**: Implement backend toggles for more granular control over system behaviors.

* **Long-term Vision**
    * The long-term vision is for the Identity & Memory service to become a transparent, "zero-maintenance" foundational layer for the entire Axon AI ecosystem, so stable and reliable that it becomes an invisible part of the infrastructure.

* **Expansion Opportunities**
    * **Multi-Chain Support**: Add support for other major ecosystems (e.g., EVM chains).
    * **Standalone B2B Identity Service**: Potentially productize the service as a standalone offering.

---
### **8. Technical Considerations**

* **Platform Requirements**
    * A backend API service exposing two HTTPS endpoints: `POST /auth/exchange` and `GET /auth/me`.
    * Must include business-grade observability and auditable logging.
    * Must implement rate limiting to protect against abuse.

* **Technology Preferences**
    * **Stateless Authentication**: The system will be stateless, validating a provider-issued JWT (initially from Dynamic) on every request.
    * **No Server-Side Tokens**: The service will not use server-issued access/refresh tokens or cookies.
    * **HTTP Caching**: The `GET /auth/me` endpoint will support conditional requests via HTTP `ETag` headers.

* **Architecture Considerations**
    * **Idempotency**: The `POST /auth/exchange` endpoint must be fully idempotent.
    * **Provider-Agnostic Resolution**: The core logic must be provider-agnostic to support future credential providers.
    * **Global Uniqueness Constraint**: The architecture must enforce a strict, system-wide unique constraint on wallet ownership (`chain` + `address`).
    * **Explicit Conflict Handling**: The system must explicitly handle and report wallet ownership conflicts with a `409 Conflict` error.

* **Proposed Design Approach: DDD & Clean Architecture**
    * **Domain-Driven Design (DDD)**: The service represents a distinct **Identity Bounded Context**. The **Principal** will be modeled as the **Aggregate Root** to enforce all business invariants.
    * **Clean Architecture**: The implementation will follow Clean Architecture principles to ensure separation of concerns, with core **Entities** and **Use Cases** isolated from external frameworks and infrastructure.

---
### **9. Constraints & Assumptions**

* **Constraints**
    * **Global Wallet Uniqueness**: A wallet can be owned by exactly one Principal.
    * **No Silent Reassignments**: The system must return a `409 Conflict` error and never automatically reassign ownership.
    * **Minimal API Surface**: The service is strictly limited to the two defined endpoints.
    * **Stateless Design**: The architecture must not use server-issued tokens or cookies.
    * **Minimal Data Persistence**: Only explicitly defined data will be persisted.

* **Key Assumptions**
    * **Authentication Provider Reliability**: The system assumes the external authentication provider is secure and reliable.
    * **Provider-Agnostic Logic Sufficiency**: It is assumed the identity resolution logic will support future providers without major architectural changes.
    * **Client-Side Conflict Resolution**: The design assumes the frontend will be responsible for the UX of resolving `409` ownership disputes.
    * **Scalable Uniqueness Check**: It is assumed the check for global wallet uniqueness will be performant at scale.

---
### **10. Key Risks**

* **Conflict Resolution UX**: A risk that the user-facing UI for submitting and tracking disputes may be unclear or confusing.
* **Operational Overhead**: The initial manual conflict resolution flow could create a significant operational burden.
* **Performance at Scale**: The global wallet uniqueness check poses a potential risk of becoming a performance bottleneck.
* **Authentication Provider Dependency**: An outage or breaking change in the initial provider's API would directly impact the service.

---
### **11. Appendices**

* **A. Source Document**: The primary source of truth for all requirements is the "Identity & Memory — Final Business Feature Brief" document.

---
### **12. Next Steps**

* **Immediate Actions**
    1.  Secure final stakeholder approval on this Project Brief.
    2.  Handoff to the Product Manager to begin creating detailed user stories and technical tasks.
    3.  Handoff to the Solution Architect to create a detailed technical design document based on the specified DDD and Clean Architecture approach.

---