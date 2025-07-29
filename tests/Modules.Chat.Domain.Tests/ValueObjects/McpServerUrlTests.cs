using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using FluentAssertions;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

public sealed class McpServerUrlTests
{
    [Theory]
    [InlineData("https://api.example.com", "https://api.example.com/")]
    [InlineData("https://localhost:8080", "https://localhost:8080/")]
    [InlineData("https://subdomain.example.com/path", "https://subdomain.example.com/path")]
    [InlineData("https://api.example.com:443/v1/mcp", "https://api.example.com/v1/mcp")]
    public void Create_ShouldReturnSuccessResult_GivenValidHttpsUrl(string validUrl, string expectedNormalizedUrl)
    {
        // Act
        var result = McpServerUrl.Create(validUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.ToString().Should().Be(expectedNormalizedUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldReturnValidationError_GivenNullOrWhitespaceString(string? input)
    {
        // Act
        var result = McpServerUrl.Create(input);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("MCP server URL cannot be null or empty");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("://invalid-url")]
    [InlineData("relative/path")]
    public void Create_ShouldReturnValidationError_GivenInvalidUrl(string invalidUrl)
    {
        // Act
        var result = McpServerUrl.Create(invalidUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("MCP server URL must be a valid absolute URL");
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("ws://example.com")]
    [InlineData("file://example.com")]
    [InlineData("file://local/path")]
    public void Create_ShouldReturnValidationError_GivenNonHttpsScheme(string nonHttpsUrl)
    {
        // Act
        var result = McpServerUrl.Create(nonHttpsUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("MCP server URL must use HTTPS scheme for security");
    }

    [Fact]
    public void Create_ShouldReturnSuccessResult_GivenValidHttpsUri()
    {
        // Arrange
        var validUri = new Uri("https://api.example.com/mcp");

        // Act
        var result = McpServerUrl.Create(validUri);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(validUri);
    }

    [Fact]
    public void Create_ShouldReturnValidationError_GivenNullUri()
    {
        // Act
        var result = McpServerUrl.Create((Uri?)null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("MCP server URI cannot be null");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_GivenRelativeUri()
    {
        // Arrange
        var relativeUri = new Uri("/relative/path", UriKind.Relative);

        // Act
        var result = McpServerUrl.Create(relativeUri);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("MCP server URI must be absolute");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_GivenNonHttpsUriScheme()
    {
        // Arrange
        var httpUri = new Uri("http://example.com");

        // Act
        var result = McpServerUrl.Create(httpUri);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("MCP server URI must use HTTPS scheme for security");
    }

    [Fact]
    public void ToString_ShouldReturnUriString_GivenValidMcpServerUrl()
    {
        // Arrange
        var urlString = "https://api.example.com/mcp";
        var mcpServerUrl = McpServerUrl.Create(urlString).Value;

        // Act
        var result = mcpServerUrl.ToString();

        // Assert
        result.Should().Be(urlString);
    }

    [Fact]
    public void ImplicitOperator_ShouldConvertToUri_GivenValidMcpServerUrl()
    {
        // Arrange
        var originalUri = new Uri("https://api.example.com/mcp");
        var mcpServerUrl = McpServerUrl.Create(originalUri).Value;

        // Act
        Uri convertedUri = mcpServerUrl;

        // Assert
        convertedUri.Should().Be(originalUri);
    }

    [Fact]
    public void ImplicitOperator_ShouldConvertToString_GivenValidMcpServerUrl()
    {
        // Arrange
        var urlString = "https://api.example.com/mcp";
        var mcpServerUrl = McpServerUrl.Create(urlString).Value;

        // Act
        string convertedString = mcpServerUrl;

        // Assert
        convertedString.Should().Be(urlString);
    }

    [Fact]
    public void Equality_ShouldReturnTrue_GivenSameUriValues()
    {
        // Arrange
        var urlString = "https://api.example.com/mcp";
        var mcpServerUrl1 = McpServerUrl.Create(urlString).Value;
        var mcpServerUrl2 = McpServerUrl.Create(urlString).Value;

        // Act & Assert
        mcpServerUrl1.Should().Be(mcpServerUrl2);
        mcpServerUrl1.Equals(mcpServerUrl2).Should().BeTrue();
        (mcpServerUrl1 == mcpServerUrl2).Should().BeTrue();
        (mcpServerUrl1 != mcpServerUrl2).Should().BeFalse();
    }

    [Fact]
    public void Equality_ShouldReturnFalse_GivenDifferentUriValues()
    {
        // Arrange
        var mcpServerUrl1 = McpServerUrl.Create("https://api1.example.com").Value;
        var mcpServerUrl2 = McpServerUrl.Create("https://api2.example.com").Value;

        // Act & Assert
        mcpServerUrl1.Should().NotBe(mcpServerUrl2);
        mcpServerUrl1.Equals(mcpServerUrl2).Should().BeFalse();
        (mcpServerUrl1 == mcpServerUrl2).Should().BeFalse();
        (mcpServerUrl1 != mcpServerUrl2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_ShouldReturnSameValue_GivenSameUriValues()
    {
        // Arrange
        var urlString = "https://api.example.com/mcp";
        var mcpServerUrl1 = McpServerUrl.Create(urlString).Value;
        var mcpServerUrl2 = McpServerUrl.Create(urlString).Value;

        // Act
        var hashCode1 = mcpServerUrl1.GetHashCode();
        var hashCode2 = mcpServerUrl2.GetHashCode();

        // Assert
        hashCode1.Should().Be(hashCode2);
    }

    [Theory]
    [InlineData("https://API.EXAMPLE.COM", "https://api.example.com")]
    [InlineData("https://example.com:443", "https://example.com/")]
    public void Equality_ShouldHandleUriNormalization_GivenEquivalentUris(string url1, string url2)
    {
        // Arrange
        var mcpServerUrl1 = McpServerUrl.Create(url1).Value;
        var mcpServerUrl2 = McpServerUrl.Create(url2).Value;

        // Act & Assert
        // Note: This test verifies that Uri normalization is handled correctly
        // The behavior depends on how Uri internally normalizes URLs
        mcpServerUrl1.Should().Be(mcpServerUrl2);
    }
}