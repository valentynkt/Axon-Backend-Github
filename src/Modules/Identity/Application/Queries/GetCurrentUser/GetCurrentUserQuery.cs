using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Query to retrieve current authenticated user information from domain model.
/// Production-ready implementation that returns only verified domain-backed data.
/// Authentication context is resolved through IWalletAuthorizationService.
/// </summary>
public record GetCurrentUserQuery : IRequest<Result<CurrentUserResult, Error>>;