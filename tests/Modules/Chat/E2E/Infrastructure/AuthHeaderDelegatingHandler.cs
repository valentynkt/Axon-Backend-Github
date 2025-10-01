namespace Axon.Modules.Chat.E2E.Infrastructure;

/// <summary>
/// HTTP message handler that automatically injects authorization headers into all outgoing requests.
/// This ensures consistent authentication across all HTTP requests in E2E tests,
/// solving the issue where HttpClient.DefaultRequestHeaders.Authorization was not persisting correctly.
/// </summary>
public sealed class AuthHeaderDelegatingHandler : DelegatingHandler
{
    private string? _currentBearerToken;
    private readonly object _lock = new();

    /// <summary>
    /// Sets the bearer token to be used for all subsequent requests.
    /// </summary>
    public void SetBearerToken(string? token)
    {
        lock (_lock)
        {
            _currentBearerToken = token;
        }
    }

    /// <summary>
    /// Clears the current bearer token, causing subsequent requests to have no authorization header.
    /// </summary>
    public void ClearBearerToken()
    {
        lock (_lock)
        {
            _currentBearerToken = null;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? token;
        lock (_lock)
        {
            token = _currentBearerToken;
        }

        // Inject authorization header if we have a token
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
