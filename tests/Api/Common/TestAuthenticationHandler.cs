using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Axon.Api.Tests.Common;

/// <summary>
/// Test authentication handler that creates a mock authenticated user for testing
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim("environment_id", "test-env"),
            new Claim("wallet:solana", "Sol1234567890"),
            new Claim("wallet:provider:solana", "phantom"),
            // Add JWT-style claims that /auth/me endpoint expects
            new Claim("sub", "test-subject-123"),
            new Claim("iss", "https://test.dynamic.xyz"),
            new Claim("aud", "test-audience")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}