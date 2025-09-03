using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Queries.GetPrincipalByCredential;

/// <summary>
/// Query to retrieve a principal by their identity credential.
/// Returns both the principal and the matching credential.
/// Uses primitive types for parameters.
/// </summary>
/// <param name="ProviderType">The identity provider type (max 50 chars)</param>
/// <param name="Issuer">The credential issuer (max 255 chars)</param>
/// <param name="Subject">The credential subject (max 255 chars)</param>
public sealed record GetPrincipalByCredentialQuery(
    string ProviderType,
    string Issuer,
    string Subject
) : IRequest<Result<PrincipalWithCredentialDto, Error>>;