using System.ComponentModel.DataAnnotations;

namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Request to verify a wallet signature and obtain access token
/// </summary>
public sealed record VerifySignatureRequestDto(
    [Required] string ChainId,              // e.g. "solana" or "solana:mainnet"
    [Required] string Address,              // Wallet address
    [Required] string SignedMessage,        // Exact canonical JSON that was signed
    [Required] string Signature             // Base58 or Base64 encoded signature
);