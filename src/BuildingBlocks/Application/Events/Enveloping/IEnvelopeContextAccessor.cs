namespace BuildingBlocks.Application.Events.Enveloping;

public interface IEnvelopeContextAccessor
{
    IntegrationEnvelopeContext? Current { get; }
    IDisposable Push(IntegrationEnvelopeContext context);
}