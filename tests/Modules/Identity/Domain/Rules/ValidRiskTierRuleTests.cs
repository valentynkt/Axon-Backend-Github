using Axon.Modules.Identity.Domain.Tests.TestData;

namespace Axon.Modules.Identity.Domain.Tests.Rules;

[TestFixture]
public class ValidRiskTierRuleTests
{
    [Test]
    public void ServicePrincipal_WithLowRisk_ShouldBeValid()
    {
        var rule = new ValidRiskTierRule(RiskTier.Low, PrincipalType.Service);
        rule.IsBroken().ShouldBeFalse();
    }
    
    [Test]
    public void ServicePrincipal_WithMediumRisk_ShouldBeInvalid()
    {
        var rule = new ValidRiskTierRule(RiskTier.Medium, PrincipalType.Service);
        rule.IsBroken().ShouldBeTrue();
    }
    
    [Test]
    public void ServicePrincipal_WithHighRisk_ShouldBeInvalid()
    {
        var rule = new ValidRiskTierRule(RiskTier.High, PrincipalType.Service);
        rule.IsBroken().ShouldBeTrue();
    }
    
    [Test]
    public void HumanPrincipal_WithLowRisk_ShouldBeValid()
    {
        var rule = new ValidRiskTierRule(RiskTier.Low, PrincipalType.Human);
        rule.IsBroken().ShouldBeFalse();
    }
    
    [Test]
    public void HumanPrincipal_WithMediumRisk_ShouldBeValid()
    {
        var rule = new ValidRiskTierRule(RiskTier.Medium, PrincipalType.Human);
        rule.IsBroken().ShouldBeFalse();
    }
    
    [Test]
    public void HumanPrincipal_WithHighRisk_ShouldBeValid()
    {
        var rule = new ValidRiskTierRule(RiskTier.High, PrincipalType.Human);
        rule.IsBroken().ShouldBeFalse();
    }
    
    [Test]
    public void Rule_HasCorrectErrorMessage()
    {
        var rule = new ValidRiskTierRule(RiskTier.High, PrincipalType.Service);
        
        rule.Message.ShouldNotBeNullOrWhiteSpace();
        rule.Code.ShouldBe(IdentityDomainErrors.Profile.RiskTierInvalidForPrincipal().Code);
    }
    
    [Test]
    public async Task IsBrokenAsync_ReturnsSameResultAsIsBroken()
    {
        var rule = new ValidRiskTierRule(RiskTier.Medium, PrincipalType.Service);
        
        var syncResult = rule.IsBroken();
        var asyncResult = await rule.IsBrokenAsync();
        
        asyncResult.ShouldBe(syncResult);
        asyncResult.ShouldBeTrue();
    }
}