using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.Enums;using BuildingBlocks.Core.Domain.Events;
using Shouldly;
using System.Diagnostics;

namespace Axon.Modules.Identity.Domain.Tests.Extensions;

/// <summary>
/// Custom Shouldly extensions for Identity Domain testing.
/// Provides fluent, domain-specific assertions that improve test readability and error messages.
/// </summary>
public static class IdentityDomainShouldlyExtensions
{
    #region Collection Extensions

    /// <summary>
    /// Asserts that a collection has the expected count.
    /// </summary>
    public static IReadOnlyCollection<T> ShouldHaveCount<T>(this IReadOnlyCollection<T> collection, int expectedCount, string? customMessage = null)
    {
        collection.Count.ShouldBe(expectedCount, 
            customMessage ?? $"Collection should have {expectedCount} items but has {collection.Count}");
        return collection;
    }

    /// <summary>
    /// Asserts that a collection is empty.
    /// </summary>
    public static IReadOnlyCollection<T> ShouldBeEmpty<T>(this IReadOnlyCollection<T> collection, string? customMessage = null)
    {
        collection.ShouldHaveCount(0, customMessage ?? "Collection should be empty");
        return collection;
    }

    #endregion

    #region String Extensions

    /// <summary>
    /// Provides fluent assertion syntax for string values.
    /// </summary>
    public static StringShould Should(this string? actual)
    {
        return new StringShould(actual);
    }

    /// <summary>
    /// Helper class to provide string-specific assertion methods.
    /// </summary>
    public class StringShould
    {
        private readonly string? _actual;

        public StringShould(string? actual)
        {
            _actual = actual;
        }

        public void Be(string? expected, string? customMessage = null)
        {
            _actual.ShouldBe(expected, customMessage);
        }

        public void NotBeNull(string? customMessage = null)
        {
            _actual.ShouldNotBeNull(customMessage ?? "String should not be null");
        }

        public void NotBeNullOrEmpty(string? customMessage = null)
        {
            _actual.ShouldNotBeNullOrEmpty(customMessage ?? "String should not be null or empty");
        }

        public void Contain(string expectedSubstring, string? customMessage = null)
        {
            _actual!.ShouldContain(expectedSubstring, customMessage: customMessage ?? $"String should contain '{expectedSubstring}'");
        }

        public void StartWith(string expectedStart, string? customMessage = null)
        {
            _actual.ShouldStartWith(expectedStart, customMessage: customMessage ?? $"String should start with '{expectedStart}'");
        }

        public void EndWith(string expectedEnd, string? customMessage = null)
        {
            _actual.ShouldEndWith(expectedEnd, customMessage: customMessage ?? $"String should end with '{expectedEnd}'");
        }
    }


    #endregion

    #region DateTimeOffset Extensions

    /// <summary>
    /// Asserts that a DateTimeOffset is after another DateTimeOffset.
    /// </summary>
    public static void ShouldBeAfter(this DateTimeOffset actual, DateTimeOffset expected, string? customMessage = null)
    {
        actual.ShouldBeGreaterThan(expected, 
            customMessage ?? $"DateTime {actual:yyyy-MM-dd HH:mm:ss.fff} should be after {expected:yyyy-MM-dd HH:mm:ss.fff}");
    }

    /// <summary>
    /// Asserts that a DateTimeOffset is before another DateTimeOffset.
    /// </summary>
    public static void ShouldBeBefore(this DateTimeOffset actual, DateTimeOffset expected, string? customMessage = null)
    {
        actual.ShouldBeLessThan(expected, 
            customMessage ?? $"DateTime {actual:yyyy-MM-dd HH:mm:ss.fff} should be before {expected:yyyy-MM-dd HH:mm:ss.fff}");
    }

    /// <summary>
    /// Provides fluent assertion syntax for DateTimeOffset values.
    /// </summary>
    public static DateTimeOffsetShould Should(this DateTimeOffset actual)
    {
        return new DateTimeOffsetShould(actual);
    }

    /// <summary>
    /// Helper class to provide DateTimeOffset-specific assertion methods.
    /// </summary>
    public class DateTimeOffsetShould
    {
        private readonly DateTimeOffset _actual;

        public DateTimeOffsetShould(DateTimeOffset actual)
        {
            _actual = actual;
        }

        public void Be(DateTimeOffset expected, string? customMessage = null)
        {
            _actual.ShouldBe(expected, customMessage);
        }

        public void BeAfter(DateTimeOffset expected, string? customMessage = null)
        {
            _actual.ShouldBeAfter(expected, customMessage);
        }

        public void BeBefore(DateTimeOffset expected, string? customMessage = null)
        {
            _actual.ShouldBeBefore(expected, customMessage);
        }

        public void BeCloseTo(DateTimeOffset expected, TimeSpan tolerance, string? customMessage = null)
        {
            var difference = Math.Abs((_actual - expected).TotalMilliseconds);
            difference.ShouldBeLessThanOrEqualTo(tolerance.TotalMilliseconds,
                customMessage ?? $"DateTime {_actual:yyyy-MM-dd HH:mm:ss.fff} should be within {tolerance.TotalMilliseconds}ms of {expected:yyyy-MM-dd HH:mm:ss.fff}");
        }
    }

    #endregion
    


    public static WalletChangedEvent Subject(this WalletChangedEvent domainEvent)
    {
        return domainEvent;
    }

    #region Event-Specific Assertion Extensions

    /// <summary>
    /// Asserts that a domain event occurred at or after a specific time.
    /// </summary>
    public static T ShouldHaveOccurredAfter<T>(this T domainEvent, DateTime expectedTime, string? customMessage = null) where T : IDomainEvent
    {
        domainEvent.OccurredAt.ShouldBeGreaterThanOrEqualTo(expectedTime, 
            customMessage ?? $"Domain event should have occurred at or after {expectedTime:yyyy-MM-dd HH:mm:ss.fff}");
        return domainEvent;
    }

    /// <summary>
    /// Asserts that a domain event occurred within a time window.
    /// </summary>
    public static T ShouldHaveOccurredWithin<T>(this T domainEvent, TimeSpan timeWindow, string? customMessage = null) where T : IDomainEvent
    {
        var now = DateTime.UtcNow;
        var earliestTime = now - timeWindow;
        
        domainEvent.OccurredAt.ShouldBeGreaterThanOrEqualTo(earliestTime, 
            customMessage ?? $"Domain event should have occurred within the last {timeWindow.TotalSeconds} seconds");
        domainEvent.OccurredAt.ShouldBeLessThanOrEqualTo(now.AddSeconds(1), 
            customMessage ?? "Domain event should not have occurred in the future");
        
        return domainEvent;
    }

    #endregion
}