using System.Security.Cryptography;
using System.Text;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Infrastructure.ETags;

/// <summary>
/// Provides compiled queries for generating ETag fingerprints based on principal data.
/// </summary>
public class PrincipalFingerprintReader
{
    private readonly IIdentityReadDbContext _context;

    public PrincipalFingerprintReader(IIdentityReadDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Generates an ETag fingerprint for a principal combining all relevant timestamps.
    /// </summary>
    public async Task<string> GenerateFingerprintAsync(AxonId principalId, CancellationToken cancellationToken = default)
    {
        var fingerprintData = await GetFingerprintDataQuery
            .Invoke(_context, principalId)
            .FirstOrDefaultAsync(cancellationToken);

        if (fingerprintData == null)
            return string.Empty;

        return GenerateHexHash(fingerprintData);
    }

    /// <summary>
    /// Compiled query to get fingerprint data efficiently.
    /// </summary>
    private static readonly Func<IIdentityReadDbContext, AxonId, IQueryable<FingerprintData>> GetFingerprintDataQuery =
        EF.CompileQuery((IIdentityReadDbContext context, AxonId principalId) =>
            from p in context.Principals
            where p.Id == principalId
            select new FingerprintData
            {
                PrincipalUpdatedAt = p.UpdatedAt,
                MaxVerifiedSigningOwnershipUpdatedAt = context.WalletOwnerships
                    .Where(wo => wo.PrincipalId == principalId 
                              && wo.Status == OwnershipStatus.Verified 
                              && wo.AccessMode == AccessMode.Signing)
                    .Max(wo => (DateTime?)wo.UpdatedAt) ?? DateTime.MinValue,
                MaxDefaultUpdatedAt = context.PrincipalChainDefaults
                    .Where(d => d.PrincipalId == principalId)
                    .Max(d => (DateTime?)d.UpdatedAt) ?? DateTime.MinValue
            });

    private static string GenerateHexHash(FingerprintData data)
    {
        var input = $"{data.PrincipalUpdatedAt:O}|{data.MaxVerifiedSigningOwnershipUpdatedAt:O}|{data.MaxDefaultUpdatedAt:O}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private record FingerprintData
    {
        public DateTime PrincipalUpdatedAt { get; init; }
        public DateTime MaxVerifiedSigningOwnershipUpdatedAt { get; init; }
        public DateTime MaxDefaultUpdatedAt { get; init; }
    }
}