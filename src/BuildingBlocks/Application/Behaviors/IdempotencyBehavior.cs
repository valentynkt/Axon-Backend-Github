using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Abstractions.Caching;
using BuildingBlocks.Core.Abstractions.Idempotency;
using BuildingBlocks.Core.Functional.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Pipeline behavior that provides idempotency support for commands.
/// Only processes commands that implement IIdempotentCommand interface.
/// Handles caching of command results and supports intermediate state caching for long-running operations.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed.</typeparam>
/// <typeparam name="TResponse">The type of response returned.</typeparam>
public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : class, IResult
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

    public IdempotencyBehavior(
        IServiceProvider serviceProvider,
        ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only handle idempotent commands
        if (request is not IIdempotentCommand idempotentCommand)
        {
            return await next();
        }

        var stopwatch = Stopwatch.StartNew();
        var requestType = typeof(TRequest).Name;
        
        try
        {
            // Get required services
            var idempotencyCache = GetRequiredService<IIdempotencyCache>();
            var currentUser = GetRequiredService<ICurrentUserService>();

            // Generate or use explicit idempotency key
            var idempotencyKey = await GenerateIdempotencyKeyAsync(idempotentCommand, currentUser);
            
            _logger.LogDebug("Processing idempotent command {CommandType} with key {IdempotencyKey}", 
                requestType, idempotencyKey);

            // Check for cached final result
            var cachedResult = await idempotencyCache.GetAsync<TResponse>(idempotencyKey, cancellationToken);
            if (cachedResult is not null)
            {
                _logger.LogInformation("Idempotency cache hit for {CommandType} with key {IdempotencyKey}", 
                    requestType, idempotencyKey);
                
                EmitMetrics(requestType, "cache_hit", stopwatch.ElapsedMilliseconds);
                return cachedResult;
            }

            // Execute the command
            _logger.LogDebug("Executing idempotent command {CommandType} with key {IdempotencyKey}", 
                requestType, idempotencyKey);
                
            var result = await next();
            
            // Cache successful results only
            if (result.IsSuccess)
            {
                var ttl = idempotentCommand.GetIdempotencyWindow();
                await idempotencyCache.SetAsync(idempotencyKey, result, ttl, cancellationToken);
                
                _logger.LogInformation("Cached successful result for {CommandType} with key {IdempotencyKey} (TTL: {TTL})", 
                    requestType, idempotencyKey, ttl);
                
                EmitMetrics(requestType, "executed_and_cached", stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("Not caching failed result for {CommandType} with key {IdempotencyKey}: {Error}", 
                    requestType, idempotencyKey, result.Error?.Message);
                
                EmitMetrics(requestType, "executed_failed", stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in idempotency behavior for {CommandType}", requestType);
            EmitMetrics(requestType, "error", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task<string> GenerateIdempotencyKeyAsync(
        IIdempotentCommand command, 
        ICurrentUserService currentUser)
    {
        // Use explicit key if provided
        var explicitKey = command.GetExplicitIdempotencyKey();
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return $"{IdempotencyDefaults.KeyPrefix}:explicit:{explicitKey}";
        }

        // Try to get a registered key provider for this command type
        var keyProviderType = typeof(IIdempotencyKeyProvider<>).MakeGenericType(typeof(TRequest));
        var keyProvider = _serviceProvider.GetService(keyProviderType);
        
        if (keyProvider is not null)
        {
            // Use reflection to call GenerateKey method
            var generateMethod = keyProviderType.GetMethod("GenerateKey");
            if (generateMethod is not null)
            {
                var generatedKey = generateMethod.Invoke(keyProvider, [command, currentUser]) as string;
                if (!string.IsNullOrWhiteSpace(generatedKey))
                {
                    return $"{IdempotencyDefaults.KeyPrefix}:generated:{generatedKey}";
                }
            }
        }

        // Fallback: serialize command and hash it
        try
        {
            var json = JsonSerializer.Serialize(command, JsonSerializerOptions.Default);
            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = SHA256.HashData(bytes);
            var hashString = Convert.ToHexString(hash).ToLowerInvariant();
            var fallbackKey = $"{typeof(TRequest).FullName}:{hashString}";
            
            _logger.LogWarning("Using fallback idempotency key for {CommandType}. Consider registering an IIdempotencyKeyProvider<{CommandType}>", 
                typeof(TRequest).Name, typeof(TRequest).Name);
            
            return $"{IdempotencyDefaults.KeyPrefix}:fallback:{fallbackKey}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate fallback idempotency key for {CommandType}", typeof(TRequest).Name);
            throw new InvalidOperationException($"Cannot generate idempotency key for {typeof(TRequest).Name}", ex);
        }
    }

    private T GetRequiredService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    private static void EmitMetrics(string commandType, string outcome, long elapsedMs)
    {
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag("axon.idempotency.command_type", commandType);
            activity.SetTag("axon.idempotency.outcome", outcome);
            activity.SetTag("axon.idempotency.duration_ms", elapsedMs);
        }
    }
}