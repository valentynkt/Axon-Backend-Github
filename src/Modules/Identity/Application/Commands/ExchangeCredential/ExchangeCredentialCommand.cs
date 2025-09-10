using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Exchange;

namespace Axon.Modules.Identity.Application.Commands.ExchangeCredential;

/// <summary>
/// Command to exchange validated Dynamic credential data for Axon identity with atomic operations.
/// Implements the comprehensive exchange flow with batch wallet operations, ownership linking,
/// defaults application, and conflict detection in a single transaction.
/// </summary>
/// <param name="UserData">Validated and normalized user data from Dynamic JWT including wallets</param>
public sealed record ExchangeCredentialCommand(ExchangeUserData UserData) : IdentityBaseCommand<ExchangeOutcome>;