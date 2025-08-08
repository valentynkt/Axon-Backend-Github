using Microsoft.Extensions.Logging;
using Polly;

namespace BuildingBlocks.Infrastructure.Resilience;

using Exception = System.Exception;

public static class PollyExtensions
{
    public static ILogger Logger { get; set; } = null!;

    public static T RetryOnFailure<T>(this object retrySource, Func<T> action, int retryCount = 3)
    {
        _ = retrySource; // Extension method parameter
        
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