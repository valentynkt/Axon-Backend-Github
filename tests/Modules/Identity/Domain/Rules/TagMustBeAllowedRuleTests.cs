namespace Axon.Modules.Identity.Domain.Tests.Rules;

[TestFixture]
public class TagMustBeAllowedRuleTests
{
    [TestCase("personal", false, Description = "Allowed tag 'personal' should not break rule")]
    [TestCase("business", false, Description = "Allowed tag 'business' should not break rule")]
    [TestCase("trading", false, Description = "Allowed tag 'trading' should not break rule")]
    [TestCase("defi", false, Description = "Allowed tag 'defi' should not break rule")]
    [TestCase("gaming", false, Description = "Allowed tag 'gaming' should not break rule")]
    [TestCase("nft", false, Description = "Allowed tag 'nft' should not break rule")]
    [TestCase("dao", false, Description = "Allowed tag 'dao' should not break rule")]
    [TestCase("test", false, Description = "Allowed tag 'test' should not break rule")]
    [TestCase("main", false, Description = "Allowed tag 'main' should not break rule")]
    [TestCase("hot", false, Description = "Allowed tag 'hot' should not break rule")]
    [TestCase("cold", false, Description = "Allowed tag 'cold' should not break rule")]
    public void AllowedTags_ShouldNotBreakRule(string tagValue, bool shouldBreak)
    {
        // Arrange
        var rule = new TagMustBeAllowedRule(tagValue);
        
        // Act & Assert
        rule.IsBroken().ShouldBe(shouldBreak);
    }
    
    [TestCase("invalid-tag", true, Description = "Non-allowed tag should break rule")]
    [TestCase("random", true, Description = "Random tag should break rule")]
    [TestCase("", true, Description = "Empty tag should break rule")]
    [TestCase(" ", true, Description = "Whitespace tag should break rule")]
    [TestCase("a", true, Description = "Too short tag should break rule")]
    [TestCase("verylongtagthatexceedsfiftycharacterslimitandshouldfail123456", true, Description = "Too long tag should break rule")]
    [TestCase("invalid@tag", true, Description = "Tag with invalid characters should break rule")]
    public void InvalidTags_ShouldBreakRule(string tagValue, bool shouldBreak)
    {
        // Arrange
        var rule = new TagMustBeAllowedRule(tagValue);
        
        // Act & Assert
        rule.IsBroken().ShouldBe(shouldBreak);
    }
    
    [Test]
    public void CaseInsensitive_ShouldNotBreakRule()
    {
        // Test that tags are case-insensitive
        var uppercaseRule = new TagMustBeAllowedRule("PERSONAL");
        var mixedCaseRule = new TagMustBeAllowedRule("PeRsOnAl");
        
        uppercaseRule.IsBroken().ShouldBeFalse();
        mixedCaseRule.IsBroken().ShouldBeFalse();
    }
    
    [Test]
    public void Rule_HasCorrectErrorDetails()
    {
        // Arrange
        var rule = new TagMustBeAllowedRule("invalid-tag");
        
        // Assert
        rule.Message.ShouldNotBeNullOrWhiteSpace();
        rule.Code.ShouldNotBeNullOrWhiteSpace();
        // The error details come from WalletDomainErrors.Tag.NotAllowed()
    }
}