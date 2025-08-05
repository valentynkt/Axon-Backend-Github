using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web;

public interface ICurrentUserProvider
{
    long? GetCurrentUserId();
}

public class CurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }


    public long? GetCurrentUserId()
    {
        var nameIdentifier = _httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (long.TryParse(nameIdentifier, out var userId))
        {
            return userId;
        }

        return null;
    }
}