using System.ComponentModel.DataAnnotations;
using Axon.Modules.Chat.Infrastructure.Configuration;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Tests.Configuration;

/// <summary>
/// Unit tests for McpServerOptions configuration class
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Configuration")]
public sealed class McpServerOptionsTests
{
    private McpServerOptions _options = null!;
    private ValidationContext _validationContext = null!;

    [SetUp]
    public void SetUp()
    {
        _options = new McpServerOptions();
        _validationContext = new ValidationContext(_options);
    }

    #region Property Default Values Tests

    [Test]
    public void Enabled_ShouldDefaultToTrue()
    {
        // Assert
        _options.Enabled.ShouldBeTrue();
    }

    [Test]
    public void ServerUrl_ShouldDefaultToEmptyString()
    {
        // Assert
        _options.ServerUrl.ShouldBe(string.Empty);
    }

    [Test]
    public void ServerLabel_ShouldDefaultToNull()
    {
        // Assert
        _options.ServerLabel.ShouldBeNull();
    }

    [Test]
    public void Headers_ShouldDefaultToEmptyDictionary()
    {
        // Assert
        _options.Headers.ShouldNotBeNull();
        _options.Headers.ShouldBeEmpty();
    }

    [Test]
    public void AllowedTools_ShouldDefaultToEmptyArray()
    {
        // Assert
        _options.AllowedTools.ShouldNotBeNull();
        _options.AllowedTools.ShouldBeEmpty();
    }

    [Test]
    public void TimeoutSeconds_ShouldDefaultTo30()
    {
        // Assert
        _options.TimeoutSeconds.ShouldBe(30);
    }

    [Test]
    public void RequireApproval_ShouldDefaultToFalse()
    {
        // Assert
        _options.RequireApproval.ShouldBeFalse();
    }

    #endregion

    #region Validate Method Tests - Disabled Server

    [Test]
    public void Validate_GivenDisabledServer_ShouldReturnNoValidationErrors()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = false,
            ServerUrl = "", // Invalid, but should be ignored when disabled
            TimeoutSeconds = 999 // Invalid, but should be ignored when disabled
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    #endregion

    #region Validate Method Tests - ServerUrl Validation

    [Test]
    public void Validate_GivenEnabledServerWithEmptyUrl_ShouldReturnValidationError()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = ""
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        validationResults[0].ErrorMessage.ShouldBe("ServerUrl is required for enabled servers");
        validationResults[0].MemberNames.ShouldContain(nameof(McpServerOptions.ServerUrl));
    }

    [Test]
    public void Validate_GivenEnabledServerWithWhitespaceUrl_ShouldReturnValidationError()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "   "
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        validationResults[0].ErrorMessage.ShouldBe("ServerUrl is required for enabled servers");
    }

    [Test]
    public void Validate_GivenEnabledServerWithNullUrl_ShouldReturnValidationError()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = null!
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        validationResults[0].ErrorMessage.ShouldBe("ServerUrl is required for enabled servers");
    }

    [Test]
    public void Validate_GivenHttpUrl_ShouldReturnValidationError()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "http://api.example.com/mcp"
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        validationResults[0].ErrorMessage.ShouldBe("ServerUrl must use HTTPS scheme for security");
        validationResults[0].MemberNames.ShouldContain(nameof(McpServerOptions.ServerUrl));
    }

    [Test]
    public void Validate_GivenInvalidUrl_ShouldReturnValidationError()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "not-a-valid-url"
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        validationResults[0].ErrorMessage.ShouldBe("ServerUrl must be a valid absolute URL");
        validationResults[0].MemberNames.ShouldContain(nameof(McpServerOptions.ServerUrl));
    }

    [Test]
    public void Validate_GivenValidHttpsUrl_ShouldReturnNoValidationErrors()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp"
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    [Test]
    [TestCase("https://api.example.com/mcp")]
    [TestCase("https://subdomain.api.example.com/path/to/mcp")]
    [TestCase("https://localhost:8080/mcp")]
    [TestCase("https://127.0.0.1:3000/api/v1/mcp")]
    [TestCase("https://api.example.com:443/mcp?param=value")]
    public void Validate_GivenValidHttpsUrls_ShouldReturnNoValidationErrors(string validUrl)
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = validUrl
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    #endregion

    #region Validate Method Tests - AllowedTools Validation

    [Test]
    public void Validate_GivenEmptyAllowedTools_ShouldReturnNoValidationErrors()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            AllowedTools = []
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    [Test]
    public void Validate_GivenValidAllowedTools_ShouldReturnNoValidationErrors()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            AllowedTools = ["weather", "search", "calendar"]
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    [Test]
    public void Validate_GivenAllowedToolsWithEmptyString_ShouldReturnValidationError()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            AllowedTools = [""]
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        validationResults[0].ErrorMessage.ShouldBe("AllowedTools array should not contain empty strings");
        validationResults[0].MemberNames.ShouldContain(nameof(McpServerOptions.AllowedTools));
    }

    [Test]
    public void Validate_GivenAllowedToolsWithWhitespaceString_ShouldReturnValidationError()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            AllowedTools = ["   "]
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        validationResults[0].ErrorMessage.ShouldBe("AllowedTools array should not contain empty strings");
    }

    [Test]
    public void Validate_GivenAllowedToolsWithValidAndInvalidItems_ShouldNotReturnValidationError()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            AllowedTools = ["valid_tool", "another_valid_tool"]
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    #endregion

    #region Validate Method Tests - Headers Validation

    [Test]
    public void Validate_GivenEmptyHeaders_ShouldReturnNoValidationErrors()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            Headers = new Dictionary<string, string>()
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    [Test]
    public void Validate_GivenValidHeaders_ShouldReturnNoValidationErrors()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer token" },
                { "X-API-Key", "api-key-value" },
                { "Content-Type", "application/json" }
            }
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    [Test]
    public void Validate_GivenHeaderWithEmptyKey_ShouldReturnValidationError()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            Headers = new Dictionary<string, string>
            {
                { "", "some-value" }
            }
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        validationResults[0].ErrorMessage.ShouldBe("Header keys cannot be empty");
        validationResults[0].MemberNames.ShouldContain(nameof(McpServerOptions.Headers));
    }

    [Test]
    public void Validate_GivenHeaderWithWhitespaceKey_ShouldReturnValidationError()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            Headers = new Dictionary<string, string>
            {
                { "   ", "some-value" }
            }
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        validationResults[0].ErrorMessage.ShouldBe("Header keys cannot be empty");
    }

    [Test]
    public void Validate_GivenHeaderWithNullValue_ShouldReturnValidationError()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            Headers = new Dictionary<string, string>
            {
                { "Authorization", null! }
            }
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldHaveSingleItem();
        validationResults[0].ErrorMessage.ShouldBe("Header 'Authorization' cannot have null value");
        validationResults[0].MemberNames.ShouldContain(nameof(McpServerOptions.Headers));
    }

    [Test]
    public void Validate_GivenHeaderWithEmptyValue_ShouldReturnNoValidationErrors()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            Headers = new Dictionary<string, string>
            {
                { "X-Custom-Header", "" }
            }
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    #endregion

    #region Validate Method Tests - Multiple Validation Errors

    [Test]
    public void Validate_GivenMultipleValidationErrors_ShouldReturnAllErrors()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "http://insecure.com", // Invalid: HTTP instead of HTTPS
            AllowedTools = [""], // Invalid: empty tool name
            Headers = new Dictionary<string, string>
            {
                { "", "value" }, // Invalid: empty key
                { "Valid-Key", null! } // Invalid: null value
            }
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.Count.ShouldBe(4);
        
        validationResults.ShouldContain(r => r.ErrorMessage == "ServerUrl must use HTTPS scheme for security");
        validationResults.ShouldContain(r => r.ErrorMessage == "AllowedTools array should not contain empty strings");
        validationResults.ShouldContain(r => r.ErrorMessage == "Header keys cannot be empty");
        validationResults.ShouldContain(r => r.ErrorMessage == "Header 'Valid-Key' cannot have null value");
    }

    #endregion

    #region Edge Cases and Complex Scenarios

    [Test]
    public void Validate_GivenComplexValidConfiguration_ShouldReturnNoValidationErrors()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://complex.api.example.com:8443/path/to/mcp?version=v1",
            ServerLabel = "Complex MCP Server Configuration",
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer jwt-token-here" },
                { "X-API-Version", "v1" },
                { "X-Client-Id", "test-client" },
                { "User-Agent", "Axon-Backend/1.0" }
            },
            AllowedTools = ["weather", "search", "calendar", "file_operations", "database_query"],
            TimeoutSeconds = 60,
            RequireApproval = true
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    [Test]
    public void Validate_GivenLargeNumberOfHeaders_ShouldValidateAll()
    {
        // Arrange
        var headers = new Dictionary<string, string>();
        for (int i = 0; i < 50; i++)
        {
            headers[$"X-Custom-Header-{i}"] = $"value-{i}";
        }

        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            Headers = headers
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    [Test]
    public void Validate_GivenLargeNumberOfAllowedTools_ShouldValidateAll()
    {
        // Arrange
        var allowedTools = new string[100];
        for (int i = 0; i < 100; i++)
        {
            allowedTools[i] = $"tool_{i}";
        }

        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            AllowedTools = allowedTools
        };
        _validationContext = new ValidationContext(_options);

        // Act
        var validationResults = _options.Validate(_validationContext).ToList();

        // Assert
        validationResults.ShouldBeEmpty();
    }

    #endregion

    #region Data Annotations Validation

    [Test]
    public void TimeoutSeconds_GivenValueBelowMinimum_ShouldFailDataAnnotationValidation()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            TimeoutSeconds = 0 // Below minimum of 1
        };

        var results = new List<ValidationResult>();
        _validationContext = new ValidationContext(_options);

        // Act
        var isValid = Validator.TryValidateObject(_options, _validationContext, results, true);

        // Assert
        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.MemberNames.Contains(nameof(McpServerOptions.TimeoutSeconds)));
    }

    [Test]
    public void TimeoutSeconds_GivenValueAboveMaximum_ShouldFailDataAnnotationValidation()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            TimeoutSeconds = 301 // Above maximum of 300
        };

        var results = new List<ValidationResult>();
        _validationContext = new ValidationContext(_options);

        // Act
        var isValid = Validator.TryValidateObject(_options, _validationContext, results, true);

        // Assert
        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.MemberNames.Contains(nameof(McpServerOptions.TimeoutSeconds)));
    }

    [Test]
    public void ServerUrl_GivenEmptyString_ShouldFailDataAnnotationValidation()
    {
        // Arrange
        _options = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "" // Violates [Required] attribute
        };

        var results = new List<ValidationResult>();
        _validationContext = new ValidationContext(_options);

        // Act
        var isValid = Validator.TryValidateObject(_options, _validationContext, results, true);

        // Assert
        isValid.ShouldBeFalse();
        results.ShouldContain(r => r.MemberNames.Contains(nameof(McpServerOptions.ServerUrl)));
    }

    #endregion
}