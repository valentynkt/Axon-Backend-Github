// /BuildingBlocks/Application/Behaviors/UnitOfWorkBehavior.cs
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Abstractions.CQRS; // IWriteUnitOfWork
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Wraps command handlers in a single Unit of Work transaction.
/// - Executes handler
/// - On success -> SaveChanges inside the transaction
/// - On failure -> no commit
/// No event plumbing here (keep it in UoW/interceptors/outbox).
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
{
    private readonly IWriteUnitOfWork _uow;
    private readonly ILogger<UnitOfWorkBehavior<TRequest, TResponse>> _logger;

    public UnitOfWorkBehavior(IWriteUnitOfWork uow, ILogger<UnitOfWorkBehavior<TRequest, TResponse>> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // Only apply to commands (not queries)
        var isCommand = request is ICommand
                        || request.GetType().GetInterfaces()
                             .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
        if (!isCommand)
            return await next();

        // Wrap handler + commit in one transaction
        return await _uow.ExecuteInTransactionAsync<TResponse>(async token =>
        {
            var response = await next(); // handler does domain work

            if (!ResultShape<TResponse>.IsSuccess(response))
            {
                var err = ResultShape<TResponse>.TryGetError(response);
                if (err is not null)
                {
                    _logger.LogWarning("Command {Command} failed; skipping commit. Error: {ErrorCode} {Message}",
                        typeof(TRequest).Name, err.Code, err.Message);
                }
                else
                {
                    _logger.LogWarning("Command {Command} failed; skipping commit.", typeof(TRequest).Name);
                }

                return response;
            }

            await _uow.SaveChangesAsync(token);
            _logger.LogDebug("Command {Command} committed successfully.", typeof(TRequest).Name);
            return response;

        }, ct);
    }

    /// <summary>
    /// Per-closed-TResponse compiled accessors for CFE results.
    /// Supports: Result, Result<T>, Result<T, Error>, UnitResult<Error>.
    /// If the shape is unrecognized, we treat as success (commit).
    /// </summary>
    private static class ResultShape<T>
    {
        private static readonly Func<T, bool>?   _isSuccess = CompileIsSuccess();
        private static readonly Func<T, Error?>? _getError  = CompileGetError();

        public static bool IsSuccess(T value)
        {
            if (_isSuccess is null) return true; // not a CFE result → assume success
            return _isSuccess(value);
        }

        public static Error? TryGetError(T value)
        {
            if (_getError is null) return null;
            return _getError(value);
        }

        private static Func<T, bool>? CompileIsSuccess()
        {
            var t = typeof(T);
            var prop = t.GetProperty("IsSuccess", BindingFlags.Public | BindingFlags.Instance);
            if (prop is null || prop.PropertyType != typeof(bool)) return null;

            var p = Expression.Parameter(t, "r");
            var body = Expression.Property(p, prop);
            return Expression.Lambda<Func<T, bool>>(body, p).Compile();
        }

        private static Func<T, Error?>? CompileGetError()
        {
            var t = typeof(T);
            var prop = t.GetProperty("Error", BindingFlags.Public | BindingFlags.Instance);
            if (prop is null) return null;

            // On CFE results Error is of type E (we expect BuildingBlocks.Core.Diagnostics.Errors.Error)
            // Convert if compatible, otherwise return null at runtime
            var p = Expression.Parameter(t, "r");
            var access = Expression.Property(p, prop);
            var convert = Expression.TypeAs(access, typeof(Error));
            return Expression.Lambda<Func<T, Error?>>(convert, p).Compile();
        }
    }
}
