using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices.DynamicXyz.Client;

public interface IDynamicApiClient
{
    Task<Result<T, Error>> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default);
    
    Task<Result<T, Error>> PostAsync<T>(string endpoint, object? content = null, CancellationToken cancellationToken = default);
}