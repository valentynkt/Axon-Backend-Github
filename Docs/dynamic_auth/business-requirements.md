# Dynamic.xyz Authentication Integration - Business Requirements Document (BRD)

**Document Version:** 1.0  
**Date:** August 26, 2025  
**Author:** Business Analysis Team  
**Status:** Draft for Review  

---

## 1. Executive Summary

### 1.1 Business Case

Axon AI requires a robust, scalable authentication system that supports modern web3 workflows while maintaining enterprise-grade security. Dynamic.xyz provides a comprehensive authentication platform that enables seamless wallet-based authentication, traditional social logins, and enterprise identity management through a single integration.

### 1.2 Strategic Value

- **Web3-Native Authentication**: Support for 30+ wallet providers across Solana, Ethereum, and other blockchain networks
- **Wallet-First Experience**: Pure wallet-based authentication optimized for crypto trading workflows  
- **Enterprise Ready**: JWT-based authentication with RS256 security, comprehensive user/wallet APIs, and real-time webhooks
- **Zero Authentication Infrastructure**: Complete delegation to Dynamic.xyz with local data mirroring for performance
- **Scalability**: Proven infrastructure handling millions of authentication requests with enterprise SLAs

### 1.3 Investment Justification

Dynamic.xyz integration eliminates the need to build custom authentication infrastructure, reducing development time by an estimated 8-12 weeks while providing enterprise-grade security and compliance capabilities from day one.

---

## 2. Architecture Overview

### 2.1 Dynamic.xyz vs. Axon Backend Responsibilities

#### 2.1.1 Dynamic.xyz Handles (Fully Delegated)
- **User Authentication**: Wallet connections, signature verification, JWT issuance
- **Session Management**: JWT generation, token refresh, session lifecycle
- **Wallet Management**: Multi-wallet connections, wallet provider integrations
- **User Profile Management**: User registration, profile updates, metadata storage
- **Authentication UI**: Frontend SDK, wallet connection flows, authentication widgets
- **Security**: JWT signing, JWKS key rotation, webhook signature generation

#### 2.1.2 Axon Backend Implements (Backend-Only)
- **JWT Validation**: Dynamic.xyz JWT verification using `https://app.dynamic.xyz/api/v0/sdk/{environmentId}/.well-known/jwks`
- **Data Mirroring**: Local PostgreSQL storage of user/wallet data for query performance
- **Webhook Processing**: Real-time sync of user/wallet changes from Dynamic.xyz
- **User Context**: Implementation of `ICurrentUserService` using JWT claims
- **API Integration**: Dynamic.xyz Management API clients for data retrieval
- **Backup Synchronization**: Periodic data reconciliation for webhook failures

#### 2.1.3 Frontend Responsibilities (Out of Scope for Backend)
- **Dynamic.xyz SDK Integration**: Wallet authentication flows using Dynamic's React/JS SDK
- **Token Management**: Storing and sending Dynamic.xyz JWTs to backend
- **Wallet Connection UX**: User interface for wallet selection and connection

---

## 3. Business Context

### 3.1 Axon AI Platform Vision

Axon AI aims to provide intelligent, chat-driven trading experiences for Solana ecosystem participants. Authentication serves as the foundation for:

- **Personalized AI Interactions**: Context-aware conversations based on user identity and wallet history
- **Secure Trading Operations**: Protected execution of high-value financial transactions
- **Compliance Requirements**: Data privacy compliance and audit trail capabilities
- **Partner Integration**: White-label authentication for B2B partners and integrated applications

### 3.2 Current State

- Authentication is currently disabled for MVP/POC development with `DefaultCurrentUserService`
- All API endpoints allow anonymous access via `AllowAnonymous()` configuration
- Existing authentication infrastructure ready via `ICurrentUserService` and `AuthenticationBehavior` pipeline
- Clean Architecture patterns established for authentication integration
- PostgreSQL database ready for user and wallet data mirroring

### 3.3 Target State

- **Wallet-Only Authentication**: Pure Dynamic.xyz JWT-based authentication flow
- **Local Data Mirroring**: Real-time synchronization of user and wallet data from Dynamic.xyz APIs
- **JWT-First Architecture**: Primary reliance on Dynamic.xyz JWT tokens with optional Axon session capability
- **Performance Optimization**: Local database queries for user/wallet lookups with Dynamic.xyz as source of truth
- **Webhook-Driven Updates**: Real-time data synchronization via Dynamic.xyz webhooks

---

## 4. Functional Requirements

### 4.1 Core Authentication Flows

#### 4.1.1 Wallet-Only Authentication
- **Requirement**: Users authenticate exclusively through wallet connections managed by Dynamic.xyz
- **Supported Providers**: Phantom, Solflare, Backpack, MetaMask, WalletConnect, and 30+ providers via Dynamic.xyz
- **Flow**: Frontend uses Dynamic.xyz SDK → User connects wallet → Backend receives Dynamic.xyz JWT → Validates JWT via JWKS
- **Multi-Wallet Support**: Users can connect multiple wallets per account as managed by Dynamic.xyz

#### 4.1.2 JWT Token Validation
- **Requirement**: Backend validates Dynamic.xyz JWTs using RS256 and `https://app.dynamic.xyz/api/v0/sdk/{environmentId}/.well-known/jwks`
- **Implementation**: JWKS caching with automatic key rotation support
- **Token Claims**: Trust user identity (`sub`) and proof of ownership; fetch wallets via API; don't rely on JWT for app permissions
- **No Custom Sessions**: Direct use of Dynamic.xyz JWTs - no additional session layer needed

#### 4.1.3 Data Mirroring Strategy
- **Requirement**: Mirror essential user and wallet data locally for performance
- **Source of Truth**: Dynamic.xyz APIs remain authoritative for all user data
- **Local Storage**: PostgreSQL entities for users, wallets, and authentication events
- **Sync Mechanism**: Webhook-driven updates with periodic reconciliation backup

### 4.2 User Data Management

#### 4.2.1 Dynamic.xyz API Integration
- **Requirement**: Fetch user data from Dynamic.xyz APIs using Management API tokens
- **User Endpoints**: `GET /environments/{environmentId}/users/{userId}` for profile data
- **Wallet Endpoints**: `GET /environments/{environmentId}/users/{userId}/wallets` for wallet data
- **Rate Limiting**: Dynamic enforces rate limits (HTTP 429) and offers SLAs per plan
- **Error Handling**: Robust handling of Dynamic.xyz API failures with fallback to cached data

#### 4.2.2 Local Data Mirroring
- **Requirement**: Mirror essential Dynamic.xyz data in local PostgreSQL for query performance
- **User Data**: Dynamic user ID, email, metadata, creation/update timestamps
- **Wallet Data**: Wallet ID, address, chain, provider, properties, connection timestamps
- **Sync Status**: Track last sync timestamps and identify stale data
- **Data Integrity**: Unique constraints on Dynamic user IDs and wallet addresses per chain

### 4.3 Webhook Integration

#### 4.3.1 Real-Time Event Processing
- **Requirement**: Process Dynamic.xyz webhooks for immediate data synchronization
- **Event Types**: `user.created`, `user.updated`, `user.deleted`, `wallet.linked`, `wallet.unlinked`
- **Webhook Security**: Signature verification using Dynamic.xyz webhook secrets
- **Processing**: Immediate API calls to fetch updated user/wallet data and mirror locally
- **Reliability**: Webhook acknowledgment within 30 seconds with retry handling

#### 4.3.2 Fallback Synchronization
- **Requirement**: Backup sync mechanism for webhook failures or missed events
- **Schedule**: Periodic reconciliation every 6 hours for active users
- **Detection**: Identify users with stale data based on last sync timestamps
- **Recovery**: Batch API calls to refresh user and wallet data from Dynamic.xyz
- **Monitoring**: Alert on sync failures and data inconsistencies

---

## 5. Backend API Requirements

### 5.1 Authentication Endpoints

#### 5.1.1 JWT Validation Endpoint
- **Requirement**: `POST /auth/exchange` - Exchange Dynamic.xyz JWT for Axon session context
- **Input**: Dynamic.xyz JWT token from frontend
- **Processing**: JWKS validation, user data sync, local user lookup
- **Output**: Axon user context with locally-mirrored wallet data (not from JWT)
- **Caching**: Cache JWKS keys and user lookups for performance

#### 5.1.2 User Context Endpoints
- **Requirement**: `GET /auth/me` - Return current user profile with wallet data
- **Data Source**: Local mirrored data with fallback to Dynamic.xyz API
- **Response**: User profile, connected wallets, last activity, metadata
- **Performance**: Sub-100ms response time using local database queries

### 5.2 Webhook Processing

#### 5.2.1 Dynamic.xyz Webhook Receiver
- **Requirement**: `POST /webhooks/dynamic` - Process Dynamic.xyz lifecycle events
- **Security**: Signature validation using Dynamic.xyz webhook secrets
- **Processing**: Immediate user/wallet data refresh from Dynamic.xyz APIs
- **Response**: 200 OK within 30 seconds to prevent webhook retries
- **Logging**: Comprehensive webhook event logging for troubleshooting

---

## 6. Integration Requirements

### 6.1 Backend Architecture Integration

#### 6.1.1 Identity Module Structure
- **New Module**: `src/Modules/Identity/` following Axon Clean Architecture patterns
- **Domain Layer**: User and Wallet aggregates, authentication domain services
- **Application Layer**: JWT validation handlers, user sync commands/queries
- **Infrastructure Layer**: Dynamic.xyz API clients, JWKS services, webhook processors
- **Database**: Entity Framework Core with PostgreSQL for user/wallet mirroring

#### 6.1.2 Existing System Integration
- **Authentication Pipeline**: Implement `ICurrentUserService` using Dynamic.xyz JWT claims
- **Behavior Integration**: Update `AuthenticationBehavior` to validate Dynamic.xyz JWTs
- **CQRS Integration**: User lookup queries and wallet sync commands via MediatR
- **Result Pattern**: All Dynamic.xyz API integrations return `Result<T>` for error handling

### 6.2 Dynamic.xyz API Integration

#### 6.2.1 Management API Client
- **Base URL**: `https://app.dynamic.xyz/api/v0`
- **Authentication**: Bearer token using Dynamic.xyz Management API token
- **Endpoints**: User management, wallet retrieval, environment configuration
- **HTTP Client**: Configured with retry policies, timeout, and rate limiting
- **Error Handling**: Comprehensive error mapping to Axon error types

---

## 7. Security Requirements

### 7.1 Authentication Security

#### 7.1.1 JWT Security
- **Algorithm**: RS256 signature verification using `https://app.dynamic.xyz/api/v0/sdk/{environmentId}/.well-known/jwks`
- **Validation**: Comprehensive JWT validation (signature, issuer, expiration, audience)
- **Key Management**: JWKS caching with automatic key rotation and 6-hour refresh cycles
- **No Token Storage**: Validate Dynamic.xyz JWTs on each request - no server-side token storage

#### 7.1.2 API Security
- **Management API Tokens**: Secure storage of Dynamic.xyz Management API tokens for server-to-server calls
- **Environment Isolation**: Separate Dynamic.xyz environments and tokens for dev/staging/production
- **Rate Limiting**: Respect Dynamic.xyz API limits with exponential backoff and caching
- **Webhook Security**: Signature verification for Dynamic.xyz webhooks using shared secrets

### 7.2 Data Security

#### 7.2.1 Data Protection
- **PII Handling**: Minimal PII storage with explicit consent
- **Wallet Data**: Public wallet addresses only, no private key storage
- **Encryption**: Encryption at rest for sensitive user data
- **Compliance**: GDPR and CCPA compliance for user data handling

#### 7.2.2 Request Security
- **Stateless Authentication**: Each request validates Dynamic.xyz JWT independently
- **User Isolation**: JWT claims determine user context with no cross-user data access
- **CORS Protection**: Strict CORS policies for frontend-backend communication
- **Token Expiration**: Respect Dynamic.xyz JWT expiration times (no custom timeouts needed)

---

## 8. Data Requirements

### 8.1 Local Data Mirroring Model

#### 8.1.1 User Entity (Mirrored from Dynamic.xyz)
```
User:
- Id (Axon UserId, Primary Key)
- DynamicUserId (Dynamic.xyz user ID, Unique)
- Email (from Dynamic.xyz user.email)
- DisplayName (from Dynamic.xyz user profile if provided, minimal PII)
- Username (from Dynamic.xyz user.username)
- CreatedAt, UpdatedAt (Axon timestamps)
- DynamicCreatedAt, DynamicUpdatedAt (Dynamic.xyz timestamps)
- LastSyncedAt (webhook/API sync timestamp)
- Metadata (JSON - Dynamic.xyz user.metadata)
```

#### 8.1.2 Wallet Entity (Mirrored from Dynamic.xyz)
```
Wallet:
- Id (Axon WalletId, Primary Key)
- UserId (Foreign Key to User)
- DynamicWalletId (Dynamic.xyz wallet ID)
- Address (wallet public key/address)
- Chain (SOL, ETH, MATIC, etc.)
- Provider (phantom, metamask, solflare, etc.)
- WalletName (user-assigned name)
- Properties (JSON - provider-specific metadata)
- LastSelectedAt (from Dynamic.xyz)
- ConnectedAt, LastSyncedAt (Axon sync timestamps)
```

### 8.2 Session Management Strategy: JWT-First with Optional Sessions

#### 8.2.1 JWT-First Approach with Optional Enhancement
- **Dynamic.xyz Handles Core Sessions**: Dynamic.xyz JWTs contain all necessary session information
- **JWT Self-Contained**: Tokens include user ID, expiration - no additional session data required for basic auth
- **Optional Axon Sessions**: Backend can create optional sessions for complex features requiring server-side state
- **Reduced Base Complexity**: Core authentication requires no session storage or synchronization
- **Dynamic.xyz Reliability**: Proven JWT management with automatic refresh and security features

#### 8.2.2 JWT-Only Authentication Flow
- **Frontend**: Uses Dynamic.xyz SDK to obtain JWT tokens after wallet authentication
- **Backend**: Validates Dynamic.xyz JWT on each API request using `https://app.dynamic.xyz/api/v0/sdk/{environmentId}/.well-known/jwks`
- **User Context**: Extracts user ID from JWT `sub` claim for `ICurrentUserService` implementation
- **Token Expiration**: Dynamic.xyz handles token lifecycle - backend respects JWT expiration times

### 8.3 Data Synchronization Requirements

#### 8.3.1 Webhook-Driven Sync
- **Trigger**: Dynamic.xyz webhooks for `user.created`, `user.updated`, `user.deleted`, `wallet.linked`, `wallet.unlinked`
- **Processing**: Immediate API calls to Dynamic.xyz to fetch latest user/wallet data
- **Upsert Strategy**: Update existing records or create new ones based on Dynamic user/wallet IDs
- **Audit Trail**: Log all sync operations with timestamps and source (webhook vs. periodic)

#### 8.3.2 Backup Sync Strategy
- **Schedule**: Every 6 hours for users with recent activity (last 7 days)
- **Detection**: Identify stale records using LastSyncedAt timestamps
- **Recovery**: Batch API calls to refresh outdated user and wallet data
- **Performance**: Rate-limited sync to respect Dynamic.xyz API limits

---

## 9. Compliance & Risk Management

### 9.1 Regulatory Compliance

#### 9.1.1 Data Privacy Compliance
- **GDPR Compliance**: Right to access, rectify, delete, and port user data
- **CCPA Compliance**: California privacy rights for user data
- **Data Minimization**: Collect and retain only necessary user data
- **Consent Management**: Clear consent flows for data collection and processing

#### 9.1.2 Security Compliance
- **Audit Trail**: Comprehensive logging for regulatory examination
- **Data Residency**: Configurable data storage location for jurisdictional requirements
- **Access Control**: Role-based access and permission management
- **Security Monitoring**: Authentication event logging and anomaly detection

### 9.2 Risk Management

#### 9.2.1 Security Risks
- **Account Takeover**: Multi-factor authentication and anomaly detection
- **Session Hijacking**: Secure session management with device fingerprinting
- **API Abuse**: Rate limiting and abuse detection for authentication endpoints
- **Wallet Compromise**: Wallet verification and suspicious activity monitoring

#### 9.2.2 Operational Risks  
- **Service Availability**: Enterprise-grade availability per Dynamic.xyz SLA terms
- **Data Loss**: Automated backups with disaster recovery procedures
- **Vendor Lock-in**: API abstraction layer for potential provider migration
- **Scalability**: Auto-scaling capabilities for authentication traffic spikes

---

## 10. Success Metrics

### 10.1 Backend Performance Metrics

#### 10.1.1 API Performance
- **JWT Validation Latency**: 95th percentile JWT validation time <50ms
- **Dynamic.xyz API Response Time**: API call response times to Dynamic.xyz
- **Local Data Query Performance**: User/wallet lookups from local database <20ms
- **Webhook Processing Time**: Time to process and sync webhook events <30s

#### 10.1.2 Integration Reliability
- **JWT Validation Success Rate**: Successful JWT validations / total attempts
- **Dynamic.xyz API Success Rate**: Successful API calls to Dynamic.xyz
- **Webhook Processing Success**: Successfully processed webhooks / total received
- **Data Sync Accuracy**: Percentage of user/wallet data in sync with Dynamic.xyz

### 10.2 Data Synchronization Metrics

#### 10.2.1 Sync Performance
- **Webhook Latency**: Time from Dynamic.xyz event to local data update
- **Batch Sync Duration**: Time to complete periodic user data reconciliation
- **Data Freshness**: Average age of user/wallet data compared to Dynamic.xyz
- **Sync Error Rate**: Failed synchronization attempts / total sync operations

#### 10.2.2 System Health Metrics
- **Database Performance**: PostgreSQL query response times for user/wallet data
- **JWKS Cache Hit Rate**: Percentage of JWT validations using cached keys
- **Dynamic.xyz API Rate Limit Usage**: API call consumption vs. rate limits
- **Memory Usage**: Application memory consumption for caching and processing

---

## 11. Assumptions & Dependencies

### 11.1 Technical Assumptions

#### 11.1.1 Dynamic.xyz Service Assumptions
- **Service Availability**: Dynamic.xyz APIs and JWKS endpoint maintain enterprise availability per SLA
- **Webhook Reliability**: Dynamic.xyz webhooks delivered with at-least-once semantics
- **API Stability**: Dynamic.xyz Management API endpoints remain backward compatible
- **Rate Limits**: Dynamic.xyz API rate limits sufficient for expected user volume per plan

#### 11.1.2 Development Environment
- **.NET 10 Compatibility**: Dynamic.xyz JWT validation works with .NET JWT libraries
- **PostgreSQL Performance**: Database handles user/wallet data with <20ms query times
- **JSON Compatibility**: System.Text.Json handles Dynamic.xyz API response formats
- **HTTP Client Reliability**: .NET HttpClient suitable for Dynamic.xyz API integration

### 11.2 Business Dependencies

#### 11.2.1 Dynamic.xyz Requirements
- **Environment Setup**: Dynamic.xyz environments configured for dev/staging/production
- **Management API Access**: API tokens provisioned with user and wallet read permissions
- **Webhook Configuration**: Webhook endpoints configured for user and wallet lifecycle events
- **Rate Limit Allocation**: API rate limits sized for expected user authentication volume

#### 11.2.2 Internal Dependencies
- **Database Schema**: PostgreSQL schema designed for user/wallet mirroring with proper indexing
- **Frontend Integration**: Frontend team implements Dynamic.xyz SDK for wallet authentication
- **DevOps Infrastructure**: Monitoring and alerting for Dynamic.xyz API health and webhook processing
- **Security Approval**: Security team review of JWT validation and webhook signature verification

### 11.3 External Dependencies

#### 11.3.1 Wallet Providers
- **Wallet Availability**: Continued availability of supported wallet providers
- **Provider APIs**: Stability of wallet provider connection APIs
- **Standard Compliance**: Wallet providers adhering to authentication standards
- **Mobile Support**: Mobile wallet application compatibility

#### 11.3.2 Technology Infrastructure  
- **Internet Connectivity**: Stable network connectivity for API calls
- **DNS Resolution**: Reliable DNS for Dynamic.xyz endpoint resolution
- **TLS/HTTPS Support**: Modern TLS support for secure communications
- **JSON Processing**: Efficient JSON parsing for API responses

---

## 12. Next Steps

### 12.1 Immediate Actions
1. **Stakeholder Review**: Circulate BRD for business stakeholder approval
2. **Technical Feasibility**: Engineering team technical review and estimation
3. **Dynamic.xyz Account**: Set up Dynamic.xyz development environment
4. **Security Review**: Initial security architecture review with security team

### 12.2 Subsequent Documents
1. **Product Requirements Document (PRD)**: Detailed feature specifications and user stories
2. **Functional Requirements Document (FRD)**: Technical implementation specifications
3. **Security Architecture Document**: Detailed security design and threat model
4. **Integration Testing Plan**: Comprehensive testing strategy for authentication flows

### 12.3 Success Criteria
- **Stakeholder Approval**: Business stakeholder sign-off on requirements
- **Technical Feasibility**: Engineering team confirmation of implementation approach
- **Security Approval**: Security team approval of authentication architecture
- **Timeline Commitment**: Agreed delivery timeline for authentication integration

---

**Document Status**: Draft for Review  
**Next Review Date**: [To be scheduled]  
**Approval Required From**: Product Owner, Engineering Lead, Security Lead, Business Stakeholders