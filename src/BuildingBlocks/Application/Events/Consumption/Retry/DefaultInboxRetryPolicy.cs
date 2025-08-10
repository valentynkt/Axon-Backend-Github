using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Events.Consumption.Retry;

/// <summary>
/// Default implementation of inbox retry policy with exponential backoff and jitter.
/// Mirrors the retry semantics from outbox processing for consistency.
/// </summary>
public sealed class DefaultInboxRetryPolicy : IInboxRetryPolicy
{
    private readonly ILogger<DefaultInboxRetryPolicy> _logger;
    private readonly Random _random;

    public DefaultInboxRetryPolicy(ILogger<DefaultInboxRetryPolicy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    public InboxRetryDecision Compute(int nextAttempt, DateTime producedAtUtc, DateTime nowUtc, InboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (nextAttempt < 1)
            throw new ArgumentOutOfRangeException(nameof(nextAttempt), "Next attempt must be 1 or greater");

        // Check if message is too old
        var messageAge = nowUtc - producedAtUtc;
        if (messageAge > options.MaxMessageAge)
        {
            _logger.LogInformation(
                "Message produced at {ProducedAt} is too old (age: {MessageAge}, max: {MaxMessageAge}). Moving to dead letter.",
                producedAtUtc, messageAge, options.MaxMessageAge);
            
            return InboxRetryDecision.DeadLetter();
        }

        // Check if we've exceeded max attempts
        if (nextAttempt > options.MaxAttempts)
        {
            _logger.LogInformation(
                "Attempt {NextAttempt} exceeds max attempts {MaxAttempts}. Moving to dead letter.",
                nextAttempt, options.MaxAttempts);
                
            return InboxRetryDecision.DeadLetter();
        }

        // Calculate exponential backoff delay
        var delay = ComputeDelay(nextAttempt - 1, options); // nextAttempt is 1-based, compute is 0-based

        _logger.LogDebug(
            "Scheduling retry attempt {NextAttempt}/{MaxAttempts} with delay {Delay}",
            nextAttempt, options.MaxAttempts, delay);

        return InboxRetryDecision.Retry(delay);
    }

    private TimeSpan ComputeDelay(int attemptNumber, InboxOptions options)
    {
        // Exponential backoff: baseDelay * 2^attempt
        var exponentialDelay = options.BaseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber);
        
        // Cap at maximum delay
        var cappedDelay = Math.Min(exponentialDelay, options.MaxRetryDelay.TotalMilliseconds);
        
        // Apply jitter if enabled
        if (options.UseJitter)
        {
            cappedDelay = ApplyJitter(cappedDelay, options.JitterRatio);
        }

        return TimeSpan.FromMilliseconds(Math.Max(0, cappedDelay));
    }

    private double ApplyJitter(double baseDelayMs, double jitterRatio)
    {
        // Apply jitter as ±jitterRatio% of the base delay
        var jitterRange = baseDelayMs * jitterRatio;
        var jitter = _random.NextDouble() * 2 * jitterRange - jitterRange; // Random value in [-jitterRange, +jitterRange]
        
        return baseDelayMs + jitter;
    }
}