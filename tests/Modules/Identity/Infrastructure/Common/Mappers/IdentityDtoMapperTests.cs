using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Common.Mappers;

[TestFixture]
public class IdentityDtoMapperTests
{
    [Test]
    public void ToPrincipalDto_WithValidPrincipal_ShouldMapCorrectly()
    {
        // Arrange
        var principalResult = AxonPrincipal.CreateHumanPrincipal();
        var principal = principalResult.Value;

        // Act
        var dto = IdentityDtoMapper.ToPrincipalDto(principal);

        // Assert
        dto.AxonId.ShouldBe(principal.Id.Value.ToString());
        dto.Type.ShouldBe(principal.Type.Value);
        dto.PreferredLanguage.ShouldBe(principal.Profile.PreferredLanguage.Value);
        dto.RiskTier.ShouldBe(principal.Profile.RiskTier.Value);
        dto.DefaultPerChain.ShouldNotBeNull();
        dto.ActiveCredentialCount.ShouldBe(0); // No credentials added through public API
        dto.ActiveWalletCount.ShouldBe(0);
    }

    [Test]
    public void ToPrincipalDto_WithNullPrincipal_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => IdentityDtoMapper.ToPrincipalDto(null!));
    }

    // Note: CredentialDto mapping tests would require access to internal IdentityCredential.Create
    // These would be better tested through integration tests that use the full domain flow
}