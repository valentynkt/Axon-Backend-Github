# Epic 3: Wallet-First Identity Resolution (Simplified)

**Epic ID:** IDENTITY-3.0
**Status:** Ready for Implementation
**Priority:** Critical
**Estimated Effort:** 1-2 days

## Problem Statement

Current system creates duplicate user identities when the same person logs in with different methods (Google, email, wallet), even when they control the same wallet. This violates our core principle: **"A user is their wallet, not how they log in."**

## Solution

Implement wallet-first resolution: When a user authenticates, check their wallet ownership FIRST before creating a new identity.

## Implementation Plan

### Core Change: Wallet-First Resolution

Modify `ExchangeCredentialHandler.ResolveOrCreatePrincipal` method:

```csharp
private async Task<Result<(AxonPrincipal, bool), Error>> ResolveOrCreatePrincipalWalletFirst(
    ProviderType providerType,
    string issuer,
    string subject,
    List<(string chainId, Address address)> walletSpecs,
    CancellationToken cancellationToken)
{
    AxonPrincipal? resolvedPrincipal = null;

    // STEP 1: Check wallets first (if provided)
    if (walletSpecs?.Count > 0)
    {
        var walletLookup = await _walletRepository.EnsureManyByChainAndAddressAsync(
            walletSpecs, cancellationToken);

        // Check each wallet for existing owner
        foreach (var walletId in walletLookup.Values)
        {
            var principal = await _principalRepository.FindByWalletIdAsync(
                walletId, cancellationToken);

            if (principal != null)
            {
                resolvedPrincipal = principal;
                break; // Found owner
            }
        }
    }

    // STEP 2: Fallback to credential lookup
    if (resolvedPrincipal == null)
    {
        resolvedPrincipal = await _principalRepository.FindByCredentialAsync(
            providerType, issuer, subject, cancellationToken);
    }

    // STEP 3: Add new credential to existing principal
    if (resolvedPrincipal != null)
    {
        // Check if credential already exists (idempotency)
        var hasCredential = resolvedPrincipal.Credentials.Any(c =>
            c.Provider == providerType.Value &&
            c.Issuer == issuer &&
            c.Subject == subject);

        if (!hasCredential)
        {
            var credential = IdentityCredential.Create(
                resolvedPrincipal.Id,
                providerType.Value,
                issuer,
                subject,
                DateTime.UtcNow);

            // Simple check - is this credential taken by another principal?
            var isTaken = await _principalRepository.IsCredentialTakenAsync(
                providerType, issuer, subject, cancellationToken);

            if (isTaken)
            {
                return Result.Failure<(AxonPrincipal, bool), Error>(
                    Error.Conflict("This login method belongs to a different account"));
            }

            resolvedPrincipal.Credentials.Add(credential);
        }

        return (resolvedPrincipal, false); // Existing principal
    }

    // STEP 4: Create new principal
    var createResult = AxonPrincipal.CreateWithDynamicCredential(
        providerType, issuer, subject);

    if (createResult.IsFailure)
        return createResult.Error;

    return (createResult.Value, true); // New principal
}
```

### Integration Point

In `ExchangeCredentialHandler.ExecuteExchangeTransaction`, replace line 148:
```csharp
// OLD: Credential-first resolution
var principalResult = await ResolveOrCreatePrincipal(
    providerType, issuer, subject, cancellationToken);

// NEW: Wallet-first resolution
var walletSpecs = ParseWalletSpecs(userData.Wallets);
var principalResult = await ResolveOrCreatePrincipalWalletFirst(
    providerType, issuer, subject, walletSpecs, cancellationToken);
```

### Fix Nullable Warnings

Line 192: Change to pattern matching:
```csharp
if (command.UserData is not { } userData)
    return Result.Failure<ExchangeOutcome, Error>(
        Error.Validation("User data is required", "EXCHANGE.USER_DATA_REQUIRED"));
```

Line 295: Use Any() for clarity:
```csharp
if (conflicts.Any())
{
    var conflictWallet = conflicts.First();
    // Handle conflict...
}
```

## Testing

### Test 1: Wallet Resolution Works
```csharp
[Test]
public async Task Should_ResolveSamePrincipal_When_WalletMatches()
{
    // Given: Existing principal owns wallet
    var existingPrincipal = CreatePrincipalWithWallet();
    _principalRepository.FindByWalletIdAsync(walletId).Returns(existingPrincipal);

    // When: New credential with same wallet
    var result = await _handler.Handle(commandWithWallet);

    // Then: Returns existing principal
    result.Value.Created.ShouldBeFalse();
    result.Value.AxonId.ShouldBe(existingPrincipal.Id);
}
```

### Test 2: Credential Added to Existing
```csharp
[Test]
public async Task Should_AddCredential_When_WalletMatches()
{
    // Given: Existing principal with one credential
    var existingPrincipal = CreatePrincipalWithCredential("google");

    // When: New Dynamic credential with matching wallet
    var result = await _handler.Handle(commandWithDynamicAndWallet);

    // Then: Principal has both credentials
    existingPrincipal.Credentials.Count.ShouldBe(2);
}
```

### Test 3: Conflict Handling
```csharp
[Test]
public async Task Should_ReturnConflict_When_CredentialBelongsToOther()
{
    // Given: Credential exists for different principal
    _principalRepository.IsCredentialTakenAsync().Returns(true);

    // When: Try to add same credential
    var result = await _handler.Handle(command);

    // Then: Returns conflict error
    result.IsFailure.ShouldBeTrue();
    result.Error.Type.ShouldBe(ErrorType.Conflict);
}
```

## What We're NOT Doing

1. **No automated migration** - Handle existing duplicates manually if needed
2. **No provider abstraction** - Build when we actually need Solana sign-in
3. **No complex concurrency** - Database transactions are sufficient
4. **No extensive metrics** - Basic logging is enough
5. **No performance optimization** - Current speed is acceptable

## Success Criteria

- Users with same wallet get same identity regardless of login method
- No new duplicate principals created
- Existing code continues to work

## Rollback Plan

If issues occur, revert the commit. The changes are isolated to one handler method.