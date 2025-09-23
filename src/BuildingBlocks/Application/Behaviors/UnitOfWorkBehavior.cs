// /BuildingBlocks/Application/Behaviors/UnitOfWorkBehavior.cs
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UnitOfWorkBehavior<TRequest, TResponse>> _logger;

    public UnitOfWorkBehavior(IServiceProvider serviceProvider, ILogger<UnitOfWorkBehavior<TRequest, TResponse>> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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

        // Resolve the correct module-specific UnitOfWork based on command namespace
        var uow = ResolveUnitOfWork(request);
        if (uow == null)
        {
            _logger.LogWarning("No UnitOfWork found for command {Command}. Proceeding without transaction.", typeof(TRequest).Name);
            return await next();
        }

        // Wrap handler + commit in one transaction
        return await uow.ExecuteInTransactionAsync<TResponse>(async token =>
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

            await uow.SaveChangesAsync(token);
            _logger.LogDebug("Command {Command} committed successfully.", typeof(TRequest).Name);
            return response;

        }, ct);
    }

    /// <summary>
    /// Resolves the appropriate module-specific UnitOfWork based on the command's namespace
    /// </summary>
    private IWriteUnitOfWork? ResolveUnitOfWork(TRequest request)
    {
        var requestType = request.GetType();
        var namespaceName = requestType.Namespace ?? string.Empty;

        try
        {
            // Chat module commands
            if (namespaceName.Contains("Axon.Modules.Chat"))
            {
                var chatModuleType = Type.GetType("Axon.Modules.Chat.Application.Common.Models.ChatModule, Axon.Modules.Chat.Application");
                if (chatModuleType != null)
                {
                    var uowType = typeof(IWriteUnitOfWork<>).MakeGenericType(chatModuleType);
                    return _serviceProvider.GetService(uowType) as IWriteUnitOfWork;
                }
            }
            // Identity module commands
            else if (namespaceName.Contains("Axon.Modules.Identity"))
            {
                var identityModuleType = Type.GetType("Axon.Modules.Identity.Application.Common.Models.IdentityModule, Axon.Modules.Identity.Application");
                if (identityModuleType != null)
                {
                    var uowType = typeof(IWriteUnitOfWork<>).MakeGenericType(identityModuleType);
                    return _serviceProvider.GetService(uowType) as IWriteUnitOfWork;
                }
            }

            _logger.LogWarning("Unknown module for command {Command} with namespace {Namespace}",
                requestType.Name, namespaceName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve UnitOfWork for command {Command}", requestType.Name);
            return null;
        }
    }

    /// <summary>
    /// Per-closed-TResponse compiled accessors for CFE results.
    /// Supports: Result, Result&lt;T&gt;, Result&lt;T, Error&gt;, UnitResult&lt;Error&gt;.
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
