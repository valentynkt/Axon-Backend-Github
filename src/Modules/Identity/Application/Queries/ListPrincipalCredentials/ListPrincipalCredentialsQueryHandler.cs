using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Queries.ListPrincipalCredentials;

/// <summary>
/// Handler for ListPrincipalCredentialsQuery that retrieves all active credentials for a principal.
/// 
/// Repository Strategy: Intentionally reuses IAxonPrincipalWriteRepository for reads
/// following YAGNI principle. Loads principal and filters credentials in memory.
/// Uses non-tracking queries for performance optimization.
/// 
/// Returns credentials sorted by LastSeenAt descending (most recently used first).
/// Logs warning if >500 credentials for future pagination consideration.
/// </summary>
public sealed class ListPrincipalCredentialsQueryHandler 
    : IRequestHandler<ListPrincipalCredentialsQuery, Result<IReadOnlyList<CredentialDto>, Error>>
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly IWalletAuthorizationService _authorizationService;
    private readonly ILogger<ListPrincipalCredentialsQueryHandler> _logger;
    private const int LargeResultWarningThreshold = 500;

    public ListPrincipalCredentialsQueryHandler(
        IAxonPrincipalWriteRepository principalRepository,
        IWalletAuthorizationService authorizationService,
        ILogger<ListPrincipalCredentialsQueryHandler> logger)
    {
        _principalRepository = principalRepository ?? throw new ArgumentNullException(nameof(principalRepository));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<IReadOnlyList<CredentialDto>, Error>> Handle(
        ListPrincipalCredentialsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        try
        {
            // Check authorization - only allow self-access or admin permissions
            if (!await _authorizationService.CanViewPrincipalAsync(request.AxonId, cancellationToken))
            {
                return Result.Failure<IReadOnlyList<CredentialDto>, Error>(
                    Error.Forbidden("Access denied to principal credentials.", "IDENTITY.QUERY.CREDENTIALS.ACCESS_DENIED"));
            }
            var principal = await _principalRepository.GetByIdAsync(request.AxonId, cancellationToken);
            
            if (principal is null || principal.IsDeleted)
            {
                return Result.Failure<IReadOnlyList<CredentialDto>, Error>(
                    Error.NotFound("Principal not found", "IDENTITY.QUERY.PRINCIPAL.NOT_FOUND"));
            }

            var activeCredentials = principal.Credentials
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.LastSeenAt)
                .ToList();

            if (activeCredentials.Count > LargeResultWarningThreshold)
            {
                _logger.LogWarning(
                    "Principal {AxonId} has {Count} credentials, consider implementing pagination",
                    request.AxonId, activeCredentials.Count);
            }

            var credentialDtos = activeCredentials
                .Select(IdentityDtoMapper.ToCredentialDto)
                .ToList()
                .AsReadOnly();

            _logger.LogDebug(
                "ListPrincipalCredentials {AxonId} count={Count}",
                request.AxonId, credentialDtos.Count);

            return Result.Success<IReadOnlyList<CredentialDto>, Error>(credentialDtos);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<CredentialDto>, Error>(
                Error.Internal(
                    "Failed to list credentials for principal",
                    "IDENTITY.QUERY.CREDENTIALS.LIST_FAILED",
                    ex));
        }
    }
}