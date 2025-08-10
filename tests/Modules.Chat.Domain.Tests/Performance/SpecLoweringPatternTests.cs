using System;
using System.Linq.Expressions;
using FluentAssertions;
using Xunit;
using Axon.Modules.Chat.Domain.Internal.Text;
using Axon.Modules.Chat.Domain.Specifications;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Mothers;

namespace Axon.Modules.Chat.Domain.Tests.Performance;

/// <summary>
/// Tests to ensure specification lowering follows the correct pattern:
/// - Constants are lowered with NormalizeForSearchConst (outside expression)
/// - Entity fields use .ToLower() inside expressions (EF-translatable)
/// - Empty/whitespace terms result in pass-through behavior
/// </summary>
public class SpecLoweringPatternTests
{
    [Fact]
    public void ConversationTitleContainsSpec_WithValidTerm_ShouldLowerConstantOnce()
    {
        // Arrange
        var searchTerm = "MyTitle";
        
        // Act
        var spec = new ConversationTitleContainsSpec(searchTerm);
        var expression = spec.ToExpression();
        
        // Assert
        // The expression should contain .ToLower() on the entity side and a lowered constant
        var expressionString = expression.ToString();
        
        // Verify entity side uses .ToLower() method call
        expressionString.Should().Contain(".ToLower()", 
            "expression should use .ToLower() on entity field for EF translation");
        
        // Verify the constant is already lowered (no ToLower in constant)
        expressionString.Should().Contain("mytitle", 
            "constant should be pre-lowered and appear as lowercase in expression");
        
        // Should not contain ToLowerInvariant in the expression (that's for constants only)
        expressionString.Should().NotContain("ToLowerInvariant", 
            "ToLowerInvariant should not appear in EF expression");
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConversationTitleContainsSpec_WithEmptyOrNullTerm_ShouldReturnPassThroughExpression(string? searchTerm)
    {
        // Arrange & Act
        var spec = new ConversationTitleContainsSpec(searchTerm);
        var expression = spec.ToExpression();
        
        // Assert
        // Expression should be equivalent to "c => true" (pass-through)
        var compiled = expression.Compile();
        var testConversation = CreateTestConversation("Any Title");
        
        compiled(testConversation).Should().BeTrue(
            "empty/null search term should result in pass-through (always true) expression");
        
        // Expression string should indicate pass-through behavior
        var expressionString = expression.ToString();
        expressionString.Should().Match("*True*" , 
            "pass-through expression should evaluate to True for empty/null terms");
    }
    
    [Fact]
    public void ConversationTitleContainsSpec_WithCaseMixedTerm_ShouldMatchCaseInsensitively()
    {
        // Arrange
        var spec = new ConversationTitleContainsSpec("MiXeD");
        var expression = spec.ToExpression();
        var compiled = expression.Compile();
        
        // Test conversations with various cases
        var lowerConversation = CreateTestConversation("this contains mixed case");
        var upperConversation = CreateTestConversation("THIS CONTAINS MIXED CASE");
        var mixedConversation = CreateTestConversation("This Contains MiXeD Case");
        
        // Act & Assert
        compiled(lowerConversation).Should().BeTrue("should match lowercase title");
        compiled(upperConversation).Should().BeTrue("should match uppercase title");
        compiled(mixedConversation).Should().BeTrue("should match mixed case title");
    }
    
    [Fact]
    public void ConversationTitleContainsSpec_WithTrimmedTerm_ShouldHandleWhitespace()
    {
        // Arrange - Term with leading/trailing whitespace
        var spec = new ConversationTitleContainsSpec("  search  ");
        var expression = spec.ToExpression();
        var compiled = expression.Compile();
        
        var conversation = CreateTestConversation("This contains search term");
        
        // Act & Assert
        compiled(conversation).Should().BeTrue(
            "whitespace around search term should be handled correctly");
    }
    
    [Fact]
    public void TextSlices_NormalizeForSearchConst_ShouldFollowCorrectPattern()
    {
        // Arrange & Act & Assert
        TextSlices.NormalizeForSearchConst("Mixed").Should().Be("mixed",
            "non-empty term should be lowered");
        
        TextSlices.NormalizeForSearchConst("UPPER").Should().Be("upper",
            "uppercase term should be lowered");
        
        TextSlices.NormalizeForSearchConst("  spaced  ").Should().Be("  spaced  ",
            "whitespace should be preserved but content lowered");
        
        TextSlices.NormalizeForSearchConst("").Should().Be("",
            "empty string should return empty string");
        
        TextSlices.NormalizeForSearchConst(null).Should().Be("",
            "null should return empty string");
        
        TextSlices.NormalizeForSearchConst("   ").Should().Be("",
            "whitespace-only should return empty string");
    }
    
    [Fact]
    public void PerformancePattern_CodeMarker_ShouldPreventRegressions()
    {
        // This test serves as a "code marker" to prevent performance pattern regressions
        // If this test fails, it indicates the lowering pattern has been violated
        
        // Arrange
        var spec = new ConversationTitleContainsSpec("TestTerm");
        var expression = spec.ToExpression();
        var expressionString = expression.ToString();
        
        // Assert - Document the expected pattern
        expressionString.Should().MatchRegex(@".*\.ToLower\(\)\.Contains\(.*\)",
            "PERFORMANCE PATTERN: Entity field should use .ToLower() method for EF translation");
        
        expressionString.Should().NotContain("ToLowerInvariant",
            "PERFORMANCE PATTERN: No ToLowerInvariant should appear in EF expressions");
        
        expressionString.Should().Contain("testterm",
            "PERFORMANCE PATTERN: Constants should be pre-normalized and appear lowercase");
    }
    
    private static Conversation CreateTestConversation(string title)
    {
        // Create a test conversation with the specified title
        // This is a simplified approach for testing specification expressions
        var result = Conversation.Start(
            UserMother.DefaultUser(),
            title,
            new FixedClock());
        return result.Value;
    }
}