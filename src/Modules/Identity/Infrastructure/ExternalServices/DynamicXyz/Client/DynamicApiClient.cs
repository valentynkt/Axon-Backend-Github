using System.Net;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices.DynamicXyz.Client;

public class DynamicApiClient : IDynamicApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DynamicApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public DynamicApiClient(HttpClient httpClient, ILogger<DynamicApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = JsonSerializerOptions.Web;
    }

    public async Task<Result<T, Error>> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Making GET request to {Endpoint}", endpoint);
            
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            return await ProcessResponse<T>(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for GET {Endpoint}", endpoint);
            return Result.Failure<T, Error>(MapHttpException(ex));
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger.LogWarning("GET request to {Endpoint} was cancelled", endpoint);
            return Result.Failure<T, Error>(Error.Cancelled("Request was cancelled"));
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "GET request to {Endpoint} timed out", endpoint);
            return Result.Failure<T, Error>(Error.Timeout("Request timeout"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GET request to {Endpoint}", endpoint);
            return Result.Failure<T, Error>(Error.External($"Unexpected error: {ex.Message}", "DYNAMIC_API_ERROR", ex));
        }
    }

    public async Task<Result<T, Error>> PostAsync<T>(string endpoint, object? content = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Making POST request to {Endpoint}", endpoint);

            using HttpContent? httpContent = content is not null
                ? new StringContent(JsonSerializer.Serialize(content, _jsonOptions), Encoding.UTF8, "application/json")
                : null;

            var response = await _httpClient.PostAsync(endpoint, httpContent, cancellationToken);
            return await ProcessResponse<T>(response);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON serialization failed for POST {Endpoint}", endpoint);
            return Result.Failure<T, Error>(Error.Serialization($"Failed to serialize request: {ex.Message}"));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for POST {Endpoint}", endpoint);
            return Result.Failure<T, Error>(MapHttpException(ex));
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger.LogWarning("POST request to {Endpoint} was cancelled", endpoint);
            return Result.Failure<T, Error>(Error.Cancelled("Request was cancelled"));
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "POST request to {Endpoint} timed out", endpoint);
            return Result.Failure<T, Error>(Error.Timeout("Request timeout"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during POST request to {Endpoint}", endpoint);
            return Result.Failure<T, Error>(Error.External($"Unexpected error: {ex.Message}", "DYNAMIC_API_ERROR", ex));
        }
    }

    private async Task<Result<T, Error>> ProcessResponse<T>(HttpResponseMessage response)
    {
        try
        {
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    if (typeof(T) == typeof(string))
                    {
                        return Result.Success<T, Error>((T)(object)string.Empty);
                    }
                    return Result.Failure<T, Error>(Error.External("Empty response received", "EMPTY_RESPONSE"));
                }

                var result = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
                if (result is null)
                {
                    return Result.Failure<T, Error>(Error.Serialization("Failed to deserialize response", "DESERIALIZATION_ERROR"));
                }

                return Result.Success<T, Error>(result);
            }

            // Handle error response
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("API returned error {StatusCode}: {Content}", response.StatusCode, errorContent);
            
            return Result.Failure<T, Error>(MapHttpStatusToError(response.StatusCode, errorContent));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize response");
            return Result.Failure<T, Error>(Error.Serialization($"Failed to deserialize response: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing response");
            return Result.Failure<T, Error>(Error.External($"Error processing response: {ex.Message}", "RESPONSE_PROCESSING_ERROR", ex));
        }
    }

    private static Error MapHttpStatusToError(HttpStatusCode statusCode, string content)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => Error.Unauthorized("Invalid API token"),
            HttpStatusCode.Forbidden => Error.Forbidden("Access denied"),
            HttpStatusCode.NotFound => Error.NotFound("Resource not found"),
            HttpStatusCode.BadRequest => Error.Validation($"Bad request: {content}"),
            HttpStatusCode.TooManyRequests => Error.RateLimit("Rate limit exceeded"),
            HttpStatusCode.RequestTimeout => Error.Timeout("Request timeout"),
            HttpStatusCode.ServiceUnavailable => Error.Unavailable("Service unavailable"),
            HttpStatusCode.BadGateway => Error.External("Bad gateway", "BAD_GATEWAY"),
            HttpStatusCode.GatewayTimeout => Error.Timeout("Gateway timeout"),
            _ => Error.External($"API error: {statusCode}", "DYNAMIC_API_ERROR")
        };
    }

    private static Error MapHttpException(HttpRequestException ex)
    {
        if (ex.StatusCode.HasValue)
        {
            return MapHttpStatusToError(ex.StatusCode.Value, ex.Message);
        }

        return Error.Network($"Network error: {ex.Message}", "NETWORK_ERROR", ex);
    }
}