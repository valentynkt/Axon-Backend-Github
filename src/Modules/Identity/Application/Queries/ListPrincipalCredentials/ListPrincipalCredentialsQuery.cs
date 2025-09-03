using Axon.Modules.Identity.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Queries.ListPrincipalCredentials;

/// <summary>
/// Query to retrieve all active credentials for a principal, sorted by LastSeenAt descending.
/// Returns only active (non-deleted) credentials with metadata keys but no values.
/// </summary>
/// <param name="AxonId">The unique identifier of the principal whose credentials to retrieve</param>
public sealed record ListPrincipalCredentialsQuery(
    AxonId AxonId
) : IRequest<Result<IReadOnlyList<CredentialDto>, Error>>;