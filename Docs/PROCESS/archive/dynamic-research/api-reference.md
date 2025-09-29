# Dynamic.xyz API Reference

## Overview

Dynamic provides a robust API that allows developers to securely access their dashboard environment's data and programmatically update settings relevant to their Dynamic-powered application.

## Base URLs

- **Production**: `https://app.dynamic.xyz/api/v0`
- **Alternative**: `https://app.dynamicauth.com/api/v0` 
- **Development**: `http://localhost:3333/api/v0`

## Authentication

All APIs require a bearer token used to authenticate requests and authorize access to resources.

### Getting an API Token

1. Go to your environment's [Developer Tab](https://app.dynamic.xyz/dashboard/developer/api)
2. In the "API Token" section, click "Create Token"
3. Provide a meaningful name (e.g., "Mycompany Admin", "background-job service")
4. **Copy the API token immediately** - this is the last time you'll see the plaintext token
5. The token starts with `dyn_` followed by 56 alphanumeric characters

### Using the API Token

Include the token in the `Authorization` header of your HTTP requests:

```bash
Authorization: Bearer dyn_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
```

## Standard Errors

- **400** - Bad Request: Invalid form/parameters in request
- **401** - Unauthorized: Missing or invalid Authorization header  
- **403** - Forbidden: Token lacks access to requested resource
- **404** - Not Found: Invalid URL path or resource ID
- **500** - Internal Server Error

## Rate Limits

Dynamic's API endpoints are subject to rate limits. Refer to the [Rate Limits documentation](https://www.dynamic.xyz/docs/developer-dashboard/rate-limits) for details.

---

# API Endpoints

## Users

### Get User by ID

Retrieve detailed information about a specific user.

**Endpoint**: `GET /environments/{environmentId}/users/{userId}`

**Parameters**:
- `environmentId` (string, required): Environment UUID (36 characters)  
- `userId` (string, required): User UUID (36 characters)

**Example Request**:
```bash
curl --request GET \
  --url https://app.dynamicauth.com/api/v0/environments/{environmentId}/users/{userId} \
  --header 'Authorization: Bearer <token>'
```

**Response Schema**:
```json
{
  "user": {
    "id": "95b11417-f18f-457f-8804-68e361f9164f",
    "projectEnvironmentId": "95b11417-f18f-457f-8804-68e361f9164f",
    "verifiedCredentials": [
      {
        "address": "0xbF394748301603f18d953C90F0b087CBEC0E1834",
        "chain": "string",
        "refId": "95b11417-f18f-457f-8804-68e361f9164f",
        "signerRefId": "95b11417-f18f-457f-8804-68e361f9164f",
        "email": "jsmith@example.com",
        "id": "95b11417-f18f-457f-8804-68e361f9164f",
        "wallet_name": "string",
        "wallet_provider": "browserExtension",
        "wallet_properties": {
          "turnkeySubOrganizationId": "95b11417-f18f-457f-8804-68e361f9164f",
          "turnkeyPrivateKeyId": "95b11417-f18f-457f-8804-68e361f9164f",
          "turnkeyHDWalletId": "95b11417-f18f-457f-8804-68e361f9164f",
          "isAuthenticatorAttached": true,
          "turnkeyUserId": "95b11417-f18f-457f-8804-68e361f9164f",
          "isSessionKeyCompatible": true,
          "version": "V1",
          "ecdsaProviderType": "zerodev_signer_to_ecdsa",
          "entryPointVersion": "v6",
          "kernelVersion": "v2_4"
        },
        "format": "blockchain",
        "oauth_provider": "emailOnly",
        "phoneNumber": "9171113333",
        "phoneCountryCode": "1",
        "isoCountryCode": "US",
        "lastSelectedAt": "2023-11-07T05:31:56Z",
        "signInEnabled": true
      }
    ],
    "lastVerifiedCredentialId": "95b11417-f18f-457f-8804-68e361f9164f",
    "sessionId": "95b11417-f18f-457f-8804-68e361f9164f",
    "alias": "An example name",
    "country": "US",
    "email": "jsmith@example.com",
    "firstName": "An example name",
    "jobTitle": "An example name", 
    "lastName": "An example name",
    "phoneNumber": "string",
    "policiesConsent": true,
    "tShirtSize": "An example name",
    "team": "An example name",
    "username": "An example name",
    "firstVisit": "2023-11-07T05:31:56Z",
    "lastVisit": "2023-11-07T05:31:56Z",
    "newUser": true,
    "metadata": {},
    "mfaBackupCodeAcknowledgement": "pending",
    "btcWallet": "string",
    "kdaWallet": "string", 
    "ltcWallet": "string",
    "ckbWallet": "string",
    "kasWallet": "string",
    "dogeWallet": "string",
    "emailNotification": true,
    "discordNotification": true,
    "newsletterNotification": true,
    "lists": ["string"],
    "scope": "superuser marketing operations",
    "missingFields": [],
    "walletPublicKey": "string",
    "wallet": "string",
    "chain": "ETH",
    "createdAt": "2023-11-07T05:31:56Z",
    "updatedAt": "2023-11-07T05:31:56Z",
    "sessions": [
      {
        "id": "95b11417-f18f-457f-8804-68e361f9164f",
        "createdAt": "2023-11-07T05:31:56Z", 
        "ipAddress": "string",
        "userAgent": "string",
        "revokedAt": "2023-11-07T05:31:56Z"
      }
    ],
    "wallets": [
      {
        "id": "95b11417-f18f-457f-8804-68e361f9164f",
        "name": "An example name",
        "chain": "ETH", 
        "publicKey": "0xbF394748301603f18d953C90F0b087CBEC0E1834",
        "provider": "browserExtension",
        "properties": {
          "turnkeySubOrganizationId": "95b11417-f18f-457f-8804-68e361f9164f",
          "turnkeyPrivateKeyId": "95b11417-f18f-457f-8804-68e361f9164f", 
          "turnkeyHDWalletId": "95b11417-f18f-457f-8804-68e361f9164f",
          "isAuthenticatorAttached": true,
          "turnkeyUserId": "95b11417-f18f-457f-8804-68e361f9164f",
          "isSessionKeyCompatible": true,
          "version": "V1",
          "ecdsaProviderType": "zerodev_signer_to_ecdsa",
          "entryPointVersion": "v6",
          "kernelVersion": "v2_4"
        },
        "lastSelectedAt": "string"
      }
    ],
    "chainalysisChecks": [
      {
        "id": "95b11417-f18f-457f-8804-68e361f9164f",
        "createdAt": "2023-11-07T05:31:56Z",
        "result": "OK",
        "walletPublicKey": "0xbF394748301603f18d953C90F0b087CBEC0E1834",
        "response": "string"
      }
    ],
    "oauthAccounts": [
      {
        "id": "95b11417-f18f-457f-8804-68e361f9164f",
        "provider": "emailOnly",
        "accountUsername": "string"
      }
    ],
    "mfaDevices": [
      {
        "type": "totp",
        "verified": true,
        "id": "95b11417-f18f-457f-8804-68e361f9164f", 
        "createdAt": "2023-11-07T05:31:56Z",
        "verifiedAt": "2023-11-07T05:31:56Z",
        "default": true,
        "alias": "string"
      }
    ]
  }
}
```

**Key Response Fields**:
- `user.id`: Unique user identifier
- `user.email`: User's email address
- `user.verifiedCredentials`: Array of linked wallets and credentials
- `user.wallets`: Array of user's wallets
- `user.sessions`: Active user sessions
- `user.metadata`: Custom user data
- `user.scope`: JWT permissions (whitespace-separated)
- `user.chain`: Primary blockchain (`ETH`, `SOL`, `BTC`, etc.)

---

## Wallets

### Get Wallets by User

Retrieve all wallets associated with a specific user.

**Endpoint**: `GET /environments/{environmentId}/users/{userId}/wallets`

**Parameters**:
- `environmentId` (string, required): Environment UUID (36 characters)
- `userId` (string, required): User UUID (36 characters)

**Example Request**:
```bash
curl --request GET \
  --url https://app.dynamicauth.com/api/v0/environments/{environmentId}/users/{userId}/wallets \
  --header 'Authorization: Bearer <token>'
```

**Response Schema**:
```json
{
  "count": 123,
  "wallets": [
    {
      "id": "95b11417-f18f-457f-8804-68e361f9164f",
      "name": "An example name",
      "chain": "ETH",
      "publicKey": "0xbF394748301603f18d953C90F0b087CBEC0E1834",
      "provider": "browserExtension",
      "properties": {
        "turnkeySubOrganizationId": "95b11417-f18f-457f-8804-68e361f9164f",
        "turnkeyPrivateKeyId": "95b11417-f18f-457f-8804-68e361f9164f",
        "turnkeyHDWalletId": "95b11417-f18f-457f-8804-68e361f9164f", 
        "isAuthenticatorAttached": true,
        "turnkeyUserId": "95b11417-f18f-457f-8804-68e361f9164f",
        "isSessionKeyCompatible": true,
        "version": "V1",
        "ecdsaProviderType": "zerodev_signer_to_ecdsa",
        "entryPointVersion": "v6",
        "kernelVersion": "v2_4"
      },
      "lastSelectedAt": "string"
    }
  ]
}
```

**Key Response Fields**:
- `count`: Total number of wallets
- `wallets[].id`: Unique wallet identifier
- `wallets[].chain`: Blockchain type (`ETH`, `SOL`, `BTC`, etc.)
- `wallets[].provider`: Wallet provider type
- `wallets[].publicKey`: Wallet's public key/address
- `wallets[].properties`: Provider-specific wallet metadata

---

## Environments

### Get Environment by ID

Retrieve environment configuration and settings.

**Endpoint**: `GET /environments/{environmentId}`

**Parameters**:
- `environmentId` (string, required): Environment UUID (36 characters)

**Example Request**:
```bash
curl --request GET \
  --url https://app.dynamicauth.com/api/v0/environments/{environmentId} \
  --header 'Authorization: Bearer <token>'
```

**Response Schema**:
```json
{
  "id": "string",
  "settings": {
    "kyc": [],
    "sdk": {
      "waas": {
        "onSignUp": {
          "promptBackupOptions": true,
          "promptClientShareExport": true
        },
        "relayUrl": "string",
        "backupOptions": ["string"],
        "delegatedAccess": {
          "enabled": true
        },
        "passcodeRequired": true,
        "delegatedAccessEndpoint": "string"
      },
      "views": [],
      "mobile": {
        "deeplinkUrlsEnabled": true
      },
      "funding": {
        "onramps": ["string"],
        "externalWallets": {
          "enabled": true,
          "minAmount": {
            "amount": "string",
            "currency": "string"
          }
        }
      },
      "showFiat": true,
      "emailSignIn": {
        "signInProvider": "string"
      },
      "multiWallet": true,
      "socialSignIn": {
        "providers": [
          {
            "enabled": true,
            "provider": "string"
          }
        ],
        "signInProvider": "string"
      },
      "walletConnect": {
        "projectId": "string",
        "v2Enabled": true,
        "walletProjectId": "string"
      },
      "embeddedWallets": {
        "promptForKeyExport": true,
        "sessionKeyDuration": {
          "unit": "string",
          "amount": 123
        },
        "chainConfigurations": [
          {
            "name": "string",
            "enabled": true,
            "primary": true
          }
        ],
        "defaultWalletVersion": "string",
        "emailRecoveryEnabled": true,
        "transactionSimulation": true,
        "domainEnabledByProvider": true,
        "supportedSecurityMethods": {
          "email": {
            "isDefault": true,
            "isEnabled": true,
            "listPosition": 123,
            "isPermanentAuthenticator": true
          },
          "passkey": {
            "isDefault": true,
            "isEnabled": true,
            "listPosition": 123,
            "isPermanentAuthenticator": true
          },
          "password": {
            "isDefault": true,
            "isEnabled": true,
            "listPosition": 123,
            "isPermanentAuthenticator": true
          }
        }
      },
      "accountAbstraction": {
        "allUsers": true,
        "allWallets": true,
        "enablePasskeys": true,
        "separateSmartWalletAndSigner": true
      }
    },
    "chains": [
      {
        "name": "string",
        "enabled": true,
        "networks": [
          {
            "type": "string",
            "rpcUrl": "string",
            "enabled": true,
            "iconUrl": "string",
            "chainName": "string",
            "networkId": "string"
          }
        ]
      }
    ],
    "design": {
      "modal": {
        "view": "string",
        "brand": "string",
        "theme": "string",
        "border": "string",
        "radius": 123,
        "template": "string",
        "emailOnly": true,
        "displayOrder": ["string"],
        "primaryColor": "string",
        "socialAboveEmail": true,
        "showWalletsButton": true,
        "splitEmailAndSocial": true
      },
      "button": {
        "radius": 123,
        "fontColor": "string",
        "background": "string",
        "paddingWidth": 123,
        "paddingHeight": 123
      },
      "widget": {
        "theme": "string",
        "border": "string",
        "radius": 123,
        "textColor": "string",
        "backgroundColor": "string"
      }
    },
    "general": {
      "appLogo": "string",
      "displayName": "string",
      "supportText": "string",
      "supportUrls": {
        "slack": "string",
        "twitter": "string"
      },
      "supportEmail": "string",
      "emailCompanyName": "string"
    },
    "privacy": {
      "collectIp": true
    },
    "providers": [],
    "customFields": [],
    "environmentName": "string"
  },
  "createdAt": "string",
  "updatedAt": "string",
  "sdkVersion": "string"
}
```

---

## Additional API Endpoints

### Allowlists
- `GET /environments/{environmentId}/allowlists` - Get all allowlists
- `POST /environments/{environmentId}/allowlists` - Create new allowlist
- `GET /environments/{environmentId}/allowlists/{allowlistId}` - Get allowlist by ID
- `DELETE /environments/{environmentId}/allowlists/{allowlistId}` - Delete allowlist
- `POST /environments/{environmentId}/allowlists/{allowlistId}/entries` - Add allowlist entry
- `DELETE /environments/{environmentId}/allowlists/{allowlistId}/entries/{entryId}` - Remove allowlist entry

### Analytics
- `GET /environments/{environmentId}/analytics/overview` - Get analytics overview
- `GET /environments/{environmentId}/analytics/topline` - Get topline metrics
- `GET /environments/{environmentId}/analytics/engagement` - Get engagement data
- `GET /environments/{environmentId}/analytics/wallets-breakdown` - Get wallet breakdown
- `GET /environments/{environmentId}/analytics/visits` - Get visit analytics

### Custom Fields
- `GET /environments/{environmentId}/custom-fields` - Get custom fields
- `POST /environments/{environmentId}/custom-fields` - Create custom field
- `GET /environments/{environmentId}/custom-fields/{fieldId}` - Get custom field by ID
- `PUT /environments/{environmentId}/custom-fields/{fieldId}` - Update custom field
- `DELETE /environments/{environmentId}/custom-fields/{fieldId}` - Delete custom field

### Events
- `GET /environments/{environmentId}/events` - Get environment events
- `GET /event-types` - Get available event types

### Exports
- `GET /environments/{environmentId}/exports` - Get data exports
- `POST /environments/{environmentId}/exports` - Create export request
- `GET /environments/{environmentId}/exports/{exportId}` - Get export by ID
- `GET /environments/{environmentId}/exports/{exportId}/download` - Download export

## Supported Blockchain Chains

Dynamic supports the following blockchain types:

- **ETH** - Ethereum and EVM-compatible chains
- **SOL** - Solana
- **BTC** - Bitcoin  
- **ALGO** - Algorand
- **FLOW** - Flow
- **STARK** - Starknet
- **COSMOS** - Cosmos ecosystem
- **SUI** - Sui Network
- **ECLIPSE** - Eclipse

## Common Provider Types

- `browserExtension` - Browser wallet extensions (MetaMask, etc.)
- `walletConnect` - WalletConnect protocol
- `email` - Email-based authentication
- `social` - Social login providers
- `embedded` - Dynamic embedded wallets
- `passkey` - WebAuthn/Passkey authentication

## Usage Notes

1. **Authentication**: Always include the Bearer token in the Authorization header
2. **UUIDs**: All ID parameters must be valid UUIDs (36 characters)
3. **Rate Limits**: Monitor your API usage to avoid rate limiting
4. **Error Handling**: Implement proper error handling for all standard HTTP error codes
5. **Pagination**: Some endpoints may support pagination (check individual endpoint documentation)
6. **Webhooks**: Consider implementing webhooks for real-time updates instead of polling APIs