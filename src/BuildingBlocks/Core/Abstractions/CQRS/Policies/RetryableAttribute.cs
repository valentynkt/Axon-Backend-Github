// /BuildingBlocks/Core/Abstractions/CQRS/Policies/RetryableAttribute.cs
#nullable enable
using System;
using Polly; // uses DelayBackoffType, etc.

namespace BuildingBlocks.Core.Abstractions.CQRS.Policies;

/// <summary>
/// Declarative, Polly-native retry hints for idempotent queries.
/// Application behavior translates these directly into Polly RetryStrategyOptions.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class RetryableAttribute : Attribute
{
    /// <summary>Enable/disable retries (default: true).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Maximum retry attempts (default: 3).</summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>Initial delay (default: 100ms).</summary>
    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Maximum backoff delay (default: 30s).</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Backoff shape (default: Exponential).</summary>
    public DelayBackoffType BackoffType { get; init; } = DelayBackoffType.Exponential;

    /// <summary>Apply jitter (default: true).</summary>
    public bool UseJitter { get; init; } = true;
}