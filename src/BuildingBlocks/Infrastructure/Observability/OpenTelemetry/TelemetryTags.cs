namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry;

/// <summary>
/// Minimal tags used by Application.ObservabilityBehavior.
/// Everything else (HTTP/EF/Messaging) comes from official instrumentations.
/// </summary>
public static class TelemetryTags
{
    public static class Tracing
    {
        public static class Exception
        {
            public const string EventName   = "exception";
            public const string Type        = "exception.type";
            public const string Message     = "exception.message";
            public const string Stacktrace  = "exception.stacktrace";
        }

        public static class Otel
        {
            public const string StatusCode        = "otel.status_code";
            public const string StatusDescription = "otel.status_description";
        }

        public static class Application
        {
            // Application layer uses this for ActivitySource
            public static string AppService => $"{ObservabilityConstant.InstrumentationName}.appservice";
        }
    }

    public static class Metrics
    {
        public static class Application
        {
            // Application layer uses this for Meter
            public static string AppService => $"{ObservabilityConstant.InstrumentationName}.appservice";
        }
    }
}