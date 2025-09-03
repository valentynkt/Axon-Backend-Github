using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Queries.GetPrincipalByCredential;

/// <summary>
/// Handler for GetPrincipalByCredentialQuery that resolves a principal by their identity credential.
/// 
/// Repository Strategy: Intentionally reuses IAxonPrincipalWriteRepository for reads
/// following YAGNI principle. Uses existing FindByCredentialAsync method.
/// All queries are non-tracking for optimal performance.
/// 
/// Returns both the principal and the matching active credential.
/// </summary>
public sealed class GetPrincipalByCredentialQueryHandler 
    : IRequestHandler<GetPrincipalByCredentialQuery, Result<PrincipalWithCredentialDto, Error>>
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly IWalletAuthorizationService _authorizationService;

    public GetPrincipalByCredentialQueryHandler(
        IAxonPrincipalWriteRepository principalRepository,
        IWalletAuthorizationService authorizationService)
    {
        _principalRepository = principalRepository ?? throw new ArgumentNullException(nameof(principalRepository));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    public async Task<Result<PrincipalWithCredentialDto, Error>> Handle(
        GetPrincipalByCredentialQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        try
        {
            // Parse primitive string to value object
            var providerTypeResult = ProviderType.Create(request.ProviderType);
            if (providerTypeResult.IsFailure)
            {
                return Result.Failure<PrincipalWithCredentialDto, Error>(
                    Error.Validation("Invalid provider type.", "IDENTITY.QUERY.PROVIDER_TYPE.INVALID"));
            }

            var principal = await _principalRepository.FindByCredentialAsync(
                providerTypeResult.Value,
                request.Issuer,
                request.Subject,
                cancellationToken);

            if (principal is null || principal.IsDeleted)
            {
                return Result.Failure<PrincipalWithCredentialDto, Error>(
                    Error.NotFound(
                        "Principal with specified credential not found",
                        "IDENTITY.QUERY.CREDENTIAL.NOT_FOUND"));
            }

            // Check authorization - only allow self-access or admin permissions
            if (!await _authorizationService.CanViewPrincipalAsync(principal.Id, cancellationToken))
            {
                return Result.Failure<PrincipalWithCredentialDto, Error>(
                    Error.Forbidden("Access denied to principal data.", "IDENTITY.QUERY.PRINCIPAL.ACCESS_DENIED"));
            }

            var matchingCredential = principal.Credentials
                .FirstOrDefault(c => !c.IsDeleted && 
                                   c.Matches(providerTypeResult.Value, request.Issuer, request.Subject));

            if (matchingCredential is null)
            {
                return Result.Failure<PrincipalWithCredentialDto, Error>(
                    Error.NotFound(
                        "Active credential not found",
                        "IDENTITY.QUERY.CREDENTIAL.NOT_FOUND"));
            }

            var principalDto = IdentityDtoMapper.ToPrincipalDto(principal);
            var credentialDto = IdentityDtoMapper.ToCredentialDto(matchingCredential);

            var result = new PrincipalWithCredentialDto(principalDto, credentialDto);
            return Result.Success<PrincipalWithCredentialDto, Error>(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<PrincipalWithCredentialDto, Error>(
                Error.Internal(
                    "Failed to retrieve principal by credential",
                    "IDENTITY.QUERY.PRINCIPAL_BY_CREDENTIAL.FAILED",
                    ex));
        }
    }
}