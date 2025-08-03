using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
public sealed class McpServerUrlTests
{
    [Test]
    [TestCase("https://api.example.com")]
    [TestCase("https://localhost:8080")]
    [TestCase("https://subdomain.example.com/path")]
    public void Create_GivenValidUrl_ShouldReturnSuccess(string validUrl)
    {
        // Act
        var result = McpServerUrl.Create(validUrl);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldNotBeNull();
    }

    [Test]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("invalid-url")]
    [TestCase("ftp://invalid.com")]
    public void Create_GivenInvalidUrl_ShouldReturnFailure(string invalidUrl)
    {
        // Act
        var result = McpServerUrl.Create(invalidUrl);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
    }

    [Test]
    public void ToString_ShouldReturnUrlString()
    {
        // Arrange
        var url = "https://api.example.com";
        var mcpUrl = McpServerUrl.Create(url).Value;

        // Act
        var result = mcpUrl.ToString();

        // Assert
        result.ShouldStartWith("https://api.example.com");
    }

    [Test]
    public void Equals_WithSameValue_ShouldReturnTrue()
    {
        // Arrange
        var url = "https://api.example.com";
        var mcpUrl1 = McpServerUrl.Create(url).Value;
        var mcpUrl2 = McpServerUrl.Create(url).Value;

        // Act & Assert
        mcpUrl1.Equals(mcpUrl2).ShouldBeTrue();
        (mcpUrl1 == mcpUrl2).ShouldBeTrue();
        (mcpUrl1 != mcpUrl2).ShouldBeFalse();
    }

    [Test]
    public void GetHashCode_WithSameValue_ShouldReturnSameHashCode()
    {
        // Arrange
        var url = "https://api.example.com";
        var mcpUrl1 = McpServerUrl.Create(url).Value;
        var mcpUrl2 = McpServerUrl.Create(url).Value;

        // Act & Assert
        mcpUrl1.GetHashCode().ShouldBe(mcpUrl2.GetHashCode());
    }
}