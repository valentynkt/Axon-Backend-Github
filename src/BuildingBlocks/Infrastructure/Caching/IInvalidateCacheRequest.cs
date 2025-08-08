namespace BuildingBlocks.Infrastructure.Caching
{
    public interface IInvalidateCacheRequest
    {
        string CacheKey { get; }
    }
}