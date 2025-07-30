using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
public sealed class McpServerUrlTests : DomainTestBase
{
    [Test]
    [TestCase("https://api.example.com", "https://api.example.com/")]
    [TestCase("https://localhost:8080", "https://localhost:8080/")]
    [TestCase("https://subdomain.example.com/path", "https://subdomain.example.com/path")]
    [TestCase("https://api.example.com:443/v1/mcp", "https://api.example.com/v1/mcp")]
    public void Create_GivenValidHttpsUrl_ShouldReturnSuccessWithNormalizedUrl(string validUrl, string expectedNormalizedUrl)

    {
        // Act
        var result = McpServerUrl.Create(validUrl);

        // Assert
        result.ShouldBeSuccessAnd(mcpUrl => 
            mcpUrl.Value.ToString().ShouldBe(expectedNormalizedUrl));
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Create_GivenNullOrWhitespaceString_ShouldReturnValidationError(string? input)

    {
        // Act
        var result = McpServerUrl.Create(input);

        // Assert
        result.ShouldBeValidationFailure("MCP server URL cannot be null or empty");
    }

    [Test]
    [TestCase("not-a-url")]
    [TestCase("://invalid-url")]
    [TestCase("relative/path")]
    public void Create_GivenInvalidUrl_ShouldReturnValidationError(string invalidUrl)

    {
        // Act
        var result = McpServerUrl.Create(invalidUrl);

        // Assert
        result.ShouldBeValidationFailure("MCP server URL must be a valid absolute URL");
    }

    [Test]
    [TestCase("http://example.com")]
    [TestCase("ftp://example.com")]
    [TestCase("ws://example.com")]
    [TestCase("file://example.com")]
    [TestCase("file://local/path")]
    public void Create_GivenNonHttpsScheme_ShouldReturnValidationError(string nonHttpsUrl)

    {
        // Act
        var result = McpServerUrl.Create(nonHttpsUrl);

        // Assert
        result.ShouldBeValidationFailure("MCP server URL must use HTTPS scheme for security");
    }

    [Test]
    public void Create_GivenValidHttpsUri_ShouldReturnSuccessWithCorrectValue()

    {
        // Arrange
        var validUri = new Uri("https://api.example.com/mcp");

        // Act
        var result = McpServerUrl.Create(validUri);

        // Assert
        result.ShouldBeSuccessAnd(mcpUrl => 
            mcpUrl.Value.ShouldBe(validUri));
    }

    [Test]
    public void Create_GivenNullUri_ShouldReturnValidationError()

    {
        // Act
        var result = McpServerUrl.Create((Uri?)null);

        // Assert
        result.ShouldBeValidationFailure("MCP server URI cannot be null");
    }

    [Test]
    public void Create_GivenRelativeUri_ShouldReturnValidationError()

    {
        // Arrange
        var relativeUri = new Uri("/relative/path", UriKind.Relative);

        // Act
        var result = McpServerUrl.Create(relativeUri);

        // Assert
        result.ShouldBeValidationFailure("MCP server URI must be absolute");
    }

    [Test]
    public void Create_GivenNonHttpsUriScheme_ShouldReturnValidationError()

    {
        // Arrange
        var httpUri = new Uri("http://example.com");

        // Act
        var result = McpServerUrl.Create(httpUri);

        // Assert
        result.ShouldBeValidationFailure("MCP server URI must use HTTPS scheme for security");
    }

    [Test]
    public void ToString_GivenValidMcpServerUrl_ShouldReturnUriString()
    {
        // Arrange
        var urlString = "https://api.example.com/mcp";
        var mcpServerUrl = McpServerUrl.Create(urlString).ShouldBeSuccessWithValue();


        // Act
        var result = mcpServerUrl.ToString();

        // Assert
        result.ShouldBe(urlString);
    }

    [Test]
    public void ImplicitOperator_GivenValidMcpServerUrl_ShouldConvertToUri()
    {
        // Arrange
        var originalUri = new Uri("https://api.example.com/mcp");
        var mcpServerUrl = McpServerUrl.Create(originalUri).ShouldBeSuccessWithValue();


        // Act
        Uri convertedUri = mcpServerUrl;

        // Assert
        convertedUri.ShouldBe(originalUri);
    }

    [Test]
    public void ImplicitOperator_GivenValidMcpServerUrl_ShouldConvertToString()
    {
        // Arrange
        var urlString = "https://api.example.com/mcp";
        var mcpServerUrl = McpServerUrl.Create(urlString).ShouldBeSuccessWithValue();


        // Act
        string convertedString = mcpServerUrl;

        // Assert
        convertedString.ShouldBe(urlString);
    }

    [Test]
    public void Equality_GivenSameUriValues_ShouldReturnTrue()
    {
        // Arrange
        var urlString = "https://api.example.com/mcp";
        var mcpServerUrl1 = McpServerUrl.Create(urlString).ShouldBeSuccessWithValue();
        var mcpServerUrl2 = McpServerUrl.Create(urlString).ShouldBeSuccessWithValue();

        // Act & Assert
        mcpServerUrl1.ShouldBe(mcpServerUrl2);
        mcpServerUrl1.Equals(mcpServerUrl2).ShouldBeTrue();
        (mcpServerUrl1 == mcpServerUrl2).ShouldBeTrue();
        (mcpServerUrl1 != mcpServerUrl2).ShouldBeFalse();
    }

    [Test]
    public void Equality_GivenDifferentUriValues_ShouldReturnFalse()
    {
        // Arrange
        var mcpServerUrl1 = McpServerUrl.Create("https://api1.example.com").ShouldBeSuccessWithValue();
        var mcpServerUrl2 = McpServerUrl.Create("https://api2.example.com").ShouldBeSuccessWithValue();

        // Act & Assert
        mcpServerUrl1.ShouldNotBe(mcpServerUrl2);
        mcpServerUrl1.Equals(mcpServerUrl2).ShouldBeFalse();
        (mcpServerUrl1 == mcpServerUrl2).ShouldBeFalse();
        (mcpServerUrl1 != mcpServerUrl2).ShouldBeTrue();
    }

    [Test]
    public void GetHashCode_GivenSameUriValues_ShouldReturnSameValue()
    {
        // Arrange
        var urlString = "https://api.example.com/mcp";
        var mcpServerUrl1 = McpServerUrl.Create(urlString).ShouldBeSuccessWithValue();
        var mcpServerUrl2 = McpServerUrl.Create(urlString).ShouldBeSuccessWithValue();


        // Act
        var hashCode1 = mcpServerUrl1.GetHashCode();
        var hashCode2 = mcpServerUrl2.GetHashCode();

        // Assert
        hashCode1.ShouldBe(hashCode2);
    }

    [Test]
    [TestCase("https://API.EXAMPLE.COM", "https://api.example.com")]
    [TestCase("https://example.com:443", "https://example.com/")]
    public void Equality_GivenEquivalentUris_ShouldHandleUriNormalization(string url1, string url2)
    {
        // Arrange
        var mcpServerUrl1 = McpServerUrl.Create(url1).ShouldBeSuccessWithValue();
        var mcpServerUrl2 = McpServerUrl.Create(url2).ShouldBeSuccessWithValue();


        // Act & Assert
        // Note: This test verifies that Uri normalization is handled correctly
        // The behavior depends on how Uri internally normalizes URLs
        mcpServerUrl1.ShouldBe(mcpServerUrl2);
    }

    [Test]
    public void Factory_ShouldCreateValidMcpServerUrl()
    {
        // Act
        var mcpServerUrl = ChatDomainFactory.ValidMcpServerUrl();

        // Assert
        mcpServerUrl.Value.Scheme.ShouldBe("https");
        mcpServerUrl.Value.IsAbsoluteUri.ShouldBeTrue();
    }

    [Test]
    public void Factory_ShouldCreateMcpServerUrlFromCustomUrl()
    {
        // Arrange
        var customUrl = "https://custom.example.com/api/mcp";

        // Act
        var mcpServerUrl = ChatDomainFactory.ValidMcpServerUrl(customUrl);

        // Assert
        mcpServerUrl.Value.ToString().ShouldBe(customUrl);

    }
}