using Axon.Modules.Identity.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Queries.GetPrincipal;

/// <summary>
/// Query to retrieve a principal by their AxonId.
/// Returns PrincipalDto with computed counts and profile data.
/// </summary>
/// <param name="AxonId">The unique identifier of the principal to retrieve</param>
public sealed record GetPrincipalQuery(
    AxonId AxonId
) : IRequest<Result<PrincipalDto, Error>>;