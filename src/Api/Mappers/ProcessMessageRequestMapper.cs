using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Web.Mappers;
using Axon.Api.Contracts.Chat;
using Axon.Modules.Chat.Application.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Mappers;

/// <summary>
/// Maps ProcessMessageRequest to IChatCommandDispatcher calls.
/// This is a special case where we map directly to a service call rather than a command.
/// </summary>
public sealed class ProcessMessageRequestMapper : BaseMapper, IRequestMapper<ProcessMessageRequest, ProcessMessageResponse>
{
    private readonly IChatCommandDispatcher _dispatcher;

    public ProcessMessageRequestMapper(
        IChatCommandDispatcher dispatcher,
        ILogger<ProcessMessageRequestMapper> logger) : base(logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<Result<ProcessMessageResponse>> MapAsync(ProcessMessageRequest request, CancellationToken cancellationToken = default)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return RequiredValueMissing<ProcessMessageResponse>("Message");
        }

        Logger.LogDebug("Mapping ProcessMessageRequest to dispatcher call");

        // Delegate to the dispatcher which handles the command logic
        return await _dispatcher.ProcessMessageAsync(request, cancellationToken);
    }
}