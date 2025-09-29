using BuildingBlocks.Application;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Stores;

using System.Security.Claims;
using Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Domain.Aggregates.AxonPrincipal;
using Domain.Aggregates.Wallet;
using Domain.Entities;
using Domain.Enums;
using Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Custom UserStore that bridges Microsoft Identity Framework to Axon domain aggregates.
/// Implements core Identity interfaces while maintaining domain integrity.
/// </summary>
public sealed class AxonUserStore :
    IUserStore<AxonUserAuth>,
    IUserLoginStore<AxonUserAuth>,
    IUserSecurityStampStore<AxonUserAuth>,
    IUserClaimStore<AxonUserAuth>,
    IUserEmailStore<AxonUserAuth>,
    IUserPhoneNumberStore<AxonUserAuth>,
    IUserTwoFactorStore<AxonUserAuth>,
    IUserLockoutStore<AxonUserAuth>
{
    private readonly IAxonPrincipalWriteRepository _principalRepo;
    private readonly IdentityContext _dbContext;
    private readonly IIdentityReadDbContext _readDbContext;
    private readonly ILogger<AxonUserStore> _logger;
    private readonly IWriteUnitOfWork<IdentityModule> _unitOfWork;

    public AxonUserStore(
        IAxonPrincipalWriteRepository principalRepo,
        IdentityContext dbContext,
        IIdentityReadDbContext readDbContext,
        ILogger<AxonUserStore> logger,
        IWriteUnitOfWork<IdentityModule> unitOfWork)
    {
        _principalRepo = principalRepo;
        _dbContext = dbContext;
        _readDbContext = readDbContext;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    #region IUserStore Implementation

    public async Task<IdentityResult> CreateAsync(AxonUserAuth user, CancellationToken ct)
    {
        try
        {
            // Ensure corresponding AxonPrincipal exists
            var principal = await _principalRepo.GetByIdAsync(user.AxonPrincipalId, ct);
            if (principal == null)
            {
                // Create new principal if it doesn't exist
                principal = AxonPrincipal.CreateHuman(user.AxonPrincipalId);
                await _principalRepo.AddAsync(principal, ct);
            }

            // Add Identity user
            _dbContext.Set<AxonUserAuth>().Add(user);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Created Identity user {UserId} for principal {PrincipalId}",
                user.Id, user.AxonPrincipalId);

            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Identity user {UserId}", user.Id);
            return IdentityResult.Failed(new IdentityError
            {
                Code = "CreateFailed",
                Description = $"Failed to create user: {ex.Message}"
            });
        }
    }

    public async Task<IdentityResult> UpdateAsync(AxonUserAuth user, CancellationToken ct)
    {
        try
        {
            _dbContext.Set<AxonUserAuth>().Update(user);
            await _dbContext.SaveChangesAsync(ct);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Identity user {UserId}", user.Id);
            return IdentityResult.Failed(new IdentityError
            {
                Code = "UpdateFailed",
                Description = $"Failed to update user: {ex.Message}"
            });
        }
    }

    public async Task<IdentityResult> DeleteAsync(AxonUserAuth user, CancellationToken ct)
    {
        try
        {
            _dbContext.Set<AxonUserAuth>().Remove(user);
            await _dbContext.SaveChangesAsync(ct);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Identity user {UserId}", user.Id);
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DeleteFailed",
                Description = $"Failed to delete user: {ex.Message}"
            });
        }
    }

    public async Task<AxonUserAuth?> FindByIdAsync(string userId, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var id))
            return null;

        return await _dbContext.Set<AxonUserAuth>()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<AxonUserAuth?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
    {
        return await _dbContext.Set<AxonUserAuth>()
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, ct);
    }

    public Task<string> GetUserIdAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.UserName);

    public Task SetUserNameAsync(AxonUserAuth user, string? userName, CancellationToken ct)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(AxonUserAuth user, string? normalizedName, CancellationToken ct)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // No resources to dispose
    }

    #endregion

    #region IUserLoginStore Implementation

    public async Task AddLoginAsync(AxonUserAuth user, UserLoginInfo login, CancellationToken ct)
    {
        // For wallet-based auth, we track this through IdentityCredential in the domain
        var principal = await _principalRepo.GetByIdAsync(user.AxonPrincipalId, ct);
        if (principal != null)
        {
            var credential = IdentityCredential.Create(
                user.AxonPrincipalId,
                login.LoginProvider,
                login.ProviderDisplayName ?? login.LoginProvider,
                login.ProviderKey);

            var addResult = principal.AddCredential(credential, (provider, issuer, subject) =>
                Result.Success<bool, Error>(false));

            if (addResult.IsSuccess)
            {
                await _principalRepo.UpdateAsync(principal, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }
    }

    public async Task RemoveLoginAsync(AxonUserAuth user, string loginProvider, string providerKey, CancellationToken ct)
    {
        var principal = await _principalRepo.GetByIdAsync(user.AxonPrincipalId, ct);
        if (principal != null)
        {
            var credential = principal.Credentials
                .FirstOrDefault(c => c.Provider == loginProvider && c.Subject == providerKey);

            if (credential != null)
            {
                // Manual removal since no domain method exists yet
                // This would need a proper domain method in a real implementation
                var field = typeof(AxonPrincipal)
                    .GetField("_credentials", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (field?.GetValue(principal) is List<IdentityCredential> credentialsList)
                {
                    credentialsList.Remove(credential);
                }

                await _principalRepo.UpdateAsync(principal, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(AxonUserAuth user, CancellationToken ct)
    {
        var principal = await _principalRepo.GetByIdAsync(user.AxonPrincipalId, ct);
        if (principal == null)
            return new List<UserLoginInfo>();

        return principal.Credentials
            .Select(c => new UserLoginInfo(c.Provider, c.Subject, c.Provider))
            .ToList();
    }

    public async Task<AxonUserAuth?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken ct)
    {
        return await _dbContext.Set<AxonUserAuth>()
            .FirstOrDefaultAsync(u =>
                u.ProviderType == loginProvider &&
                u.OriginalSubject == providerKey, ct);
    }

    #endregion

    #region IUserSecurityStampStore Implementation

    public Task<string?> GetSecurityStampAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.SecurityStamp);

    public Task SetSecurityStampAsync(AxonUserAuth user, string? stamp, CancellationToken ct)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    #endregion

    #region IUserClaimStore Implementation

    public async Task<IList<Claim>> GetClaimsAsync(AxonUserAuth user, CancellationToken ct)
    {
        var claims = new List<Claim>
        {
            new("axon_user_id", user.AxonPrincipalId.Value.ToString()),
            new("provider_type", user.ProviderType),
            new("original_issuer", user.OriginalIssuer),
            new("original_subject", user.OriginalSubject)
        };

        if (!string.IsNullOrEmpty(user.DynamicEnvironmentId))
            claims.Add(new("dynamic_environment_id", user.DynamicEnvironmentId));

        if (!string.IsNullOrEmpty(user.DynamicUserId))
            claims.Add(new("dynamic_user_id", user.DynamicUserId));

        if (!string.IsNullOrEmpty(user.PrimaryChainId))
            claims.Add(new("primary_chain_id", user.PrimaryChainId));

        if (!string.IsNullOrEmpty(user.PrimaryWalletAddress))
            claims.Add(new("primary_wallet_address", user.PrimaryWalletAddress));

        return claims;
    }

    public Task AddClaimsAsync(AxonUserAuth user, IEnumerable<Claim> claims, CancellationToken ct)
    {
        // Claims are derived from user properties, not stored separately
        // This is intentionally a no-op for wallet-based authentication
        return Task.CompletedTask;
    }

    public Task ReplaceClaimAsync(AxonUserAuth user, Claim claim, Claim newClaim, CancellationToken ct)
    {
        // Claims are derived from user properties, not stored separately
        // This is intentionally a no-op for wallet-based authentication
        return Task.CompletedTask;
    }

    public Task RemoveClaimsAsync(AxonUserAuth user, IEnumerable<Claim> claims, CancellationToken ct)
    {
        // Claims are derived from user properties, not stored separately
        // This is intentionally a no-op for wallet-based authentication
        return Task.CompletedTask;
    }

    public async Task<IList<AxonUserAuth>> GetUsersForClaimAsync(Claim claim, CancellationToken ct)
    {
        var query = _dbContext.Set<AxonUserAuth>().AsQueryable();

        query = claim.Type switch
        {
            "provider_type" => query.Where(u => u.ProviderType == claim.Value),
            "original_issuer" => query.Where(u => u.OriginalIssuer == claim.Value),
            "dynamic_environment_id" => query.Where(u => u.DynamicEnvironmentId == claim.Value),
            "primary_chain_id" => query.Where(u => u.PrimaryChainId == claim.Value),
            _ => query.Where(_ => false) // Unknown claim type returns empty
        };

        return await query.ToListAsync(ct);
    }

    #endregion

    #region IUserEmailStore Implementation

    public Task<string?> GetEmailAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.Email);

    public Task SetEmailAsync(AxonUserAuth user, string? email, CancellationToken ct)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<bool> GetEmailConfirmedAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(AxonUserAuth user, bool confirmed, CancellationToken ct)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public async Task<AxonUserAuth?> FindByEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        return await _dbContext.Set<AxonUserAuth>()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
    }

    public Task<string?> GetNormalizedEmailAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(AxonUserAuth user, string? normalizedEmail, CancellationToken ct)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    #endregion

    #region IUserPhoneNumberStore Implementation

    public Task<string?> GetPhoneNumberAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.PhoneNumber);

    public Task SetPhoneNumberAsync(AxonUserAuth user, string? phoneNumber, CancellationToken ct)
    {
        user.PhoneNumber = phoneNumber;
        return Task.CompletedTask;
    }

    public Task<bool> GetPhoneNumberConfirmedAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.PhoneNumberConfirmed);

    public Task SetPhoneNumberConfirmedAsync(AxonUserAuth user, bool confirmed, CancellationToken ct)
    {
        user.PhoneNumberConfirmed = confirmed;
        return Task.CompletedTask;
    }

    #endregion

    #region IUserTwoFactorStore Implementation

    public Task<bool> GetTwoFactorEnabledAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.TwoFactorEnabled);

    public Task SetTwoFactorEnabledAsync(AxonUserAuth user, bool enabled, CancellationToken ct)
    {
        user.TwoFactorEnabled = enabled;
        return Task.CompletedTask;
    }

    #endregion

    #region IUserLockoutStore Implementation

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(AxonUserAuth user, DateTimeOffset? lockoutEnd, CancellationToken ct)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(AxonUserAuth user, CancellationToken ct)
    {
        user.AccessFailedCount++;
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task ResetAccessFailedCountAsync(AxonUserAuth user, CancellationToken ct)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(AxonUserAuth user, CancellationToken ct)
        => Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(AxonUserAuth user, bool enabled, CancellationToken ct)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    #endregion

    #region Wallet-Specific Methods

    /// <summary>
    /// Find user by wallet address through domain aggregates
    /// </summary>
    public async Task<AxonUserAuth?> FindByWalletAsync(
        string chainId,
        string address,
        CancellationToken ct = default)
    {
        // Query through Wallet → WalletOwnership → AxonPrincipal → AxonUserAuth
        var wallet = await _readDbContext.Wallets
            .Where(w => w.ChainId == chainId)
            .FirstOrDefaultAsync(w => EF.Functions.ILike(w.Address.Value, address), ct);

        if (wallet == null)
            return null;

        var walletOwnership = await _readDbContext.WalletOwnerships
            .Where(wo => wo.WalletId == wallet.Id)
            .Where(wo => wo.Status == OwnershipStatus.Verified)
            .FirstOrDefaultAsync(ct);

        if (walletOwnership == null)
            return null;

        return await _dbContext.Set<AxonUserAuth>()
            .FirstOrDefaultAsync(u => u.AxonPrincipalId == walletOwnership.PrincipalId, ct);
    }

    /// <summary>
    /// Find user by provider and subject
    /// </summary>
    public async Task<AxonUserAuth?> FindByProviderAsync(
        string providerType,
        string subject,
        CancellationToken ct = default)
    {
        return await _dbContext.Set<AxonUserAuth>()
            .FirstOrDefaultAsync(u =>
                u.ProviderType == providerType &&
                u.OriginalSubject == subject, ct);
    }

    #endregion
}