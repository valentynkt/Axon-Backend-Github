using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Queries.GetPrincipal;

/// <summary>
/// Handler for GetPrincipalQuery that retrieves a principal by AxonId.
/// 
/// Repository Strategy: Intentionally reuses IAxonPrincipalWriteRepository for reads
/// following YAGNI principle. No separate read repository needed until proven necessary.
/// All queries use non-tracking methods ensuring query optimization.
/// 
/// Treats soft-deleted principals as not found.
/// </summary>
public sealed class GetPrincipalQueryHandler : IRequestHandler<GetPrincipalQuery, Result<PrincipalDto, Error>>
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly IWalletAuthorizationService _authorizationService;

    public GetPrincipalQueryHandler(
        IAxonPrincipalWriteRepository principalRepository,
        IWalletAuthorizationService authorizationService)
    {
        _principalRepository = principalRepository ?? throw new ArgumentNullException(nameof(principalRepository));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    public async Task<Result<PrincipalDto, Error>> Handle(
        GetPrincipalQuery request, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        try
        {
            // Check authorization - only allow self-access or admin permissions
            if (!await _authorizationService.CanViewPrincipalAsync(request.AxonId, cancellationToken))
            {
                return Result.Failure<PrincipalDto, Error>(
                    Error.Forbidden("Access denied to principal data.", "IDENTITY.QUERY.PRINCIPAL.ACCESS_DENIED"));
            }
            var principal = await _principalRepository.GetByIdAsync(request.AxonId, cancellationToken);
            
            if (principal is null || principal.IsDeleted)
            {
                return Result.Failure<PrincipalDto, Error>(
                    Error.NotFound("Principal not found", "IDENTITY.QUERY.PRINCIPAL.NOT_FOUND"));
            }

            var principalDto = IdentityDtoMapper.ToPrincipalDto(principal);
            return Result.Success<PrincipalDto, Error>(principalDto);
        }
        catch (Exception ex)
        {
            return Result.Failure<PrincipalDto, Error>(
                Error.Internal(
                    "Failed to retrieve principal", 
                    "IDENTITY.QUERY.PRINCIPAL.FAILED", 
                    ex));
        }
    }
}