namespace Axon.Modules.Identity.Domain.Tests.Rules;

[TestFixture]
public class PrincipalMustHaveTypeRuleTests
{
    [Test]
    public void HumanType_ShouldNotBreakRule()
    {
        // Arrange
        var rule = new PrincipalMustHaveTypeRule(PrincipalType.Human);
        
        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }
    
    [Test]
    public void ServiceType_ShouldNotBreakRule()
    {
        // Arrange
        var rule = new PrincipalMustHaveTypeRule(PrincipalType.Service);
        
        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }
    
    [Test]
    public void Rule_HasCorrectErrorDetails()
    {
        // Arrange
        var rule = new PrincipalMustHaveTypeRule(PrincipalType.Human);
        
        // Assert
        rule.Message.ShouldBe("Principal must have a valid type assigned.");
        rule.Code.ShouldBe("IDENTITY.PRINCIPAL.TYPE.REQUIRED");
    }
    
    [Test]
    public async Task IsBrokenAsync_ReturnsSameResultAsIsBroken()
    {
        // Arrange
        var rule = new PrincipalMustHaveTypeRule(PrincipalType.Human);
        
        // Act
        var syncResult = rule.IsBroken();
        var asyncResult = await rule.IsBrokenAsync();
        
        // Assert
        asyncResult.ShouldBe(syncResult);
        asyncResult.ShouldBeFalse();
    }
}