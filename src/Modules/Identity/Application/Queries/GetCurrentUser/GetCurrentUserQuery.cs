using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Query to retrieve current authenticated user information
/// </summary>
public record GetCurrentUserQuery(string JwtToken) : IRequest<Result<CurrentUserResult, Error>>;