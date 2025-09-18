using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.Enums;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
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

    #region DateTime Extensions

    /// <summary>
    /// Asserts that a DateTime is close to another DateTime within a tolerance.
    /// </summary>
    public static void ShouldBeCloseTo(this DateTime actual, DateTime expected, TimeSpan tolerance, string? customMessage = null)
    {
        var difference = Math.Abs((actual - expected).TotalMilliseconds);
        difference.ShouldBeLessThanOrEqualTo(tolerance.TotalMilliseconds,
            customMessage ?? $"DateTime {actual:yyyy-MM-dd HH:mm:ss.fff} should be within {tolerance.TotalMilliseconds}ms of {expected:yyyy-MM-dd HH:mm:ss.fff}");
    }

    #endregion

    #region Domain-Specific Composite Assertions

    /// <summary>
    /// Asserts that an AxonPrincipal has valid default state after creation.
    /// </summary>
    public static AxonPrincipal ShouldBeValidPrincipal(this AxonPrincipal principal, PrincipalType expectedType, RiskTier expectedRiskTier = RiskTier.Low)
    {
        principal.ShouldNotBeNull("Principal should not be null");
        principal.Id.ShouldNotBe(default(AxonUserId), "Principal should have a valid ID");
        principal.Type.ShouldBe(expectedType, $"Principal type should be {expectedType}");
        principal.RiskTier.ShouldBe(expectedRiskTier, $"Principal risk tier should be {expectedRiskTier}");
        return principal;
    }

    /// <summary>
    /// Asserts that a principal has the expected ownership for a wallet.
    /// </summary>
    public static AxonPrincipal ShouldHaveOwnership(this AxonPrincipal principal, WalletId walletId, AccessMode? expectedMode = null, OwnershipStatus? expectedStatus = null)
    {
        var ownership = principal.WalletOwnerships.FirstOrDefault(o => o.WalletId == walletId);
        ownership.ShouldNotBeNull($"Principal should have ownership for wallet {walletId}");
        
        if (expectedMode.HasValue)
            ownership.AccessMode.ShouldBe(expectedMode.Value, $"Ownership access mode should be {expectedMode}");
            
        if (expectedStatus.HasValue)
            ownership.Status.ShouldBe(expectedStatus.Value, $"Ownership status should be {expectedStatus}");
            
        return principal;
    }

    /// <summary>
    /// Asserts that a principal does not have ownership for a wallet.
    /// </summary>
    public static AxonPrincipal ShouldNotHaveOwnership(this AxonPrincipal principal, WalletId walletId)
    {
        var ownership = principal.WalletOwnerships.FirstOrDefault(o => o.WalletId == walletId);
        ownership.ShouldBeNull($"Principal should not have ownership for wallet {walletId}");
        return principal;
    }

    /// <summary>
    /// Asserts that a principal has the expected credential.
    /// </summary>
    public static AxonPrincipal ShouldHaveCredential(this AxonPrincipal principal, string expectedProvider, string expectedSubject)
    {
        var credential = principal.Credentials.FirstOrDefault(c => c.Provider == expectedProvider && c.Subject == expectedSubject);
        credential.ShouldNotBeNull($"Principal should have credential from provider {expectedProvider} with subject {expectedSubject}");
        return principal;
    }

    /// <summary>
    /// Asserts that an aggregate root raised a specific domain event.
    /// </summary>
    public static T ShouldHaveRaisedEvent<T>(this IAggregateRoot aggregate) where T : class, IDomainEvent
    {
        var domainEvent = aggregate.DomainEvents.OfType<T>().FirstOrDefault();
        domainEvent.ShouldNotBeNull($"Aggregate should have raised event of type {typeof(T).Name}");
        return domainEvent;
    }

    /// <summary>
    /// Asserts that an aggregate root raised a specific number of domain events.
    /// </summary>
    public static IAggregateRoot ShouldHaveRaisedEventCount(this IAggregateRoot aggregate, int expectedCount)
    {
        aggregate.DomainEvents.Count.ShouldBe(expectedCount, $"Aggregate should have raised {expectedCount} domain events");
        return aggregate;
    }

    /// <summary>
    /// Asserts that an aggregate root has not raised any domain events.
    /// </summary>
    public static IAggregateRoot ShouldNotHaveRaisedAnyEvents(this IAggregateRoot aggregate)
    {
        aggregate.DomainEvents.ShouldBeEmpty("Aggregate should not have raised any domain events");
        return aggregate;
    }

    /// <summary>
    /// Asserts that a wallet has valid default state after creation.
    /// </summary>
    public static Wallet ShouldBeValidWallet(this Wallet wallet, string expectedChainId, string expectedAddressValue)
    {
        wallet.ShouldNotBeNull("Wallet should not be null");
        wallet.Id.ShouldNotBe(default(WalletId), "Wallet should have a valid ID");
        wallet.ChainId.ShouldBe(expectedChainId, $"Wallet chain ID should be {expectedChainId}");
        wallet.Address.Value.ShouldBe(expectedAddressValue, $"Wallet address should be {expectedAddressValue}");
        wallet.LastSeenAt.ShouldBe(wallet.FirstSeenAt, "LastSeenAt should initially equal FirstSeenAt");
        return wallet;
    }

    /// <summary>
    /// Asserts that a Result is successful and returns the value.
    /// </summary>
    public static T ShouldBeSuccessful<T>(this Result<T> result, string? customMessage = null)
    {
        result.IsSuccess.ShouldBeTrue(customMessage ?? "Result should be successful");
        return result.Value;
    }

    /// <summary>
    /// Asserts that a Result is a failure and returns the error.
    /// </summary>
    public static TError ShouldBeFailure<T, TError>(this Result<T, TError> result, string? customMessage = null)
    {
        result.IsFailure.ShouldBeTrue(customMessage ?? "Result should be a failure");
        return result.Error;
    }

    /// <summary>
    /// Asserts that a Result is a failure with specific error code.
    /// </summary>
    public static TError ShouldBeFailureWithCode<T, TError>(this Result<T, TError> result, string expectedCode, string? customMessage = null) where TError : class
    {
        var error = result.ShouldBeFailure(customMessage);
        
        // Assuming error has a Code property - adjust based on actual Error implementation
        var codeProperty = typeof(TError).GetProperty("Code");
        if (codeProperty != null)
        {
            var actualCode = codeProperty.GetValue(error)?.ToString();
            actualCode.ShouldBe(expectedCode, $"Error code should be {expectedCode}");
        }
        
        return error;
    }

    /// <summary>
    /// Asserts that a WalletOwnership has the expected properties.
    /// </summary>
    public static WalletOwnership ShouldBeValidOwnership(this WalletOwnership ownership, 
        AxonUserId expectedPrincipalId, WalletId expectedWalletId, 
        AccessMode expectedMode, OwnershipStatus expectedStatus)
    {
        ownership.ShouldNotBeNull("Ownership should not be null");
        ownership.PrincipalId.ShouldBe(expectedPrincipalId, "Ownership should have correct principal ID");
        ownership.WalletId.ShouldBe(expectedWalletId, "Ownership should have correct wallet ID");
        ownership.AccessMode.ShouldBe(expectedMode, "Ownership should have correct access mode");
        ownership.Status.ShouldBe(expectedStatus, "Ownership should have correct status");
        return ownership;
    }

    #endregion
}