using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.Services;

namespace Axon.Modules.Identity.Application.Commands.ExchangeToken;

/// <summary>
/// Command to exchange Dynamic JWT for Axon identity
/// </summary>
public sealed record ExchangeTokenCommand(string Jwt) : IdentityBaseCommand<ExchangeOutcome>;