using Axon.Modules.Identity.Domain.Enums;
namespace Axon.Modules.Identity.Application.DTOs.Responses;

/// <summary>
/// Data transfer object representing a principal.
/// Uses primitive types only for JSON serialization safety.
/// </summary>
public sealed record PrincipalDto(
    string AxonId,
    string Type,
    string Language,
    string RiskTier,
    Dictionary<string, string> DefaultPerChain,
    int ActiveWalletCount,
    int ActiveCredentialCount
);