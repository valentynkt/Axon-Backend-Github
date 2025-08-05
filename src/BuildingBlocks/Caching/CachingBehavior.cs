using EasyCaching.Core;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Caching;

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull
{
    private readonly IEasyCachingProvider _cachingProvider;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;
    private readonly int defaultCacheExpirationInHours = 1;

    public CachingBehavior(IEasyCachingProviderFactory cachingFactory,
        ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
        _cachingProvider = cachingFactory.GetCachingProvider("mem");
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheRequest cacheRequest)
            // No cache request found, so just continue through the pipeline
            return await next(cancellationToken);

        var cacheKey = cacheRequest.CacheKey;
        var cachedResponse = await _cachingProvider.GetAsync<TResponse>(cacheKey, cancellationToken);
        if (cachedResponse.Value != null)
        {
            _logger.LogDebug("Response retrieved {TRequest} from cache. CacheKey: {CacheKey}",
                typeof(TRequest).FullName, cacheKey);
            return cachedResponse.Value;
        }

        var response = await next(cancellationToken);

        var expirationTime = cacheRequest.AbsoluteExpirationRelativeToNow ??
                             DateTime.Now.AddHours(defaultCacheExpirationInHours);

        await _cachingProvider.SetAsync(cacheKey, response, expirationTime.TimeOfDay, cancellationToken);

        _logger.LogDebug("Caching response for {TRequest} with cache key: {CacheKey}", typeof(TRequest).FullName,
            cacheKey);

        return response;
    }
}