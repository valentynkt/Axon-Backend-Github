// /BuildingBlocks/Core/Idempotency/IdempotencyKeyResolver.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Abstractions.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Core.Idempotency;

public sealed class IdempotencyKeyResolver
{
    private readonly IOptions<IdempotencyOptions> _options;
    private readonly IServiceProvider _sp;
    private readonly IHttpContextAccessor? _http;
    private readonly ICurrentUserService? _currentUser;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public IdempotencyKeyResolver(
        IOptions<IdempotencyOptions> options,
        IServiceProvider sp,
        IHttpContextAccessor? http = null,
        ICurrentUserService? currentUser = null)
    {
        _options = options;
        _sp = sp;
        _http = http;
        _currentUser = currentUser;
    }

    public string Resolve<TRequest>(TRequest request)
        where TRequest : IIdempotentCommand
    {
        // 1) Request explicit
        var explicitKey = request.GetExplicitIdempotencyKey();
        if (!string.IsNullOrWhiteSpace(explicitKey))
            return Compose(explicitKey!, typeof(TRequest));

        // 2) HTTP header
        var headerName = _options.Value.HeaderName;
        var headerKey = _http?.HttpContext?.Request?.Headers[headerName].ToString();
        if (!string.IsNullOrWhiteSpace(headerKey))
            return Compose(headerKey!, typeof(TRequest));

        // 3) Domain provider (if registered)
        var provider = _sp.GetService(typeof(IIdempotencyKeyProvider<TRequest>))
                      as IIdempotencyKeyProvider<TRequest>;
        if (provider is not null && _currentUser is not null)
        {
            var k = provider.GenerateKey(request, _currentUser);
            if (!string.IsNullOrWhiteSpace(k))
            {
                // IMPORTANT: return provider key *as-is* to allow cross-command de-duplication
                // (i.e., no request-type suffix). Providers should namespace their keys.
                return k!;
            }
        }

        // 4) Generic, deterministic fallback (prefix + type + optional user + optional payload hash)
        var parts = new List<string> { _options.Value.KeyPrefix, typeof(TRequest).FullName ?? typeof(TRequest).Name };

        if (_options.Value.IncludeUserInKey && _currentUser?.AxonUserId is { Length: > 0 })
            parts.Add(_currentUser.AxonUserId);

        if (_options.Value.IncludePayloadHash)
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            parts.Add(Sha256Base64(json));
        }

        return string.Join(':', parts);
    }

    private static string Compose(string baseKey, Type t)
        => $"{baseKey}:{t.FullName ?? t.Name}";

    private static string Sha256Base64(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
