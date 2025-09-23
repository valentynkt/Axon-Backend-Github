# Story 3: Manual Wallet Verification Endpoint

## Overview
Implement the `/auth/verify` endpoint that validates wallet signatures, performs principal resolution, and issues Axon JWT tokens for manual wallet authentication flow.

## Success Criteria
- Wallet signatures can be verified against challenge messages
- HMAC validation prevents tampering
- Replay protection via nonce caching
- Principal resolution creates/links users correctly
- Unified token response format with Exchange endpoint

## Dependencies
- Story 1: JWT Authentication Schemes (for token generation)
- Story 2: Signature Verification Service (for Ed25519 validation)
- Existing: PrincipalResolutionService, AuthenticationService

## Tasks

### Task 3.1: Create Verify Request/Response DTOs
**File:** `src/Api/Contracts/V1/Auth/VerifySignatureRequestDto.cs`

- [ ] Create request DTO with all required fields
- [ ] Add validation attributes

```csharp
namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Request to verify a wallet signature and obtain access token
/// </summary>
public sealed record VerifySignatureRequestDto(
    [Required] string ChainId,              // "solana"
    [Required] string NetworkEnvironment,   // "mainnet", "devnet", "testnet"
    [Required] string Address,              // Wallet address
    [Required] string SignedMessage,        // Exact canonical JSON that was signed
    [Required] string Signature,            // Base58 or Base64 encoded signature
    [Required] string Mac,                  // MAC from challenge response
    [Required, RegularExpression("^v\\d+$")] string Mkv  // MAC key version (e.g., "v1")
);
```

**File:** `src/Api/Contracts/V1/Auth/AuthTokenResponseDto.cs`

- [ ] Create unified token response DTO
- [ ] Use for both Verify and Exchange endpoints

```csharp
namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Unified response for token issuance endpoints
/// </summary>
public sealed record AuthTokenResponseDto(
    string AccessToken,        // Axon JWT
    string TokenType,          // "Bearer"
    long ExpiresIn,           // Seconds until expiry
    string AxonUserId,        // Principal ID
    bool Created,             // Was principal created in this call
    int WalletsLinked,        // Number of wallets linked
    int Conflicts             // Number of conflicts encountered
);
```

### Task 3.2: Update Challenge Response DTO
**File:** `src/Api/Contracts/V1/Auth/ChallengeResponseDto.cs`

- [ ] Add Mac field for HMAC
- [ ] Add Mkv field for key version

```csharp
public sealed record ChallengeResponseDto(
    string Message,
    string NetworkEnvironment,
    string ChainId,
    string Address,
    long IssuedAt,
    long Exp,        // Match canonical message field name
    string Nonce,
    string? Audience,
    string Mac,      // HMAC-SHA256 in Base64Url
    string Mkv       // Key version like "v1"
);
```

### Task 3.3: Enhance AuthenticationService with HMAC
**File:** `src/Modules/Identity/Infrastructure/Services/AuthenticationService.cs`

- [ ] Add MAC generation to GenerateChallengeAsync
- [ ] Implement ValidateMac with constant-time comparison
- [ ] Add replay protection with smart cache keys

```csharp
public async Task<Result<AuthenticationChallenge, Error>> GenerateChallengeAsync(...)
{
    // Existing challenge generation...

    // Add MAC generation
    var keyVersion = _options.CurrentKeyVersion;
    var mac = GenerateMacForChallenge(message, keyVersion);

    var result = new AuthenticationChallenge(
        // ... existing fields ...
        Mac: mac,
        Mkv: keyVersion
    );

    return Result.Success<AuthenticationChallenge, Error>(result);
}

public string GenerateMacForChallenge(string canonicalJson, string keyVersion)
{
    if (!_options.HmacKeys.TryGetValue(keyVersion, out var keyBase64))
    {
        throw new InvalidOperationException($"HMAC key version '{keyVersion}' not found");
    }

    var key = Convert.FromBase64String(keyBase64);
    using var hmac = new HMACSHA256(key);
    var messageBytes = Encoding.UTF8.GetBytes(canonicalJson);
    var hash = hmac.ComputeHash(messageBytes);
    return Base64UrlEncoder.Encode(hash);
}

public Result<bool, Error> ValidateMac(string message, string mac, string keyVersion)
{
    try
    {
        var expectedMac = GenerateMacForChallenge(message, keyVersion);
        var expectedBytes = Base64UrlEncoder.DecodeBytes(expectedMac);
        var actualBytes = Base64UrlEncoder.DecodeBytes(mac);

        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            return Result.Failure<bool, Error>(
                Error.Unauthorized("Invalid MAC", "AUTH.INVALID_MAC"));
        }

        return Result.Success<bool, Error>(true);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "MAC validation failed");
        return Result.Failure<bool, Error>(
            Error.Internal("MAC validation error", "AUTH.MAC_ERROR"));
    }
}

public async Task<Result<Unit, Error>> CheckAndMarkNonceUsedAsync(
    string signedMessage, string mkv, CancellationToken ct = default)
{
    using var doc = JsonDocument.Parse(signedMessage);
    var nonce = doc.RootElement.GetProperty("nonce").GetString();
    if (string.IsNullOrWhiteSpace(nonce))
        return Result.Failure<Unit, Error>(Error.Validation("Missing nonce", "AUTH.MISSING_NONCE"));

    var cacheKey = $"nonce:{mkv}:{nonce}";

    // Compute remaining TTL with skew (don't cache past expiration)
    var iat = doc.RootElement.GetProperty("issued_at").GetInt64();
    var exp = doc.RootElement.GetProperty("exp").GetInt64();
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var remaining = Math.Max(0, (int)(exp - now) + _options.ClockSkewSeconds);

    if (remaining <= 0)
        return Result.Failure<Unit, Error>(Error.Validation("Challenge expired", "CANONICAL_MESSAGE.EXPIRED"));

    if (_cache.TryGetValue(cacheKey, out _))
    {
        _logger.LogWarning("Replay attempt detected for nonce {Nonce}", nonce[..Math.Min(8, nonce.Length)]);
        return Result.Failure<Unit, Error>(
            Error.Conflict("Message already used", "AUTH.REPLAY"));
    }

    _cache.Set(cacheKey, true, TimeSpan.FromSeconds(remaining));
    return Result.Success<Unit, Error>(Unit.Value);
}
```

### Task 3.4: Create Verify Command and Handler
**File:** `src/Modules/Identity/Application/Commands/VerifyWalletSignature/VerifyWalletSignatureCommand.cs`

```csharp
namespace Axon.Modules.Identity.Application.Commands.VerifyWalletSignature;

public sealed record VerifyWalletSignatureCommand(
    string ChainId,
    NetworkEnvironment NetworkEnvironment,
    string Address,
    string SignedMessage,
    string Signature,
    string Mac,
    string Mkv
) : IRequest<Result<VerifyWalletSignatureResult, Error>>;
```

**File:** `src/Modules/Identity/Application/Commands/VerifyWalletSignature/VerifyWalletSignatureHandler.cs`

```csharp
public sealed class VerifyWalletSignatureHandler
    : IRequestHandler<VerifyWalletSignatureCommand, Result<VerifyWalletSignatureResult, Error>>
{
    private readonly IAuthenticationService _authService;
    private readonly IWalletSignatureVerifier _signatureVerifier;
    private readonly IPrincipalResolutionService _principalResolver;
    private readonly IAddressNormalizationService _addressNormalizer;
    private readonly ILogger<VerifyWalletSignatureHandler> _logger;

    public async Task<Result<VerifyWalletSignatureResult, Error>> Handle(
        VerifyWalletSignatureCommand command,
        CancellationToken ct)
    {
        // Step 1: Validate MAC
        var macResult = _authService.ValidateMac(
            command.SignedMessage, command.Mac, command.Mkv);

        if (macResult.IsFailure)
        {
            _logger.LogWarning("MAC validation failed for mkv={Mkv}", command.Mkv);
            return Result.Failure<VerifyWalletSignatureResult, Error>(macResult.Error);
        }

        // Step 2: Validate TTL and canonical message shape
        var validationResult = _authService.ValidateChallengeAsync(
            command.SignedMessage,
            command.NetworkEnvironment,
            command.ChainId,
            command.Address,
            null); // Audience validated separately if needed

        if (validationResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(validationResult.Error);
        }

        // Step 3: Check replay protection
        var replayResult = await _authService.CheckAndMarkNonceUsedAsync(
            command.SignedMessage, command.Mkv, ct);

        if (replayResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(replayResult.Error);
        }

        // Step 4: Verify Ed25519 signature
        var signatureResult = _signatureVerifier.VerifySignature(
            command.ChainId,
            command.Address,
            command.SignedMessage,
            command.Signature);

        if (signatureResult.IsFailure)
        {
            _logger.LogWarning("Signature verification failed for chain={Chain}", command.ChainId);
            return Result.Failure<VerifyWalletSignatureResult, Error>(signatureResult.Error);
        }

        // Step 5: Normalize address
        var normalizedAddress = _addressNormalizer.NormalizeAddress(
            command.ChainId, command.Address);

        // Step 6: Resolve principal
        var chainId = ChainId.Create(command.ChainId).Value;
        var address = Address.Create(normalizedAddress).Value;

        var principalResult = await _principalResolver.ResolveAsync(
            ProviderType.Siws,  // Use Siws for signature-based sign-in
            $"siws:{command.ChainId}",
            normalizedAddress,
            command.NetworkEnvironment,
            chainId,
            address,
            ct);

        if (principalResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(principalResult.Error);
        }

        var principal = principalResult.Value.Principal;
        var wasCreated = principalResult.Value.Path == ResolutionPath.Created;

        // Step 7: Generate Axon JWT
        var tokenResult = await _authService.GenerateAccessTokenAsync(
            new AxonUserId(principal.Id.Value),
            ProviderType.Siws,
            $"siws:{command.ChainId}",
            normalizedAddress,
            _authService.Options.AccessTokenExpirySeconds,
            ct);

        if (tokenResult.IsFailure)
        {
            return Result.Failure<VerifyWalletSignatureResult, Error>(tokenResult.Error);
        }

        return Result.Success<VerifyWalletSignatureResult, Error>(
            new VerifyWalletSignatureResult(
                AccessToken: tokenResult.Value.Token,
                TokenType: "Bearer",
                ExpiresIn: _authService.Options.AccessTokenExpirySeconds,
                AxonUserId: principal.Id.Value.ToString(),
                Created: wasCreated,
                WalletsLinked: 1,
                Conflicts: 0));
    }
}
```

### Task 3.5: Create Verify Endpoint
**File:** `src/Api/Endpoints/V1/Auth/Commands/VerifySignatureEndpoint.cs`

```csharp
namespace Axon.Api.Endpoints.V1.Auth.Commands;

[AllowAnonymous]
public sealed class VerifySignatureEndpoint
    : BaseIdentityCommandEndpoint<
        VerifySignatureRequestDto,
        AuthTokenResponseDto,
        VerifyWalletSignatureCommand,
        VerifyWalletSignatureResult>
{
    public override void Configure()
    {
        Post("/api/v1/auth/verify");
        AllowAnonymous();

        // Apply rate limiting
        Options(x => x.RequireRateLimiting("AuthVerify"));

        // Add security headers
        PreProcessor<SecurityHeadersPreProcessor>();

        Summary(s =>
        {
            s.Summary = "Verify wallet signature and obtain access token";
            s.Description = """
                Verifies a wallet signature against a challenge message and issues an Axon JWT.

                **Flow:**
                1. Validates MAC to ensure challenge integrity
                2. Checks replay protection (nonce cache)
                3. Verifies Ed25519 signature (Solana only in V1)
                4. Resolves or creates Axon principal
                5. Issues access token (15-30 min TTL)

                **Supported Chains:** solana
                **Supported Environments:** mainnet, devnet, testnet
                """;
            s.Responses[200] = "Returns access token and user information";
            s.Responses[400] = "Invalid request or expired challenge";
            s.Responses[401] = "Invalid signature or MAC";
            s.Responses[409] = "Replay attempt or ownership conflict";
            s.Responses[429] = "Rate limit exceeded";
        });
    }

    protected override string GetRoute() => "/api/v1/auth/verify";
    protected override string GetSummary() => "Verify wallet signature";
    protected override string GetDescription() => "Verifies wallet signature and issues Axon JWT";

    protected override Task<Result<VerifyWalletSignatureCommand, Error>> ExecuteCommand(
        VerifySignatureRequestDto request,
        CancellationToken ct)
    {
        // Prevent caching
        HttpContext.Response.Headers["Cache-Control"] = "no-store";
        HttpContext.Response.Headers["Pragma"] = "no-cache";

        // Validate environment
        var envResult = NetworkEnvironment.Create(request.NetworkEnvironment);
        if (envResult.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<VerifyWalletSignatureCommand, Error>(envResult.Error));
        }

        var command = new VerifyWalletSignatureCommand(
            ChainId: request.ChainId.ToLowerInvariant(),
            NetworkEnvironment: envResult.Value,
            Address: request.Address.Trim(),
            SignedMessage: request.SignedMessage,
            Signature: request.Signature,
            Mac: request.Mac,
            Mkv: request.Mkv);

        return Task.FromResult(Result.Success<VerifyWalletSignatureCommand, Error>(command));
    }

    protected override AuthTokenResponseDto MapToResponse(VerifyWalletSignatureResult result)
    {
        return new AuthTokenResponseDto(
            AccessToken: result.AccessToken,
            TokenType: result.TokenType,
            ExpiresIn: result.ExpiresIn,
            AxonUserId: result.AxonUserId,
            Created: result.Created,
            WalletsLinked: result.WalletsLinked,
            Conflicts: result.Conflicts);
    }
}
```

### Task 3.6: Update Exchange Endpoint Response
**File:** `src/Api/Endpoints/V1/Auth/Commands/ExchangeEndpoint.cs`

- [ ] Change return type to AuthTokenResponseDto
- [ ] Remove refresh token from response
- [ ] Map ExchangeOutcome to unified response

## Testing Requirements

### Unit Tests
**File:** `tests/Api/Endpoints/V1/Auth/VerifySignatureEndpointTests.cs`

- [ ] Test happy path with valid signature
- [ ] Test invalid MAC (401)
- [ ] Test invalid signature (401)
- [ ] Test expired challenge (400)
- [ ] Test replay attempt (409)
- [ ] Test rate limiting (429)

### Integration Tests
- [ ] End-to-end flow: Challenge → Sign → Verify
- [ ] Principal creation and linking
- [ ] Token generation and validation
- [ ] Replay protection with memory cache

## Security Checklist
- [ ] MAC validated with constant-time comparison
- [ ] Replay protection prevents nonce reuse
- [ ] TTL enforced with clock skew tolerance
- [ ] No sensitive data logged (signatures, MACs)
- [ ] Cache-Control headers prevent browser caching
- [ ] Rate limiting prevents abuse

## Error Codes
| Code | HTTP | Description |
|------|------|-------------|
| AUTH.INVALID_MAC | 401 | HMAC validation failed |
| AUTH.INVALID_SIGNATURE | 401 | Ed25519 verification failed |
| AUTH.REPLAY | 409 | Nonce already used |
| CANONICAL_MESSAGE.EXPIRED | 400 | Challenge expired |
| CANONICAL_MESSAGE.INVALID_JSON | 400 | Invalid canonical message format |
| CANONICAL_MESSAGE.MISMATCH_* | 400 | Field mismatch in message |
| OWNERSHIP.CONFLICT | 409 | Wallet ownership conflict |