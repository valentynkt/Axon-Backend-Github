using BuildingBlocks.Core.Abstractions.CQRS;

namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry.CoreDiagnostics;

/// <summary>
/// Resolves handler types for commands and queries to avoid reflection duplication across observability components.
/// </summary>
internal static class HandlerTypeResolver
{
    /// <summary>
    /// Finds the command handler type for the specified command type using reflection.
    /// </summary>
    /// <typeparam name="TCommand">The command type to find the handler for.</typeparam>
    /// <returns>The handler type if found; otherwise, null.</returns>
    public static Type? FindCommandHandlerType<TCommand>()
    {
        return typeof(TCommand)
            .Assembly.GetTypes()
            .FirstOrDefault(t =>
                t.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType
                        && i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
                        && i.GetGenericArguments()[0] == typeof(TCommand)
                    )
            );
    }

    /// <summary>
    /// Finds the query handler type for the specified query type using reflection.
    /// </summary>
    /// <typeparam name="TQuery">The query type to find the handler for.</typeparam>
    /// <returns>The handler type if found; otherwise, null.</returns>
    public static Type? FindQueryHandlerType<TQuery>()
    {
        return typeof(TQuery)
            .Assembly.GetTypes()
            .FirstOrDefault(t =>
                t.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType
                        && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)
                        && i.GetGenericArguments()[0] == typeof(TQuery)
                    )
            );
    }

    /// <summary>
    /// Creates telemetry tags for command execution tracing.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <returns>A dictionary of telemetry tags.</returns>
    public static Dictionary<string, object?> CreateCommandTags<TCommand>()
    {
        var commandName = typeof(TCommand).Name;
        var handlerType = FindCommandHandlerType<TCommand>();
        var commandHandlerName = handlerType?.Name;

        return new Dictionary<string, object?>
        {
            { TelemetryTags.Tracing.Application.Commands.Command, commandName },
            { TelemetryTags.Tracing.Application.Commands.CommandType, typeof(TCommand).FullName },
            { TelemetryTags.Tracing.Application.Commands.CommandHandler, commandHandlerName },
            { TelemetryTags.Tracing.Application.Commands.CommandHandlerType, handlerType?.FullName },
        };
    }

    /// <summary>
    /// Creates telemetry tags for query execution tracing.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <returns>A dictionary of telemetry tags.</returns>
    public static Dictionary<string, object?> CreateQueryTags<TQuery>()
    {
        var queryName = typeof(TQuery).Name;
        var handlerType = FindQueryHandlerType<TQuery>();
        var queryHandlerName = handlerType?.Name;

        return new Dictionary<string, object?>
        {
            { TelemetryTags.Tracing.Application.Queries.Query, queryName },
            { TelemetryTags.Tracing.Application.Queries.QueryType, typeof(TQuery).FullName },
            { TelemetryTags.Tracing.Application.Queries.QueryHandler, queryHandlerName },
            { TelemetryTags.Tracing.Application.Queries.QueryHandlerType, handlerType?.FullName },
        };
    }

    /// <summary>
    /// Creates an activity name for command execution.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <returns>A formatted activity name.</returns>
    public static string CreateCommandActivityName<TCommand>()
    {
        var commandName = typeof(TCommand).Name;
        var handlerType = FindCommandHandlerType<TCommand>();
        var commandHandlerName = handlerType?.Name;
        
        return $"{ObservabilityConstant.Components.CommandHandler}.{commandHandlerName}/{commandName}";
    }

    /// <summary>
    /// Creates an activity name for query execution.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <returns>A formatted activity name.</returns>
    public static string CreateQueryActivityName<TQuery>()
    {
        var queryName = typeof(TQuery).Name;
        var handlerType = FindQueryHandlerType<TQuery>();
        var queryHandlerName = handlerType?.Name;
        
        return $"{ObservabilityConstant.Components.QueryHandler}.{queryHandlerName}/{queryName}";
    }
}