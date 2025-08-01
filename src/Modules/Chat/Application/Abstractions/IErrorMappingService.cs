using Axon.Shared.Common;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Service for mapping infrastructure errors to domain errors while maintaining clean architecture boundaries
/// </summary>
public interface IErrorMappingService
{
    /// <summary>
    /// Maps an exception to an appropriate domain error
    /// </summary>
    Error MapProcessingException(Exception exception);
    
    /// <summary>
    /// Maps HTTP request exceptions to domain errors
    /// </summary>
    Error MapHttpRequestException(Exception exception);
    
    /// <summary>
    /// Maps timeout exceptions to domain errors
    /// </summary>
    Error MapTimeoutException(Exception exception);
    
    /// <summary>
    /// Maps unauthorized access exceptions to domain errors
    /// </summary>
    Error MapUnauthorizedAccessException(Exception exception);
    
    /// <summary>
    /// Maps JSON serialization exceptions to domain errors
    /// </summary>
    Error MapJsonException(Exception exception);
    
    /// <summary>
    /// Maps generic exceptions to domain errors
    /// </summary>
    Error MapGenericException(Exception exception);
}