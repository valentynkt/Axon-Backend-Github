namespace BuildingBlocks.Core.Diagnostics.Performance;

public interface IErrorTelemetry
{
    void Created(string code, string type, string severity);
    void Resolved(string code);
    void HandleDuration(TimeSpan duration, string code, string? stage = null);
}