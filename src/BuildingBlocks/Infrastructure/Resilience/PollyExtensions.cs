using Microsoft.Extensions.Logging;
using Polly;

namespace BuildingBlocks.Infrastructure.Resilience;

using Exception = Exception;

public static class PollyExtensions
{
    public static ILogger Logger { get; set; } = null!;

    public static T RetryOnFailure<T>(Func<T> action, int retryCount = 3)
    {
        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry(retryCount, (exception, retryAttempt, context) =>
            {
                Logger.LogInformation("Retry attempt: {RetryAttempt}", retryAttempt);
                Logger.LogError("Exception: {ExceptionMessage}", exception.Message);
            });

        return retryPolicy.Execute(action);
    }
}