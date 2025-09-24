using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Exchange;

namespace Axon.Modules.Identity.Application.Commands.ExchangeCredential;

/// <summary>
/// Command to exchange Dynamic JWT bearer token for Axon access token.
/// All validation and processing is delegated to the AuthenticationOrchestrator.
/// </summary>
/// <param name="BearerToken">The raw Dynamic JWT bearer token to exchange</param>
public sealed record ExchangeCredentialCommand(string BearerToken) : IdentityBaseCommand<ExchangeOutcome>;