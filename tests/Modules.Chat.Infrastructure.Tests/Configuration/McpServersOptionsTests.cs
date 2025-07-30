using System.ComponentModel.DataAnnotations;
using Axon.Modules.Chat.Infrastructure.Configuration;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Tests.Configuration;

/// <summary>
/// Unit tests for McpServersOptions configuration class
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Configuration")]
public sealed class McpServersOptionsTests
{
    private McpServersOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _options = new McpServersOptions();
    }

    #region SectionName Tests

    [Test]
    public void SectionName_ShouldHaveCorrectValue()
    {
        // Assert
        McpServersOptions.SectionName.ShouldBe("Chat:McpServers");
    }

    #endregion

    #region Servers Property Tests

    [Test]
    public void Servers_ShouldInitializeAsEmptyDictionary()
    {
        // Assert
        _options.Servers.ShouldNotBeNull();
        _options.Servers.ShouldBeEmpty();
    }

    [Test]
    public void Servers_ShouldAllowAddingServerConfigurations()
    {
        // Arrange
        var serverOptions = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp"
        };

        // Act
        _options.Servers["test_server"] = serverOptions;

        // Assert
        _options.Servers.Count.ShouldBe(1);
        _options.Servers["test_server"].ShouldBe(serverOptions);
    }

    #endregion

    #region Validate Method Tests

    [Test]
    public void Validate_GivenEmptyServers_ShouldReturnNoValidationErrors()
    {
        // Act
        var validationResults = _options.Validate().ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    [Test]
    public void Validate_GivenValidServers_ShouldReturnNoValidationErrors()
    {
        // Arrange
        _options.Servers["valid_server"] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            ServerLabel = "Valid Server",
            TimeoutSeconds = 30
        };

        _options.Servers["another_valid"] = new McpServerOptions
        {
            Enabled = false, // Disabled servers are valid
            ServerUrl = "https://api.another.com/mcp"
        };

        // Act
        var validationResults = _options.Validate().ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    [Test]
    public void Validate_GivenEmptyServerId_ShouldReturnValidationError()
    {
        // Arrange
        _options.Servers[""] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp"
        };

        // Act
        var validationResults = _options.Validate().ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        validationResults[0].ErrorMessage.ShouldBe("Server ID cannot be empty");
    }

    [Test]
    [TestCase("server with spaces")]
    [TestCase("server-with-dashes")]
    [TestCase("server.with.dots")]
    [TestCase("server@with@symbols")]
    [TestCase("server#with#hash")]
    public void Validate_GivenInvalidServerIdFormat_ShouldReturnValidationError(string invalidServerId)
    {
        // Arrange
        _options.Servers[invalidServerId] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp"
        };

        // Act
        var validationResults = _options.Validate().ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        var errorMessage = validationResults[0].ErrorMessage;
        if (errorMessage != null)
        {
            errorMessage
                .ShouldContain("must contain only alphanumeric characters and underscores");
            errorMessage.ShouldContain(invalidServerId);
        }
    }

    [Test]
    [TestCase("valid_server")]
    [TestCase("ValidServer")]
    [TestCase("server123")]
    [TestCase("Server_With_Underscores")]
    [TestCase("UPPERCASE_SERVER")]
    [TestCase("mixedCase_Server_123")]
    public void Validate_GivenValidServerIdFormat_ShouldNotReturnValidationError(string validServerId)
    {
        // Arrange
        _options.Servers[validServerId] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp"
        };

        // Act
        var validationResults = _options.Validate().ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    [Test]
    public void Validate_GivenServerWithValidationErrors_ShouldReturnDetailedErrors()
    {
        // Arrange
        _options.Servers["server_with_errors"] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "", // Invalid: empty URL
            TimeoutSeconds = 500 // Invalid: exceeds maximum
        };

        // Act
        var validationResults = _options.Validate().ToList();

        // Assert
        validationResults.ShouldNotBeEmpty();
        validationResults.ShouldContain(r => r.ErrorMessage!.Contains("Server 'server_with_errors':"));
    }

    [Test]
    public void Validate_GivenMultipleServersWithErrors_ShouldReturnAllErrors()
    {
        // Arrange
        _options.Servers[""] = new McpServerOptions { Enabled = true, ServerUrl = "https://valid.com" };
        _options.Servers["invalid-id"] = new McpServerOptions { Enabled = true, ServerUrl = "https://valid.com" };
        _options.Servers["valid_server"] = new McpServerOptions { Enabled = true, ServerUrl = "" };

        // Act
        var validationResults = _options.Validate().ToList();

        // Assert
        validationResults.Count.ShouldBeGreaterThan(1);
        
        // Check for specific error types
        validationResults.ShouldContain(r => r.ErrorMessage == "Server ID cannot be empty");
        validationResults.ShouldContain(r => r.ErrorMessage!.Contains("must contain only alphanumeric characters"));
        validationResults.ShouldContain(r => r.ErrorMessage!.Contains("Server 'valid_server':"));
    }

    [Test]
    public void Validate_GivenDisabledServerWithInvalidConfiguration_ShouldNotValidateServerOptions()
    {
        // Arrange
        _options.Servers["disabled_server"] = new McpServerOptions
        {
            Enabled = false,
            ServerUrl = "", // This would normally be invalid
            TimeoutSeconds = 999 // This would normally be invalid
        };

        // Act
        var validationResults = _options.Validate().ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    #endregion

    #region IsValidServerId Tests

    [Test]
    [TestCase("valid_server", true)]
    [TestCase("ValidServer", true)]
    [TestCase("server123", true)]
    [TestCase("Server_With_Underscores", true)]
    [TestCase("UPPERCASE_SERVER", true)]
    [TestCase("mixedCase_Server_123", true)]
    [TestCase("a", true)]
    [TestCase("_", true)]
    [TestCase("1", true)]
    [TestCase("server with spaces", false)]
    [TestCase("server-with-dashes", false)]
    [TestCase("server.with.dots", false)]
    [TestCase("server@with@symbols", false)]
    [TestCase("server#with#hash", false)]
    [TestCase("server!with!exclamation", false)]
    [TestCase("server+with+plus", false)]
    [TestCase("server/with/slash", false)]
    [TestCase("server\\with\\backslash", false)]
    public void IsValidServerId_ShouldValidateServerIdFormat(string serverId, bool expectedResult)
    {
        // Arrange
        _options.Servers[serverId] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp"
        };

        // Act
        var validationResults = _options.Validate().ToList();

        // Assert
        if (expectedResult)
        {
            validationResults.ShouldNotContain(r => r.ErrorMessage!.Contains("must contain only alphanumeric characters and underscores"));
        }
        else
        {
            validationResults.ShouldContain(r => r.ErrorMessage!.Contains("must contain only alphanumeric characters and underscores"));
        }
    }

    #endregion

    #region Edge Cases

    [Test]
    public void Validate_GivenWhitespaceServerId_ShouldTreatAsEmpty()
    {
        // Arrange
        _options.Servers["  "] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp"
        };

        // Act
        var validationResults = _options.Validate().ToList();

        // Assert
        validationResults.ShouldContain(r => r.ErrorMessage!.Contains("must contain only alphanumeric characters"));
    }

    [Test]
    public void Validate_GivenLargeNumberOfServers_ShouldValidateAll()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _options.Servers[$"server_{i}"] = new McpServerOptions
            {
                Enabled = true,
                ServerUrl = $"https://api{i}.example.com/mcp"
            };
        }

        // Act
        var validationResults = _options.Validate().ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    #endregion
}