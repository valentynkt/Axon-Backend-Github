namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry;

public static class ObservabilityConstant
{
    /// <summary>
    /// Prefix used to build ActivitySource and Meter names.
    /// Default is service name; can be overridden via options.
    /// </summary>
    public static string InstrumentationName { get; set; } = "Axon";
    public static class Components
    {
        public const string CommandHandler = "CommandHandler";
        public const string QueryHandler = "QueryHandler";
        public const string Producer = "Producer";
        public const string Consumer = "Consumer";
        public const string EventHandler = "EventHandler";
    }
}