# Identity API Contracts

**REST endpoints, request/response schemas, and validation rules.**

---

## Endpoints Overview

### Authentication Endpoints
- `POST /api/v1/auth/exchange` - Exchange bearer token for Axon identity
- `GET /api/v1/auth/me` - Get current principal information (ETag support)
- `POST /api/v1/auth/challenge` - Generate wallet signature challenge
- `POST /api/v1/auth/verify` - Verify wallet signature and obtain access token
- `POST /api/v1/auth/refresh` - Refresh access token (JWT)

---

## POST /api/v1/auth/exchange

**Purpose**: Exchange Dynamic JWT or Axon Access Token for Axon identity + access token

**Authentication**: `AllowAnonymous` (token validated as input)

**Rate Limiting**: `AuthExchange` policy (10 req/min/IP)

### Request
```http
POST /api/v1/auth/exchange
Authorization: Bearer <Dynamic JWT | Axon Access Token>
Content-Type: application/json

{} // Empty body (optional flags reserved for future)
```

### Response (200 OK)
```json
{
  "accessToken": "eyJ...",
  "tokenType": "Bearer",
  "expiresIn": 1800,
  "axonUserId": "uuid",
  "created": true,
  "walletsLinked": 2,
  "conflicts": 0
}
```

### Behavior
- **Idempotent**: Safe to retry, upserts principal + credentials
- **Wallet Sync**: Parses Dynamic JWT wallet claims, creates/verifies ownerships
- **Chain Defaults**: Applies first available wallet as default per chain
- **Conflict Detection**: Reports ownership conflicts (wallet already owned by another principal)

### Status Codes
- `200` - Exchange successful, returns Axon JWT
- `400` - Invalid request parameters
- `401` - Invalid/expired bearer token
- `409` - Ownership conflict (wallet exclusively owned by another principal)
- `422` - Business rule violation
- `429` - Rate limit exceeded
- `500` - Internal server error

---

## GET /api/v1/auth/me

**Purpose**: Get current principal information with ETag caching

**Authentication**: `[Authorize(Policy = "DynamicOrAxon")]` (accepts both Dynamic JWT and Axon JWT)

**Rate Limiting**: `AuthExchange` policy

### Request
```http
GET /api/v1/auth/me
Authorization: Bearer <Dynamic JWT | Axon JWT>
If-None-Match: "etag-value" (optional)
```

### Response (200 OK)
```json
{
  "axonUserId": "uuid",
  "type": "Human",
  "riskTier": "Low",
  "wallets": [
    {
      "walletId": "uuid",
      "address": "0x1234...",
      "chainId": "ethereum-mainnet",
      "accessMode": "Signing",
      "status": "Verified",
      "verificationSource": "DynamicAttested",
      "verifiedAt": "2025-01-29T12:00:00Z"
    }
  ],
  "chainDefaults": {
    "ethereum-mainnet": "uuid",
    "solana-mainnet": "uuid"
  },
  "credentials": [
    {
      "id": "uuid",
      "provider": "dynamic",
      "issuer": "https://app.dynamic.xyz",
      "subject": "user-id",
      "lastSeenAt": "2025-01-29T12:00:00Z"
    }
  ]
}
```

### Response (304 Not Modified)
Empty body when `If-None-Match` matches current ETag.

### ETag Support
- **Server**: Returns `ETag` header with content fingerprint
- **Client**: Sends `If-None-Match: "<etag>"` for conditional GET
- **Cache**: 304 response when data unchanged, enables client-side caching

### Status Codes
- `200` - Success, returns principal data with `ETag` header
- `304` - Not Modified (data unchanged since provided ETag)
- `401` - Unauthorized (invalid/expired token)
- `500` - Internal server error

---

## POST /api/v1/auth/challenge

**Purpose**: Generate canonical challenge message for wallet authentication

**Authentication**: `AllowAnonymous`

**Rate Limiting**: `AuthChallenge` policy (10 req/min/IP)

### Request
```http
POST /api/v1/auth/challenge
Content-Type: application/json

{
  "chainId": "solana",
  "walletAddress": "base58-address",
  "audience": "https://app.example.com" (optional)
}
```

### Response (200 OK)
```json
{
  "message": "{\"chainId\":\"solana-mainnet\",\"address\":\"...\",\"nonce\":\"...\",\"issuedAt\":1706448000,\"expiresAt\":1706448300}",
  "chainId": "solana-mainnet",
  "address": "base58-address",
  "issuedAt": 1706448000,
  "expiresAt": 1706448300,
  "nonce": "uuid",
  "audience": "https://app.example.com",
  "mac": "hmac-signature",
  "mkv": 1
}
```

### Behavior
- **Message Format**: Canonical JSON with strict field ordering
- **TTL**: 5 minutes (300 seconds)
- **MAC**: HMAC signature for integrity verification
- **ChainId Normalization**: Converts simple chain IDs to compound format (`solana` → `solana-mainnet`)

### Status Codes
- `200` - Challenge generated successfully
- `400` - Invalid request parameters
- `422` - Unsupported chain or validation error
- `429` - Rate limit exceeded
- `500` - Internal server error

---

## POST /api/v1/auth/verify

**Purpose**: Verify wallet signature and obtain Axon access token

**Authentication**: `AllowAnonymous`

**Rate Limiting**: `AuthVerify` policy (10 req/min/IP)

### Request
```http
POST /api/v1/auth/verify
Content-Type: application/json

{
  "chainId": "solana",
  "address": "base58-address",
  "signedMessage": "{...}",
  "signature": "base64-signature",
  "mac": "hmac-signature",
  "mkv": 1
}
```

### Response (200 OK)
```json
{
  "accessToken": "eyJ...",
  "tokenType": "Bearer",
  "expiresIn": 1800,
  "axonUserId": "uuid",
  "created": true,
  "walletsLinked": 1,
  "conflicts": 0
}
```

### Behavior
1. **MAC Validation**: Verifies challenge integrity
2. **Replay Protection**: Checks nonce cache (prevents replay attacks)
3. **Signature Verification**: Ed25519 verification (Solana only in V1)
4. **Principal Resolution**: Resolves or creates Axon principal
5. **Token Issuance**: Issues JWT access token (15-30 min TTL)

### Supported Chains
- **Solana**: mainnet, devnet, testnet (Ed25519 signatures)
- **EVM chains**: Future support

### Status Codes
- `200` - Signature verified, access token issued
- `400` - Invalid request or expired challenge
- `401` - Invalid signature or MAC verification failed
- `409` - Replay attempt detected or ownership conflict
- `422` - Business rule violation
- `429` - Rate limit exceeded
- `500` - Internal server error

---

## POST /api/v1/auth/refresh

**Purpose**: Refresh expired or expiring access token

**Authentication**: `[Authorize]` (requires valid JWT)

**Rate Limiting**: `AuthExchange` policy

### Request
```http
POST /api/v1/auth/refresh
Authorization: Bearer <current-token>
Content-Type: application/json

{}
```

### Response (200 OK)
```json
{
  "accessToken": "eyJ...",
  "tokenType": "Bearer",
  "expiresIn": 1800
}
```

### Status Codes
- `200` - Token refreshed successfully
- `401` - Token invalid or expired beyond refresh window
- `429` - Rate limit exceeded
- `500` - Internal server error

---

## Common Patterns

### Authentication Header
All authenticated endpoints require:
```http
Authorization: Bearer <token>
```

Supported token types:
- **Dynamic JWT**: Issued by Dynamic.xyz (contains wallet claims)
- **Axon JWT**: Issued by Axon after exchange/verify

### Cache Control
Auth endpoints return cache prevention headers:
```http
Cache-Control: no-store
Pragma: no-cache
```

### Error Response Format
```json
{
  "code": "ERROR_CODE",
  "message": "Human-readable error message",
  "details": {...} // Optional additional context
}
```

### Rate Limiting Policies
- `AuthExchange`: 10 req/min/IP
- `AuthChallenge`: 10 req/min/IP
- `AuthVerify`: 10 req/min/IP

---

## Validation Rules

### ChainId Format
- **Input**: Simple (`solana`, `ethereum`) or compound (`solana-mainnet`)
- **Storage**: Compound format always stored/returned
- **Supported**: `solana-{mainnet|devnet|testnet}`, `ethereum-{mainnet|sepolia|goerli}`, etc.

### Wallet Address
- **Solana**: Base58, 32-44 characters
- **EVM**: `0x` + 40 hex characters
- **Validation**: Chain-specific format + malicious pattern detection

### Signature Format
- **Solana**: Base64-encoded Ed25519 signature (64 bytes)
- **EVM**: Future support for ECDSA

---

## Code References

**Endpoints**: `src/Api/Endpoints/V1/Auth/`
- `Commands/ExchangeEndpoint.cs` - Exchange token
- `Queries/MeEndpoint.cs` - Get current principal
- `Commands/ChallengeEndpoint.cs` - Generate challenge
- `Commands/VerifySignatureEndpoint.cs` - Verify signature
- `Commands/RefreshEndpoint.cs` - Refresh token

**Contracts**: `src/Api/Contracts/V1/Auth/`
**Validators**: `src/Api/Validators/V1/Auth/`

**Application Layer**: `src/Modules/Identity/Application/`
- `Commands/ExchangeCredential/` - Exchange command handler
- `Queries/GetMyPrincipal/` - Get principal query handler
- `Commands/GenerateChallenge/` - Challenge generation
- `Commands/VerifyWalletSignature/` - Signature verification

---

## Additional Documentation

See [Authentication Flows](./03-authentication.md) for:
- Complete authentication sequences
- Token lifecycle management
- Dynamic.xyz integration details
- Wallet verification flows