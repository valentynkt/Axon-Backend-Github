# Identity API Contracts

**REST endpoints, request/response schemas, and validation rules.**

---

## Endpoints Overview

### Authentication Endpoints
- `POST /api/v1/auth/exchange` - Exchange bearer token for Axon identity
- `GET /api/v1/auth/me` - Get current principal information
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
  "refreshToken": "protected-token-string",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "axonUserId": "uuid",
  "created": true,
  "walletsLinked": 2,
  "conflicts": 0
}
```

**Response Fields:**
- `accessToken`: Axon JWT with 15-minute expiration
- `refreshToken`: Encrypted refresh token with 30-day expiration (for token renewal)
- `tokenType`: Always "Bearer"
- `expiresIn`: Seconds until access token expires (900 = 15 minutes)
- `axonUserId`: User's principal ID
- `created`: Whether this was a new user registration
- `walletsLinked`: Number of wallets successfully linked
- `conflicts`: Number of wallet ownership conflicts detected

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

**Purpose**: Get current principal information

**Authentication**: `[Authorize(Policy = "DynamicOrAxon")]` (accepts both Dynamic JWT and Axon JWT)

**Rate Limiting**: `AuthExchange` policy

### Request
```http
GET /api/v1/auth/me
Authorization: Bearer <Dynamic JWT | Axon JWT>
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

### Status Codes
- `200` - Success, returns principal data
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
  "refreshToken": "protected-token-string",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "axonUserId": "uuid",
  "created": true,
  "walletsLinked": 1,
  "conflicts": 0
}
```

**Response Fields:**
- `accessToken`: Axon JWT with 15-minute expiration
- `refreshToken`: Encrypted refresh token with 30-day expiration (for token renewal)
- `tokenType`: Always "Bearer"
- `expiresIn`: Seconds until access token expires (900 = 15 minutes)
- `axonUserId`: User's principal ID
- `created`: true if new user was created, false if existing
- `walletsLinked`: Number of wallets linked (typically 1)
- `conflicts`: Number of ownership conflicts (typically 0)

### Behavior
1. **MAC Validation**: Verifies challenge integrity
2. **Signature Verification**: Ed25519 verification (Solana only in V1)
3. **Principal Resolution**: Resolves or creates Axon principal
4. **Token Issuance**: Issues JWT access token (15-30 min TTL)

### Supported Chains
- **Solana**: mainnet, devnet, testnet (Ed25519 signatures)
- **EVM chains**: Future support

### Status Codes
- `200` - Signature verified, access token issued
- `400` - Invalid request or expired challenge
- `401` - Invalid signature or MAC verification failed
- `409` - Ownership conflict
- `422` - Business rule violation
- `429` - Rate limit exceeded
- `500` - Internal server error

---

## POST /api/v1/auth/refresh

**Purpose**: Refresh expired or expiring access token using refresh token

**Authentication**: `AllowAnonymous`

**Rate Limiting**: `AuthExchange` policy

### Request
```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refreshToken": "protected-token-string"
}
```

### Response (200 OK)
```json
{
  "success": true,
  "accessToken": "eyJ...",
  "refreshToken": "new-protected-token-string",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "issuedAt": "2025-10-07T12:00:00Z",
  "accessTokenExpiresAt": "2025-10-07T12:15:00Z",
  "refreshTokenExpiresAt": "2025-11-06T12:00:00Z"
}
```

**Response Fields:**
- `success`: Always true on success
- `accessToken`: New JWT access token (15-minute expiration)
- `refreshToken`: **New** refresh token (30-day expiration) - old token is invalidated
- `tokenType`: Always "Bearer"
- `expiresIn`: Seconds until new access token expires (900)
- `issuedAt`: When tokens were issued (ISO 8601)
- `accessTokenExpiresAt`: Absolute expiry time for access token
- `refreshTokenExpiresAt`: Absolute expiry time for new refresh token

**Token Rotation:**
- Old refresh token is immediately invalidated (replay protection)
- Must use NEW refresh token for subsequent refresh requests
- Attempting to reuse old refresh token will fail with 401

### Status Codes
- `200` - Token refreshed successfully, new tokens issued
- `401` - Refresh token invalid, expired, or already used
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