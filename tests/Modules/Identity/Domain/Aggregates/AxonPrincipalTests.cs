using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Domain.Tests.TestData;
using BuildingBlocks.Primitives.Ids;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Aggregates;

[TestFixture]
public class AxonPrincipalTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [TestFixture] 
    public class CreateHumanPrincipal : AxonPrincipalTests
    {
        [Test]
        public void WithValidData_ShouldSucceed()
        {
            // Arrange
            var emailHash = EmailHash.From("test@example.com");

            // Act
            var result = AxonPrincipal.CreateHumanPrincipal(emailHash, _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var principal = result.Value;
            principal.Type.ShouldBe(PrincipalType.Human);
            principal.PrimaryEmailHash.ShouldBe(emailHash);
            principal.IsHuman.ShouldBeTrue();
            principal.IsService.ShouldBeFalse();
            principal.Profile.ShouldNotBeNull();
            principal.Credentials.ShouldBeEmpty();
            principal.WalletOwnerships.ShouldBeEmpty();
        }

        [Test]
        public void WithoutEmailHash_ShouldSucceed()
        {
            // Act
            var result = AxonPrincipal.CreateHumanPrincipal(timeProvider: _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var principal = result.Value;
            principal.Type.ShouldBe(PrincipalType.Human);
            principal.PrimaryEmailHash.ShouldBeNull();
            principal.IsHuman.ShouldBeTrue();
        }

        [Test]
        public void ShouldRaisePrincipalCreatedEvent()
        {
            // Arrange
            var emailHash = EmailHash.From("test@example.com");

            // Act
            var result = AxonPrincipal.CreateHumanPrincipal(emailHash, _timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var principal = result.Value;
            var events = principal.DomainEvents;
            events.ShouldHaveCount(1);
            var createdEvent = events.First().ShouldBeOfType<PrincipalCreatedEvent>().Subject;
            createdEvent.PrincipalId.ShouldBe(principal.Id);
            createdEvent.PrincipalType.ShouldBe(PrincipalType.Human.Value);
            createdEvent.PrimaryEmailHash.ShouldBe(emailHash.Value);
        }
    }

    [TestFixture]
    public class CreateServicePrincipal : AxonPrincipalTests
    {
        [Test]
        public void WithValidData_ShouldSucceed()
        {
            // Act
            var result = AxonPrincipal.CreateServicePrincipal(_timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var principal = result.Value;
            principal.Type.ShouldBe(PrincipalType.Service);
            principal.PrimaryEmailHash.ShouldBeNull();
            principal.IsHuman.ShouldBeFalse();
            principal.IsService.ShouldBeTrue();
            principal.Profile.ShouldNotBeNull();
        }

        [Test]
        public void ShouldRaisePrincipalCreatedEvent()
        {
            // Act
            var result = AxonPrincipal.CreateServicePrincipal(_timeProvider);

            // Should
            result.IsSuccess.ShouldBeTrue();
            var principal = result.Value;
            var events = principal.DomainEvents;
            events.ShouldHaveCount(1);
            var createdEvent = events.First().ShouldBeOfType<PrincipalCreatedEvent>().Subject;
            createdEvent.PrincipalId.ShouldBe(principal.Id);
            createdEvent.PrincipalType.ShouldBe(PrincipalType.Service.Value);
            createdEvent.PrimaryEmailHash.ShouldBeNull();
        }
    }

    [TestFixture]
    public class Properties : AxonPrincipalTests
    {
        [Test]
        public void HumanPrincipal_ShouldHaveCorrectProperties()
        {
            // Arrange
            var result = AxonPrincipal.CreateHumanPrincipal(timeProvider: _timeProvider);
            var principal = result.Value;

            // Should
            principal.IsHuman.ShouldBeTrue();
            principal.IsService.ShouldBeFalse();
            principal.Type.IsHuman.ShouldBeTrue();
            principal.Type.IsService.ShouldBeFalse();
        }

        [Test]
        public void ServicePrincipal_ShouldHaveCorrectProperties()
        {
            // Arrange
            var result = AxonPrincipal.CreateServicePrincipal(_timeProvider);
            var principal = result.Value;

            // Should
            principal.IsHuman.ShouldBeFalse();
            principal.IsService.ShouldBeTrue();
            principal.Type.IsHuman.ShouldBeFalse();
            principal.Type.IsService.ShouldBeTrue();
        }

        [Test]
        public void NewPrincipal_ShouldHaveEmptyCollections()
        {
            // Arrange
            var result = AxonPrincipal.CreateHumanPrincipal(timeProvider: _timeProvider);
            var principal = result.Value;

            // Should
            principal.Credentials.ShouldBeEmpty();
            principal.WalletOwnerships.ShouldBeEmpty();
            principal.ChainDefaults.ShouldBeEmpty();
        }

        [Test]
        public void NewPrincipal_ShouldBeActive()
        {
            // Arrange
            var result = AxonPrincipal.CreateHumanPrincipal(timeProvider: _timeProvider);
            var principal = result.Value;

            // Should
            principal.IsDeleted.ShouldBeFalse();
            principal.CreatedAt.ShouldBeAfter(DateTimeOffset.UtcNow.AddSeconds(-10));
        }
    }
}