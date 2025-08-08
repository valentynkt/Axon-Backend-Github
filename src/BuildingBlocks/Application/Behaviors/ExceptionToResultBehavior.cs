using System.Linq.Expressions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

public sealed class ExceptionToResultBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult
{
    private readonly ILogger<ExceptionToResultBehavior<TRequest, TResponse>> _logger;

    public ExceptionToResultBehavior(ILogger<ExceptionToResultBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            // MediatR delegate has no token parameter
            var response = await next();

            if (ResultFactory<TResponse>.IsSuccess(response))
                _logger.LogDebug("Successfully handled {RequestType}", typeof(TRequest).Name);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Never convert cancellations to failures
            _logger.LogWarning("{RequestType} was cancelled", typeof(TRequest).Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {RequestType}: {Message}", typeof(TRequest).Name, ex.Message);

            var error = Error.FromException(ex);
            return ResultFactory<TResponse>.Failure(error);
        }
    }

    // Compiled, reflection-free factories per closed TResponse
    private static class ResultFactory<T>
        where T : IResult
    {
        public static readonly Func<T, bool> IsSuccess = CompileIsSuccess();
        public static readonly Func<Error, T> Failure = CompileFailure();

        private static Func<T, bool> CompileIsSuccess()
        {
            var p = Expression.Parameter(typeof(T), "r");
            var prop = typeof(T).GetProperty("IsSuccess")
                       ?? throw new InvalidOperationException($"{typeof(T).Name} missing IsSuccess property");
            var get = Expression.Property(p, prop);
            var lambda = Expression.Lambda<Func<T, bool>>(get, p);
            return lambda.Compile();
        }

        private static Func<Error, T> CompileFailure()
        {
            // Handles both Result and Result<TValue>
            if (typeof(T) == typeof(Result))
            {
                var e = Expression.Parameter(typeof(Error), "e");
                var call = Expression.Call(
                    typeof(Result),
                    nameof(Result.Failure),
                    Type.EmptyTypes,
                    e);
                var body = Expression.Convert(call, typeof(T));
                return Expression.Lambda<Func<Error, T>>(body, e).Compile();
            }

            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var e = Expression.Parameter(typeof(Error), "e");
                var closed = typeof(T);
                var method = closed.GetMethod("Failure", new[] { typeof(Error) })
                             ?? throw new InvalidOperationException($"{closed.Name}.Failure(Error) not found");
                var call = Expression.Call(method, e);
                var lambda = Expression.Lambda<Func<Error, T>>(call, e);
                return lambda.Compile();
            }

            throw new InvalidOperationException($"Unsupported result type: {typeof(T).Name}");
        }
    }
}
