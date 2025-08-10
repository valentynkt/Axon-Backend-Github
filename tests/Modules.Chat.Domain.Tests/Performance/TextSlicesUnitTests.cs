using System;
using FluentAssertions;
using Xunit;
using Axon.Modules.Chat.Domain.Internal.Text;

namespace Axon.Modules.Chat.Domain.Tests.Performance;

/// <summary>
/// Direct unit tests for TextSlices utility to verify basic functionality.
/// </summary>
public class TextSlicesUnitTests
{
    [Fact]
    public void Preview_WithNullSource_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => TextSlices.Preview(null!, 100);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }
    
    [Fact]
    public void Preview_WithShortContent_ShouldReturnSameReference()
    {
        // Arrange
        var content = "Hello World";
        
        // Act
        var preview = TextSlices.Preview(content, 100);
        
        // Assert
        ReferenceEquals(content, preview).Should().BeTrue();
        preview.Should().Be("Hello World");
    }
    
    [Fact]
    public void Preview_WithLongContent_ShouldReturnSubstring()
    {
        // Arrange
        var content = new string('x', 150);
        
        // Act
        var preview = TextSlices.Preview(content, 100);
        
        // Assert
        ReferenceEquals(content, preview).Should().BeFalse();
        preview.Length.Should().Be(100);
        preview.Should().Be(new string('x', 100));
    }
    
    [Fact]
    public void Preview_WithExactLength_ShouldReturnSameReference()
    {
        // Arrange
        var content = new string('y', 100);
        
        // Act
        var preview = TextSlices.Preview(content, 100);
        
        // Assert
        ReferenceEquals(content, preview).Should().BeTrue();
        preview.Length.Should().Be(100);
    }
    
    [Theory]
    [InlineData("Test", "test")]
    [InlineData("UPPER", "upper")]
    [InlineData("MiXeD", "mixed")]
    [InlineData("", "")]
    public void NormalizeForSearchConst_WithValidTerms_ShouldReturnLowerCase(string input, string expected)
    {
        // Act
        var result = TextSlices.NormalizeForSearchConst(input);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void NormalizeForSearchConst_WithEmptyOrWhitespace_ShouldReturnEmptyString(string? input)
    {
        // Act
        var result = TextSlices.NormalizeForSearchConst(input);
        
        // Assert
        result.Should().Be(string.Empty);
    }
}