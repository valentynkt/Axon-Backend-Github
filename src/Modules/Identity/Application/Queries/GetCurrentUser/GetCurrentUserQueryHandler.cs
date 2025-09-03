using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Production-ready handler for retrieving current authenticated user information.
/// Returns only domain-backed data with no stubs or fabricated values.
/// Uses optimized queries to avoid N+1 problems.
/// </summary>
public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserResult, Error>>
{
    private readonly IWalletAuthorizationService _walletAuthorizationService;
    private readonly IAxonPrincipalReadRepository _principalRepository;
    private readonly IWalletReadRepository _walletRepository;
    private readonly ILogger<GetCurrentUserQueryHandler> _logger;
    public GetCurrentUserQueryHandler(
        IWalletAuthorizationService walletAuthorizationService,
        IAxonPrincipalReadRepository principalRepository,
        IWalletReadRepository walletRepository,
        ILogger<GetCurrentUserQueryHandler> logger)
    {
        _walletAuthorizationService = walletAuthorizationService ?? throw new ArgumentNullException(nameof(walletAuthorizationService));
        _principalRepository = principalRepository ?? throw new ArgumentNullException(nameof(principalRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public async Task<Result<CurrentUserResult, Error>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        using var activity = _logger.BeginScope("GetCurrentUser");
        
        try
        {
            // Get current user's AxonId through authorization service
            var axonId = await _walletAuthorizationService.GetCurrentUserAxonIdAsync(cancellationToken);
            if (axonId == null)
            {
                _logger.LogWarning("User is not authenticated or does not have a principal");
                return Result.Failure<CurrentUserResult, Error>(
                    Error.Unauthorized("User is not authenticated", "AUTH.NOT_AUTHENTICATED"));
            }

            _logger.LogDebug("Loading principal {AxonId} with active ownerships", axonId);
            
            // Load principal with active wallet ownerships in single query
            var principal = await _principalRepository.GetByIdWithActiveOwnershipsAsync(axonId.Value, cancellationToken);
            if (principal == null)
            {
                _logger.LogWarning("Principal {AxonId} not found or is deleted", axonId);
                return Result.Failure<CurrentUserResult, Error>(
                    Error.NotFound("Principal not found", "IDENTITY.PRINCIPAL.NOT_FOUND"));
            }

            var activeOwnerships = principal.GetActiveWalletOwnerships();
            _logger.LogDebug("Found {Count} active wallet ownerships for principal {AxonId}", 
                activeOwnerships.Count, axonId);

            // Batch load all owned wallets if any exist
            var wallets = new List<Wallet>();
            if (activeOwnerships.Count > 0)
            {
                var walletIds = activeOwnerships.Select(o => o.WalletId).ToList();
                wallets = (await _walletRepository.GetByIdsAsync(walletIds, includeDeleted: false, cancellationToken)).ToList();
                _logger.LogDebug("Loaded {Count} wallets for principal {AxonId}", wallets.Count, axonId);
            }

            // Map to DTOs
            var result = MapToCurrentUserResult(principal, activeOwnerships, wallets);
            
            _logger.LogInformation("Successfully retrieved current user data for principal {AxonId} with {WalletCount} wallets", 
                axonId, activeOwnerships.Count);
                
            return Result.Success<CurrentUserResult, Error>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user data");
            return Result.Failure<CurrentUserResult, Error>(
                Error.Failure("An error occurred retrieving user data", "IDENTITY.GET_CURRENT_USER.FAILED"));
        }
    }
    
    private static CurrentUserResult MapToCurrentUserResult(
        Domain.Aggregates.AxonPrincipal.AxonPrincipal principal,
        IReadOnlyCollection<Domain.Entities.WalletOwnership> activeOwnerships,
        IReadOnlyList<Wallet> wallets)
    {
        var now = DateTimeOffset.UtcNow;
        
        // Create wallet lookup for efficient mapping
        var walletLookup = wallets.ToDictionary(w => w.Id, w => w);
        
        // Map profile
        var profileDto = new PrincipalProfileDto
        {
            AxonId = principal.Id.Value.ToString(),
            PrincipalType = principal.Type.Value,
            PreferredLanguage = principal.Profile.PreferredLanguage.Value,
            RiskTier = principal.Profile.RiskTier.Value,
            PrimaryEmailHash = principal.PrimaryEmailHash?.Value,
            CreatedAt = principal.CreatedAt,
            UpdatedAt = principal.UpdatedAt ?? principal.CreatedAt
        };
        
        // Map owned wallets with ownership information
        var ownedWalletDtos = activeOwnerships
            .Where(ownership => walletLookup.ContainsKey(ownership.WalletId))
            .Select(ownership =>
            {
                var wallet = walletLookup[ownership.WalletId];
                return new OwnedWalletDto
                {
                    WalletId = ownership.WalletId.Value.ToString(),
                    Address = wallet.Address.Value,
                    ChainId = ownership.ChainId.Value,
                    ProofType = ownership.ProofType.Value,
                    AccessMode = ownership.AccessMode.Value,
                    OwnershipState = ownership.State.Value,
                    FirstLinkedAt = ownership.FirstLinkedAt,
                    LastVerifiedAt = ownership.LastVerifiedAt,
                    Label = ownership.Label
                };
            })
            .ToList();
        
        // Map chain defaults
        var defaultPerChain = principal.Profile.DefaultPerChain.Value
            .ToDictionary(kvp => kvp.Key.Value, kvp => kvp.Value.ToString());
        
        return new CurrentUserResult
        {
            Profile = profileDto,
            OwnedWallets = ownedWalletDtos,
            DefaultPerChain = defaultPerChain,
            SyncedAt = now
        };
    }
}