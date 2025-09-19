using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Service for normalizing Dynamic.xyz JWT claims into structured user data
/// Handles multi-value claims, different JSON formats, and applies precedence rules
/// </summary>
public sealed class DynamicClaimNormalizer : IDynamicClaimNormalizer
{
    private readonly DynamicXyzOptions _options;
    private readonly ILogger<DynamicClaimNormalizer> _logger;

    public DynamicClaimNormalizer(
        IOptions<DynamicXyzOptions> options,
        ILogger<DynamicClaimNormalizer> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Normalizes claims from a validated Dynamic.xyz JWT into structured user data
    /// </summary>
    public DynamicUserData NormalizeClaimsPrincipal(ClaimsPrincipal claimsPrincipal)
    {
        ArgumentNullException.ThrowIfNull(claimsPrincipal);
        
        var claims = claimsPrincipal.Claims;
        var claimsMultimap = BuildClaimMultimap(claims);

        // Extract all data using field resolvers
        var userId = ResolveAxonUserId(claimsMultimap);
        var email = ResolveEmail(claimsMultimap);
        var environmentId = ResolveEnvironmentId(claimsMultimap);
        var sessionPublicKey = ResolveSessionPublicKey(claimsMultimap);
        var (firstVisitUtc, lastVisitUtc) = ResolveVisitTimestamps(claimsMultimap);
        var isNewUser = ResolveNewUser(claimsMultimap);
        var wallets = ResolveWallets(claimsMultimap);
        var verifiedCredentialsHashes = ResolveVerifiedCredentialsHashes(claimsMultimap);

        return new DynamicUserData(
            userId,
            email,
            environmentId,
            wallets,
            firstVisitUtc,
            lastVisitUtc,
            isNewUser,
            sessionPublicKey,
            verifiedCredentialsHashes);
    }

    #region Core Utilities

    /// <summary>
    /// Builds a multimap from claims to preserve all values for each claim type
    /// </summary>
    private static ILookup<string, string> BuildClaimMultimap(IEnumerable<Claim> claims)
        => claims.ToLookup(c => c.Type, c => c.Value);

    /// <summary>
    /// Enumerates JSON objects from a string that may contain an array or single object
    /// </summary>
    private IEnumerable<JsonElement> EnumerateJsonObjects(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            yield break;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON claim value, skipping");
            yield break;
        }

        using (document)
        {
            var root = document.RootElement;

            switch (root.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var element in root.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.Object)
                            yield return element.Clone(); // Clone to avoid lifetime issues
                    }
                    break;

                case JsonValueKind.Object:
                    yield return root.Clone(); // Clone to avoid lifetime issues
                    break;

                default:
                    _logger.LogWarning("JSON claim value is neither array nor object, skipping");
                    break;
            }
        }
    }

    #endregion

    #region Field Resolvers

    /// <summary>
    /// Resolves user ID using precedence rules:
    /// 1. "sub" claim (JWT standard)
    /// 2. ClaimTypes.NameIdentifier (ASP.NET mapped claim)
    /// </summary>
    private static string ResolveAxonUserId(ILookup<string, string> claimsMultimap)
    {
        // Try "sub" claim first (JWT standard)
        var userId = claimsMultimap[JwtRegisteredClaimNames.Sub].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(userId))
            return userId;

        // Fallback to mapped claim if sub is missing (ASP.NET default mapping)
        userId = claimsMultimap[ClaimTypes.NameIdentifier].FirstOrDefault();
        return userId ?? string.Empty;
    }

    /// <summary>
    /// Resolves email using precedence rules:
    /// 1. "email" claim
    /// 2. ClaimTypes.Email (http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress)
    /// 3. verified_credentials with format="email" -> public_identifier
    /// </summary>
    private string ResolveEmail(ILookup<string, string> claimsMultimap)
    {
        // Try "email" claim first
        var email = claimsMultimap["email"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(email))
            return NormalizeEmail(email);

        // Try standard ClaimTypes.Email
        email = claimsMultimap[ClaimTypes.Email].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(email))
            return NormalizeEmail(email);

        // Try email from verified_credentials as fallback
        email = ExtractEmailFromVerifiedCredentials(claimsMultimap);
        if (!string.IsNullOrWhiteSpace(email))
            return NormalizeEmail(email);

        return string.Empty;
    }

    /// <summary>
    /// Extracts email from verified_credentials with format="email"
    /// </summary>
    private string ExtractEmailFromVerifiedCredentials(ILookup<string, string> claimsMultimap)
    {
        foreach (var verifiedCredentialsValue in claimsMultimap["verified_credentials"])
        {
            foreach (var credential in EnumerateJsonObjects(verifiedCredentialsValue))
            {
                if (credential.TryGetProperty("format", out var format) &&
                    format.GetString() == "email" &&
                    credential.TryGetProperty("public_identifier", out var publicIdentifier))
                {
                    var email = publicIdentifier.GetString();
                    if (!string.IsNullOrWhiteSpace(email))
                        return email;
                }
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Normalizes email by lowercasing the domain part
    /// </summary>
    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        var atIndex = email.LastIndexOf('@');
        if (atIndex <= 0 || atIndex >= email.Length - 1)
            return email; // Invalid format, return as-is

        var localPart = email[..atIndex];
        var domainPart = email[(atIndex + 1)..].ToLowerInvariant();
        return $"{localPart}@{domainPart}";
    }

    /// <summary>
    /// Resolves environment ID with dev fallback
    /// </summary>
    private string ResolveEnvironmentId(ILookup<string, string> claimsMultimap)
    {
        var environmentId = claimsMultimap["environment_id"].FirstOrDefault();
        
        if (!string.IsNullOrWhiteSpace(environmentId))
            return environmentId;

        // Fallback behavior based on environment
        var fallback = _options.EnvironmentId;
        if (string.IsNullOrWhiteSpace(fallback))
        {
            _logger.LogWarning("Missing environment_id claim and no fallback configured");
            return string.Empty;
        }

        // Log fallback usage
        _logger.LogInformation("Using fallback environment_id: {EnvironmentId}", fallback);
        return fallback;
    }

    /// <summary>
    /// Resolves session public key from claim
    /// </summary>
    private static string? ResolveSessionPublicKey(ILookup<string, string> claimsMultimap)
    {
        return claimsMultimap["session_public_key"].FirstOrDefault();
    }

    /// <summary>
    /// Resolves visit timestamps with UTC conversion
    /// </summary>
    private (DateTimeOffset? FirstVisitUtc, DateTimeOffset? LastVisitUtc) ResolveVisitTimestamps(ILookup<string, string> claimsMultimap)
    {
        DateTimeOffset? firstVisitUtc = null;
        DateTimeOffset? lastVisitUtc = null;

        var firstVisitStr = claimsMultimap["first_visit"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstVisitStr) && DateTimeOffset.TryParse(firstVisitStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var firstVisit))
        {
            firstVisitUtc = firstVisit.ToUniversalTime();
        }
        else if (!string.IsNullOrWhiteSpace(firstVisitStr))
        {
            _logger.LogWarning("Failed to parse first_visit timestamp: {Timestamp}", firstVisitStr);
        }

        var lastVisitStr = claimsMultimap["last_visit"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(lastVisitStr) && DateTimeOffset.TryParse(lastVisitStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var lastVisit))
        {
            lastVisitUtc = lastVisit.ToUniversalTime();
        }
        else if (!string.IsNullOrWhiteSpace(lastVisitStr))
        {
            _logger.LogWarning("Failed to parse last_visit timestamp: {Timestamp}", lastVisitStr);
        }

        return (firstVisitUtc, lastVisitUtc);
    }

    /// <summary>
    /// Resolves new user flag with tolerant boolean parsing
    /// </summary>
    private bool ResolveNewUser(ILookup<string, string> claimsMultimap)
    {
        var newUserStr = claimsMultimap["new_user"].FirstOrDefault();
        
        if (string.IsNullOrWhiteSpace(newUserStr))
            return false;

        if (bool.TryParse(newUserStr, out var newUser))
            return newUser;

        _logger.LogWarning("Failed to parse new_user boolean: {Value}", newUserStr);
        return false;
    }

    /// <summary>
    /// Resolves and deduplicates wallets from verified_credentials
    /// </summary>
    private List<WalletData> ResolveWallets(ILookup<string, string> claimsMultimap)
    {
        var wallets = new List<WalletData>();

        foreach (var verifiedCredentialsValue in claimsMultimap["verified_credentials"])
        {
            foreach (var credential in EnumerateJsonObjects(verifiedCredentialsValue))
            {
                // Check if this is a blockchain credential by presence of address and chain
                // Format field is optional - Dynamic may omit it
                if (credential.TryGetProperty("address", out var addressElement) &&
                    credential.TryGetProperty("chain", out var chainElement))
                {
                    var address = addressElement.GetString();
                    var chain = chainElement.GetString();

                    if (!string.IsNullOrWhiteSpace(address) && !string.IsNullOrWhiteSpace(chain))
                    {
                        // Verify it's actually a blockchain credential if format is present
                        if (credential.TryGetProperty("format", out var format))
                        {
                            var formatString = format.GetString();
                            if (!string.IsNullOrWhiteSpace(formatString) && formatString != "blockchain")
                            {
                                // Skip non-blockchain credentials that have format specified
                                continue;
                            }
                        }

                        var wallet = CreateWalletFromCredential(credential);
                        wallets.Add(wallet);
                    }
                }
            }
        }

        // Deduplicate wallets by (chain, address)
        return DeduplicateWallets(wallets);
    }

    /// <summary>
    /// Creates a WalletData from a blockchain credential
    /// </summary>
    private static WalletData CreateWalletFromCredential(JsonElement credential)
    {
        var id = credential.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
        var address = credential.GetProperty("address").GetString() ?? string.Empty;
        var chain = credential.GetProperty("chain").GetString() ?? string.Empty;
        var walletName = credential.TryGetProperty("wallet_name", out var walletNameElement) ? walletNameElement.GetString() : null;
        var provider = credential.TryGetProperty("wallet_provider", out var providerElement) ? providerElement.GetString() ?? string.Empty : string.Empty;

        DateTimeOffset? connectedAtUtc = null;
        if (credential.TryGetProperty("lastSelectedAt", out var connectedAtElement))
        {
            var connectedAtStr = connectedAtElement.GetString();
            if (!string.IsNullOrWhiteSpace(connectedAtStr) && DateTimeOffset.TryParse(connectedAtStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var connectedAt))
            {
                connectedAtUtc = connectedAt.ToUniversalTime();
            }
        }

        return new WalletData(id, address, chain, walletName, provider, connectedAtUtc);
    }

    /// <summary>
    /// Deduplicates wallets by (chain, address) using precedence rules
    /// </summary>
    private static List<WalletData> DeduplicateWallets(List<WalletData> wallets)
    {
        if (wallets.Count <= 1)
            return wallets;

        var deduplicatedWallets = new Dictionary<(string Chain, string Address), WalletData>();

        foreach (var wallet in wallets)
        {
            var key = (wallet.Chain, wallet.Address);
            
            if (!deduplicatedWallets.TryGetValue(key, out var existingWallet))
            {
                deduplicatedWallets[key] = wallet;
                continue;
            }

            // Apply precedence rules: prefer newer timestamp, then richer metadata
            var preferNew = ShouldPreferNewWallet(existingWallet, wallet);
            if (preferNew)
            {
                deduplicatedWallets[key] = wallet;
            }
        }

        return deduplicatedWallets.Values.ToList();
    }

    /// <summary>
    /// Determines if the new wallet should replace the existing one
    /// </summary>
    private static bool ShouldPreferNewWallet(WalletData existing, WalletData newWallet)
    {
        // Prefer wallet with newer timestamp
        if (existing.ConnectedAtUtc != newWallet.ConnectedAtUtc)
        {
            if (newWallet.ConnectedAtUtc.HasValue && !existing.ConnectedAtUtc.HasValue)
                return true;
            if (!newWallet.ConnectedAtUtc.HasValue && existing.ConnectedAtUtc.HasValue)
                return false;
            if (newWallet.ConnectedAtUtc.HasValue && existing.ConnectedAtUtc.HasValue)
                return newWallet.ConnectedAtUtc > existing.ConnectedAtUtc;
        }

        // If timestamps are equal, prefer wallet with more metadata
        var existingMetadataCount = CountNonNullMetadata(existing);
        var newMetadataCount = CountNonNullMetadata(newWallet);
        return newMetadataCount > existingMetadataCount;
    }

    /// <summary>
    /// Counts non-null metadata fields in a wallet
    /// </summary>
    private static int CountNonNullMetadata(WalletData wallet)
    {
        var count = 0;
        if (!string.IsNullOrWhiteSpace(wallet.Id)) count++;
        if (!string.IsNullOrWhiteSpace(wallet.WalletName)) count++;
        if (!string.IsNullOrWhiteSpace(wallet.Provider)) count++;
        if (wallet.ConnectedAtUtc.HasValue) count++;
        return count;
    }

    /// <summary>
    /// Resolves verified credentials hashes if present
    /// </summary>
    private Dictionary<string, object>? ResolveVerifiedCredentialsHashes(ILookup<string, string> claimsMultimap)
    {
        var hashesValue = claimsMultimap["verifiedCredentialsHashes"].FirstOrDefault();
        
        if (string.IsNullOrWhiteSpace(hashesValue))
            return null;

        try
        {
            var hashes = JsonSerializer.Deserialize<Dictionary<string, object>>(hashesValue);
            return hashes?.Count > 0 ? hashes : null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse verifiedCredentialsHashes");
            return null;
        }
    }

    #endregion
}