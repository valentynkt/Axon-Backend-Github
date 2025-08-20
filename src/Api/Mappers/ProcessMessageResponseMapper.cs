using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Web.Mappers;
using Axon.Api.Contracts.Chat;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Mappers;

/// <summary>
/// Maps ProcessMessageResponse results (mostly pass-through).
/// </summary>
public sealed class ProcessMessageResponseMapper : BaseMapper, IResponseMapper<ProcessMessageResponse, ProcessMessageResponse>
{
    public ProcessMessageResponseMapper(ILogger<ProcessMessageResponseMapper> logger) : base(logger)
    {
    }

    public Task<Result<ProcessMessageResponse>> MapAsync(ProcessMessageResponse domainResult, CancellationToken cancellationToken = default)
    {
        // In this case, the domain result is already the correct response type
        // This mapper exists for consistency and future extensibility
        Logger.LogDebug("Mapping ProcessMessageResponse (pass-through)");
        
        return Task.FromResult(Result<ProcessMessageResponse>.Success(domainResult));
    }
}