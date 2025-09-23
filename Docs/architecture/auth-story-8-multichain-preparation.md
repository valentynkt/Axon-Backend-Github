# Auth Story 8: Multi-Chain Wallet Support Preparation

**Story ID**: AUTH-008
**Title**: Prepare Infrastructure for Multi-Chain Wallet Authentication
**Priority**: MEDIUM (Should be done before AUTH-007 for cleaner architecture)
**Estimated Effort**: 2 days
**Dependencies**: AUTH-005 (Identity Integration)
**Recommended Sequence**: Implement before or with AUTH-007 for cleaner provider pattern
**Related Stories**: AUTH-007 benefits from this architecture

## Overview

Refactor wallet signature verification into a extensible Strategy pattern and add infrastructure for supporting multiple blockchain types (currently Solana, future Ethereum/EVM), while maintaining optimal performance for existing Solana implementation.

## Problem Statement

### Current Issues
1. **Single Chain Support**: Current implementation only supports Solana Ed25519 signatures
2. **Hardcoded Verification**: Ed25519SignatureVerifier.cs exists and is tightly coupled to Solana
3. **Future EVM Needs**: Planning to add Ethereum/Polygon/other EVM chains in v2
4. **ChainId Format**: Using hyphenated format (e.g., "solana-mainnet", "ethereum-goerli") as per Wallet.cs

### Business Requirements
- Keep existing Solana performance optimal
- Prepare for future EVM chain support (Ethereum, Polygon, BSC, etc.)
- Maintain backward compatibility
- Don't add heavy dependencies until needed

## Solution Overview

Implement a Strategy pattern for wallet verification that allows easy addition of new blockchain types while keeping the current Solana implementation as the high-performance default.

### Key Components
1. **WalletVerifierFactory**: Strategy factory for different chain types
2. **Chain-Specific Verifiers**: Separate implementations for each blockchain
3. **Abstract Wallet Verification**: Chain-agnostic interfaces
4. **Future EVM Preparation**: Structure ready for Nethereum integration

## Acceptance Criteria

### ✅ Must Have
1. **Strategy Pattern**: Implement IWalletSignatureVerifier with chain-specific implementations
2. **Solana Performance**: Current Solana verification performance maintained or improved
3. **Factory Pattern**: Clean way to get appropriate verifier for chain type
4. **Chain Abstraction**: Common interface for all wallet verification operations
5. **Backward Compatibility**: All existing wallet authentication continues to work
6. **No New Dependencies**: Don't add Nethereum or EVM libraries yet

### ✅ Should Have
1. **EVM Preparation**: Code structure ready for easy Nethereum integration
2. **Chain Registration**: Dynamic registration of new chain verifiers
3. **Validation Abstraction**: Common address and signature validation patterns
4. **Error Standardization**: Consistent error responses across chains

### ✅ Could Have
1. **Chain Discovery**: Automatic detection of chain type from address format
2. **Verification Caching**: Cache successful verifications for performance
3. **Chain Metadata**: Support for chain-specific parameters
4. **Testing Framework**: Easy testing setup for new chains

## Technical Specification

### 1. Wallet Verification Strategy Interface

```csharp
namespace Axon.Modules.Identity.Domain.Services;

/// <summary>
/// Strategy interface for chain-specific wallet signature verification
/// </summary>
public interface IWalletSignatureVerifier
{
    /// <summary>
    /// Indicates if this verifier supports the given chain
    /// </summary>
    bool SupportsChain(string chainId);

    /// <summary>
    /// Verify a wallet signature for a specific message
    /// </summary>
    Task<Result<bool, Error>> VerifySignatureAsync(
        WalletSignatureRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Validate wallet address format for this chain
    /// </summary>
    Result<bool, Error> ValidateAddress(string address);

    /// <summary>
    /// Get chain-specific metadata
    /// </summary>
    WalletChainInfo GetChainInfo();
}

/// <summary>
/// Request for wallet signature verification
/// </summary>
public sealed record WalletSignatureRequest(
    string ChainId,
    string Address,
    byte[] Message,
    byte[] Signature)
{
    /// <summary>
    /// Parse chain and network from chainId (e.g., "solana-mainnet" → ("solana", "mainnet"))
    /// NOTE: Uses hyphen separator as per existing Wallet.cs implementation
    /// </summary>
    public (string Chain, string Network) ParseChainId()
    {
        var parts = ChainId.Split('-', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (ChainId, "mainnet");
    }
}

/// <summary>
/// Chain-specific metadata
/// </summary>
public sealed record WalletChainInfo(
    string ChainType,
    string[] SupportedNetworks,
    string AddressFormat,
    string SignatureFormat,
    int AddressLength);
```

### 2. Solana Wallet Verifier (Wrapping Existing Ed25519SignatureVerifier)

```csharp
namespace Axon.Modules.Identity.Infrastructure.Wallet;

/// <summary>
/// Solana-specific wallet signature verification wrapping existing Ed25519SignatureVerifier
/// Adapts the existing implementation to the new strategy pattern
/// </summary>
public sealed class SolanaWalletVerifier : IWalletSignatureVerifier
{
    private readonly Ed25519SignatureVerifier _innerVerifier; // Existing implementation
    private readonly ILogger<SolanaWalletVerifier> _logger;

    // Supported Solana networks
    private static readonly string[] SupportedNetworks = { "mainnet", "devnet", "testnet" };

    public SolanaWalletVerifier(ILogger<SolanaWalletVerifier> logger)
    {
        _logger = logger;
    }

    public bool SupportsChain(string chainId)
    {
        var (chain, network) = ParseChainId(chainId);
        return chain.Equals("solana", StringComparison.OrdinalIgnoreCase) &&
               SupportedNetworks.Contains(network, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Result<bool, Error>> VerifySignatureAsync(
        WalletSignatureRequest request,
        CancellationToken ct = default)
    {
        try
        {
            // Validate chain support
            if (!SupportsChain(request.ChainId))
                return Result<bool, Error>.Failure(
                    Error.NotSupported($"Chain {request.ChainId} not supported by Solana verifier"));

            // Validate address format
            var addressValidation = ValidateAddress(request.Address);
            if (addressValidation.IsFailure)
                return Result<bool, Error>.Failure(addressValidation.Error);

            // Decode Solana public key from Base58
            byte[] publicKeyBytes;
            try
            {
                publicKeyBytes = SimpleBase.Base58.Bitcoin.Decode(request.Address);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to decode Solana address {Address}: {Error}",
                    MaskAddress(request.Address), ex.Message);
                return Result<bool, Error>.Failure(
                    Error.Validation("Invalid Solana address format"));
            }

            // Import Ed25519 public key using NSec
            PublicKey publicKey;
            try
            {
                publicKey = PublicKey.Import(
                    SignatureAlgorithm.Ed25519,
                    publicKeyBytes,
                    KeyBlobFormat.RawPublicKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to import Solana public key: {Error}", ex.Message);
                return Result<bool, Error>.Failure(
                    Error.Validation("Invalid Solana public key"));
            }

            // Verify Ed25519 signature using NSec
            var isValid = SignatureAlgorithm.Ed25519.Verify(publicKey, request.Message, request.Signature);

            if (isValid)
            {
                _logger.LogDebug("Solana signature verification successful for {Address}",
                    MaskAddress(request.Address));
            }
            else
            {
                _logger.LogWarning("Solana signature verification failed for {Address}",
                    MaskAddress(request.Address));
            }

            return Result<bool, Error>.Success(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Solana signature verification error for {Address}",
                MaskAddress(request.Address));
            return Result<bool, Error>.Failure(
                Error.Internal("Signature verification failed"));
        }
    }

    public Result<bool, Error> ValidateAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
            return Result<bool, Error>.Failure(Error.Validation("Address cannot be empty"));

        // Solana addresses are 44 characters in Base58
        if (address.Length != 44)
            return Result<bool, Error>.Failure(
                Error.Validation($"Solana address must be 44 characters, got {address.Length}"));

        // Validate Base58 format
        try
        {
            var decoded = SimpleBase.Base58.Bitcoin.Decode(address);
            if (decoded.Length != 32)
                return Result<bool, Error>.Failure(
                    Error.Validation("Solana address must decode to 32 bytes"));

            return Result<bool, Error>.Success(true);
        }
        catch (Exception)
        {
            return Result<bool, Error>.Failure(
                Error.Validation("Invalid Base58 format for Solana address"));
        }
    }

    public WalletChainInfo GetChainInfo() => new(
        ChainType: "solana",
        SupportedNetworks: SupportedNetworks,
        AddressFormat: "Base58",
        SignatureFormat: "Ed25519",
        AddressLength: 44);

    private static (string Chain, string Network) ParseChainId(string chainId)
    {
        var parts = chainId.Split('-', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (chainId, "mainnet");
    }

    private static string MaskAddress(string address) =>
        address.Length > 8 ? $"{address[..4]}...{address[^4..]}" : address;
}
```

### 3. Future EVM Wallet Verifier (Structure Only)

```csharp
namespace Axon.Modules.Identity.Infrastructure.Wallet;

/// <summary>
/// EVM-compatible wallet signature verification
/// FUTURE: Will use Nethereum when EVM support is implemented
/// Currently just placeholder structure
/// </summary>
public sealed class EVMWalletVerifier : IWalletSignatureVerifier
{
    private readonly ILogger<EVMWalletVerifier> _logger;

    // Supported EVM networks (expand as needed)
    private static readonly string[] SupportedNetworks =
    {
        "mainnet", "goerli", "sepolia",           // Ethereum
        "polygon-mainnet", "polygon-mumbai",      // Polygon
        "bsc-mainnet", "bsc-testnet"              // BSC
    };

    public EVMWalletVerifier(ILogger<EVMWalletVerifier> logger)
    {
        _logger = logger;
    }

    public bool SupportsChain(string chainId)
    {
        var (chain, network) = ParseChainId(chainId);
        return (chain.Equals("ethereum", StringComparison.OrdinalIgnoreCase) ||
                chain.Equals("polygon", StringComparison.OrdinalIgnoreCase) ||
                chain.Equals("bsc", StringComparison.OrdinalIgnoreCase)) &&
               SupportedNetworks.Contains($"{chain}-{network}", StringComparer.OrdinalIgnoreCase);
    }

    public Task<Result<bool, Error>> VerifySignatureAsync(
        WalletSignatureRequest request,
        CancellationToken ct = default)
    {
        // FUTURE: Implement using Nethereum
        // For now, return not implemented
        _logger.LogWarning("EVM signature verification not yet implemented for chain {ChainId}",
            request.ChainId);

        return Task.FromResult(Result<bool, Error>.Failure(
            Error.NotImplemented("EVM signature verification coming in v2")));
    }

    public Result<bool, Error> ValidateAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
            return Result<bool, Error>.Failure(Error.Validation("Address cannot be empty"));

        // EVM addresses are 42 characters (0x + 40 hex chars)
        if (!address.StartsWith("0x") || address.Length != 42)
            return Result<bool, Error>.Failure(
                Error.Validation("EVM address must be 42 characters starting with 0x"));

        // Validate hex format
        var hexPart = address[2..];
        if (!hexPart.All(c => char.IsAsciiHexDigit(c)))
            return Result<bool, Error>.Failure(
                Error.Validation("EVM address must be valid hexadecimal"));

        return Result<bool, Error>.Success(true);
    }

    public WalletChainInfo GetChainInfo() => new(
        ChainType: "evm",
        SupportedNetworks: SupportedNetworks,
        AddressFormat: "Hex",
        SignatureFormat: "ECDSA",
        AddressLength: 42);

    private static (string Chain, string Network) ParseChainId(string chainId)
    {
        var parts = chainId.Split('-', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (chainId, "mainnet");
    }

    // FUTURE: When implementing with Nethereum, add methods like:
    // private async Task<bool> VerifyEthereumSignatureAsync(...)
    // private string RecoverAddressFromSignature(...)
    // private bool IsValidEIP191Message(...)
}
```

### 4. Wallet Verifier Factory

```csharp
namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Factory for getting appropriate wallet verifier based on chain type
/// </summary>
public sealed class WalletVerifierFactory : IWalletVerifierFactory
{
    private readonly IReadOnlyList<IWalletSignatureVerifier> _verifiers;
    private readonly ILogger<WalletVerifierFactory> _logger;

    public WalletVerifierFactory(
        IEnumerable<IWalletSignatureVerifier> verifiers,
        ILogger<WalletVerifierFactory> logger)
    {
        _verifiers = verifiers.ToList();
        _logger = logger;

        _logger.LogInformation("Initialized WalletVerifierFactory with {Count} verifiers: {Verifiers}",
            _verifiers.Count,
            string.Join(", ", _verifiers.Select(v => v.GetChainInfo().ChainType)));
    }

    public IWalletSignatureVerifier GetVerifier(string chainId)
    {
        var verifier = _verifiers.FirstOrDefault(v => v.SupportsChain(chainId));

        if (verifier == null)
        {
            var (chain, network) = ParseChainId(chainId);
            _logger.LogWarning("No verifier found for chain {Chain} on network {Network}",
                chain, network);

            throw new NotSupportedException(
                $"Wallet verification for chain '{chainId}' is not supported. " +
                $"Supported chains: {string.Join(", ", GetSupportedChains())}");
        }

        _logger.LogDebug("Selected {VerifierType} for chain {ChainId}",
            verifier.GetType().Name, chainId);

        return verifier;
    }

    public Result<IWalletSignatureVerifier, Error> TryGetVerifier(string chainId)
    {
        try
        {
            var verifier = GetVerifier(chainId);
            return Result<IWalletSignatureVerifier, Error>.Success(verifier);
        }
        catch (NotSupportedException ex)
        {
            return Result<IWalletSignatureVerifier, Error>.Failure(
                Error.NotSupported(ex.Message));
        }
    }

    public string[] GetSupportedChains()
    {
        return _verifiers
            .SelectMany(v => v.GetChainInfo().SupportedNetworks
                .Select(network => $"{v.GetChainInfo().ChainType}:{network}"))
            .ToArray();
    }

    public WalletChainInfo[] GetChainInfos()
    {
        return _verifiers.Select(v => v.GetChainInfo()).ToArray();
    }

    private static (string Chain, string Network) ParseChainId(string chainId)
    {
        var parts = chainId.Split('-', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (chainId, "mainnet");
    }
}

/// <summary>
/// Factory interface for wallet verifiers
/// </summary>
public interface IWalletVerifierFactory
{
    IWalletSignatureVerifier GetVerifier(string chainId);
    Result<IWalletSignatureVerifier, Error> TryGetVerifier(string chainId);
    string[] GetSupportedChains();
    WalletChainInfo[] GetChainInfos();
}
```

### 5. Updated Wallet Authentication Provider

```csharp
namespace Axon.Modules.Identity.Application.Providers;

/// <summary>
/// Updated wallet authentication provider using multi-chain verifier factory
/// </summary>
public sealed class WalletAuthenticationProvider : IAuthenticationProvider
{
    private readonly IWalletVerifierFactory _verifierFactory;
    private readonly IAxonPrincipalWriteRepository _principalRepo;
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly IChallengeService _challengeService;
    private readonly ILogger<WalletAuthenticationProvider> _logger;

    public WalletAuthenticationProvider(
        IWalletVerifierFactory verifierFactory,
        IAxonPrincipalWriteRepository principalRepo,
        UserManager<AxonUserAuth> userManager,
        IChallengeService challengeService,
        ILogger<WalletAuthenticationProvider> logger)
    {
        _verifierFactory = verifierFactory;
        _principalRepo = principalRepo;
        _userManager = userManager;
        _challengeService = challengeService;
        _logger = logger;
    }

    public bool SupportsProviderType(string providerType) => providerType == "wallet";

    public async Task<Result<AuthenticationData, Error>> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken ct = default)
    {
        if (request is not WalletAuthenticationRequest walletRequest)
            return Result<AuthenticationData, Error>.Failure(
                Error.Validation("Invalid request type for wallet provider"));

        try
        {
            // Validate challenge MAC and timing (unchanged)
            var challengeValidation = await _challengeService.ValidateChallengeAsync(
                walletRequest.Message, walletRequest.Mac, walletRequest.KeyVersion, ct);

            if (challengeValidation.IsFailure)
                return Result<AuthenticationData, Error>.Failure(challengeValidation.Error);

            // Get appropriate verifier for the chain
            var verifierResult = _verifierFactory.TryGetVerifier(walletRequest.ChainId);
            if (verifierResult.IsFailure)
                return Result<AuthenticationData, Error>.Failure(verifierResult.Error);

            var verifier = verifierResult.Value;

            // Verify wallet signature using chain-specific verifier
            var signatureRequest = new WalletSignatureRequest(
                walletRequest.ChainId,
                walletRequest.Address,
                walletRequest.Message,
                walletRequest.Signature);

            var signatureResult = await verifier.VerifySignatureAsync(signatureRequest, ct);
            if (signatureResult.IsFailure)
                return Result<AuthenticationData, Error>.Failure(signatureResult.Error);

            if (!signatureResult.Value)
                return Result<AuthenticationData, Error>.Failure(
                    Error.Unauthorized("Invalid wallet signature"));

            // Rest of authentication flow remains unchanged
            var principalResult = await ResolveOrCreatePrincipalAsync(
                walletRequest.ChainId, walletRequest.Address, ct);

            if (principalResult.IsFailure)
                return Result<AuthenticationData, Error>.Failure(principalResult.Error);

            var principal = principalResult.Value;
            var identityUser = await GetOrCreateIdentityUserAsync(principal, walletRequest, ct);

            if (identityUser == null)
                return Result<AuthenticationData, Error>.Failure(
                    Error.Internal("Failed to create Identity user"));

            var chainInfo = verifier.GetChainInfo();
            var authData = new AuthenticationData(
                User: identityUser,
                ProviderType: "wallet",
                AdditionalClaims: new Dictionary<string, object>
                {
                    ["chain_id"] = walletRequest.ChainId,
                    ["wallet_address"] = walletRequest.Address,
                    ["auth_method"] = "signature",
                    ["chain_type"] = chainInfo.ChainType,
                    ["signature_format"] = chainInfo.SignatureFormat
                });

            _logger.LogInformation("Wallet authentication successful for {Address} on {ChainId} using {ChainType}",
                MaskAddress(walletRequest.Address), walletRequest.ChainId, chainInfo.ChainType);

            return Result<AuthenticationData, Error>.Success(authData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wallet authentication failed for chain {ChainId}", walletRequest.ChainId);
            return Result<AuthenticationData, Error>.Failure(
                Error.Internal("Wallet authentication failed"));
        }
    }

    private static string MaskAddress(string address) =>
        address.Length > 8 ? $"{address[..4]}...{address[^4..]}" : address;
}
```

### 6. Service Registration

```csharp
// In IdentityApiModule.cs
public static IServiceCollection AddWalletVerificationServices(
    this IServiceCollection services)
{
    // Register verifiers
    services.AddScoped<IWalletSignatureVerifier, SolanaWalletVerifier>();

    // Register EVM verifier (placeholder for now)
    // Uncomment when EVM support is implemented:
    // services.AddScoped<IWalletSignatureVerifier, EVMWalletVerifier>();

    // Register factory
    services.AddScoped<IWalletVerifierFactory, WalletVerifierFactory>();

    return services;
}
```

## Implementation Tasks

### Phase 1: Create Strategy Interface (Morning Day 1)
1. **Define Interfaces**
   - [ ] Create IWalletSignatureVerifier interface
   - [ ] Define WalletSignatureRequest/Response DTOs
   - [ ] Create WalletChainInfo metadata record
   - [ ] Define IWalletVerifierFactory interface

2. **Create Base Structure**
   - [ ] Set up strategy pattern foundation
   - [ ] Create chain parsing utilities (chainId format: "solana-mainnet")
   - [ ] Add validation patterns

### Phase 2: Wrap Existing Solana Verifier (Afternoon Day 1)
3. **Adapt Existing Implementation**
   - [ ] Create SolanaWalletVerifier that wraps Ed25519SignatureVerifier
   - [ ] Implement IWalletSignatureVerifier interface as adapter
   - [ ] Reuse existing address validation (44 chars, Base58)
   - [ ] No performance impact - uses same NSec implementation

4. **Test Solana Implementation**
   - [ ] Unit tests for Solana verifier
   - [ ] Performance comparison with current implementation
   - [ ] Integration tests with existing auth flow

### Phase 3: Create Factory and EVM Stub (Morning Day 2)
5. **Factory Implementation**
   - [ ] Create WalletVerifierFactory
   - [ ] Implement verifier selection logic
   - [ ] Add supported chains discovery
   - [ ] Create error handling for unsupported chains

6. **EVM Placeholder Implementation**
   - [ ] Create EVMWalletVerifier skeleton
   - [ ] Implement address validation for EVM (0x prefix, 42 chars)
   - [ ] Add not-implemented error for signature verification
   - [ ] Document future Nethereum integration points

### Phase 4: Integration & Testing (Afternoon Day 2)
7. **Integration with AUTH-007**
   - [ ] If AUTH-007 in progress, coordinate WalletAuthenticationProvider updates
   - [ ] Ensure factory pattern works with provider architecture
   - [ ] Test multi-chain support (Solana functional, EVM placeholder)
   - [ ] Validate backward compatibility

8. **Testing & Documentation**
   - [ ] Unit tests for all components
   - [ ] Integration tests with auth flows
   - [ ] Document how to add new chain support
   - [ ] Performance validation (must maintain current speed)

## Future EVM Implementation

When ready to add EVM support, follow these steps:

### 1. Add Nethereum Dependency
```xml
<PackageReference Include="Nethereum.Web3" Version="4.19.0" />
<PackageReference Include="Nethereum.Signer" Version="4.19.0" />
```

### 2. Complete EVM Verifier
```csharp
public async Task<Result<bool, Error>> VerifySignatureAsync(
    WalletSignatureRequest request,
    CancellationToken ct = default)
{
    // Use Nethereum for EIP-191 signature verification
    var signer = new EthereumMessageSigner();
    var recoveredAddress = signer.RecoverFromSignature(
        request.Message,
        request.Signature.ToHex());

    var isValid = string.Equals(recoveredAddress, request.Address,
        StringComparison.OrdinalIgnoreCase);

    return Result<bool, Error>.Success(isValid);
}
```

### 3. Register EVM Verifier
```csharp
services.AddScoped<IWalletSignatureVerifier, EVMWalletVerifier>();
```

## Testing Strategy

### Unit Tests
- [ ] Individual verifier implementations
- [ ] Factory verifier selection logic
- [ ] Address validation for each chain type
- [ ] Error handling scenarios

### Integration Tests
- [ ] End-to-end auth with Solana (existing flow)
- [ ] Factory integration with authentication provider
- [ ] Unsupported chain error handling
- [ ] Performance comparison

### Future Testing Framework
- [ ] Mock verifier for testing new chains
- [ ] Test data generation for different signature formats
- [ ] Performance benchmarks for multiple chains

## Performance Considerations

### Current Solana Performance
- Maintain NSec Ed25519 verification speed
- No additional overhead from factory pattern
- Lazy loading of verifiers

### Factory Overhead
- Minimal overhead for verifier selection
- Caching of verifier instances
- Fast chain type detection

### Memory Usage
- Single instance of each verifier
- No heavy dependencies until EVM is implemented
- Efficient chain metadata storage

## Success Criteria

### Functional Requirements
- [ ] All existing Solana authentication continues to work
- [ ] Clean interface for adding new chain types
- [ ] Proper error handling for unsupported chains
- [ ] Factory correctly selects appropriate verifiers

### Performance Requirements
- [ ] Solana verification performance unchanged
- [ ] Factory selection adds < 1ms overhead
- [ ] Memory usage increase < 5%

### Quality Requirements
- [ ] 100% backward compatibility
- [ ] Clean separation of chain-specific logic
- [ ] Easy to extend for new chains
- [ ] Well-documented extension points

This story creates the foundation for multi-chain wallet support without adding heavy dependencies, while maintaining optimal performance for the current Solana implementation.

## Recommended Implementation Sequence

### Option 1: Optimal Architecture (Recommended)
1. **AUTH-005** (Identity Integration) - 5-7 days - Foundation
2. **AUTH-008** (Multi-Chain Prep) - 2 days - Clean architecture
3. **AUTH-006** + **AUTH-007** (In Parallel) - 3-4 days - JWT and consolidation
   - Total: 10-13 days with clean architecture

### Option 2: Quick Wins First
1. **AUTH-005** (Identity Integration) - 5-7 days - Foundation
2. **AUTH-006** (JWT Standardization) - 3-4 days - Immediate code reduction
3. **AUTH-007** (Service Consolidation) - 2-3 days - Further cleanup
4. **AUTH-008** (Multi-Chain Prep) - 2 days - Future proofing
   - Total: 12-16 days but with immediate benefits

### Option 3: Maximum Parallelization
1. **AUTH-005** (Identity Integration) - 5-7 days - Foundation
2. **AUTH-008** + **AUTH-006** (In Parallel) - Max 4 days
3. **AUTH-007** (After above) - 2 days - Final consolidation
   - Total: 11-13 days with some coordination overhead