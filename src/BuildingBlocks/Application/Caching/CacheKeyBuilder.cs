using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Abstractions.CQRS.Attributes;
using BuildingBlocks.Core.Abstractions.CQRS.Policies;

namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Builds deterministic, stable cache keys with partition support.
/// Excludes volatile fields and includes tenant/user partitioning for secure multi-tenancy.
/// </summary>
public static class CacheKeyBuilder
{
    private static readonly HashSet<string> VolatileProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "RequestId", "RequestedAt", "TraceId", "SpanId", "ParentSpanId"
    };

    /// <summary>
    /// Builds a deterministic cache key with format: q:{prefix}:{partition}:{hash16}
    /// </summary>
    /// <param name="request">The cacheable query request</param>
    /// <param name="currentUserService">Service for accessing current user/tenant context</param>
    /// <returns>Deterministic cache key</returns>
    public static string BuildKey<TRequest>(TRequest request, ICurrentUserService? currentUserService = null)
        where TRequest : ICacheableQuery
    {
        var prefix = string.IsNullOrWhiteSpace(request.CacheKeyPrefix) 
            ? typeof(TRequest).Name 
            : request.CacheKeyPrefix!;

        var partition = BuildPartition(request, currentUserService);
        var hash = BuildStableHash(request);

        return $"q:{prefix}:{partition}:{hash}";
    }

    /// <summary>
    /// Builds partition string based on request scope and current user context
    /// </summary>
    private static string BuildPartition<TRequest>(TRequest request, ICurrentUserService? currentUserService)
        where TRequest : ICacheableQuery
    {
        return request.PartitionScope switch
        {
            PartitionScope.Global => "global",
            PartitionScope.Tenant => GetTenantPartition(currentUserService),
            PartitionScope.User => GetUserPartition(currentUserService),
            _ => "global"
        };
    }

    /// <summary>
    /// Gets tenant partition identifier
    /// </summary>
    private static string GetTenantPartition(ICurrentUserService? currentUserService)
    {
        // For now, since ICurrentUserService doesn't expose TenantId, use AxonUserId as tenant proxy
        // This should be updated when ITenantService or enhanced ICurrentUserService is available
        var userId = currentUserService?.AxonUserId;
        return string.IsNullOrEmpty(userId) ? "global" : $"tenant:{userId}";
    }

    /// <summary>
    /// Gets user partition identifier
    /// </summary>
    private static string GetUserPartition(ICurrentUserService? currentUserService)
    {
        var userId = currentUserService?.AxonUserId;
        return string.IsNullOrEmpty(userId) ? "global" : $"user:{userId}";
    }

    /// <summary>
    /// Builds stable hash excluding volatile fields and properties marked with [CacheKeyIgnore]
    /// </summary>
    private static string BuildStableHash<TRequest>(TRequest request)
    {
        try
        {
            var filteredRequest = CreateFilteredObject(request);
            var json = JsonSerializer.Serialize(filteredRequest, CacheSerializationOptions.Default);
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
        }
        catch
        {
            // Fallback: use type name + ToString() hash
            var fallback = request?.ToString() ?? typeof(TRequest).FullName ?? typeof(TRequest).Name;
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(fallback));
            return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
        }
    }

    /// <summary>
    /// Creates a filtered object excluding volatile properties and [CacheKeyIgnore] marked properties
    /// </summary>
    private static Dictionary<string, object?> CreateFilteredObject<TRequest>(TRequest request)
    {
        var requestType = typeof(TRequest);
        var properties = requestType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var filteredData = new Dictionary<string, object?>();

        foreach (var prop in properties)
        {
            // Skip volatile properties
            if (VolatileProperties.Contains(prop.Name))
                continue;

            // Skip properties marked with [CacheKeyIgnore]
            if (prop.GetCustomAttribute<CacheKeyIgnoreAttribute>() != null)
                continue;

            // Skip properties that can't be read
            if (!prop.CanRead)
                continue;

            try
            {
                var value = prop.GetValue(request);
                filteredData[prop.Name] = value;
            }
            catch
            {
                // Skip properties that throw during access
            }
        }

        return filteredData;
    }
}