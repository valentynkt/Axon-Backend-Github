using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Infrastructure.Configuration;
using Axon.Modules.Chat.Infrastructure.Services;
using Axon.Shared.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for McpServerResolver service
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Infrastructure")]
public sealed class McpServerResolverTests
{
    private McpServerResolver _resolver = null!;
    private Mock<IOptions<McpServersOptions>> _optionsMock = null!;
    private Mock<ILogger<McpServerResolver>> _loggerMock = null!;
    private McpServersOptions _mcpServersOptions = null!;

    [SetUp]
    public void SetUp()
    {
        _mcpServersOptions = new McpServersOptions();
        _optionsMock = new Mock<IOptions<McpServersOptions>>();
        _optionsMock.Setup(x => x.Value).Returns(_mcpServersOptions);
        _loggerMock = new Mock<ILogger<McpServerResolver>>();
        
        _resolver = new McpServerResolver(_optionsMock.Object, _loggerMock.Object);
    }

    #region GetEnabledServerConfigurations Tests

    [Test]
    public void GetEnabledServerConfigurations_GivenNoServers_ShouldReturnEmptyCollection()
    {
        // Act
        var result = _resolver.GetEnabledServerConfigurations();

        // Assert
        result.ShouldBeSuccessAnd(servers =>
        {
            servers.ShouldNotBeNull();
            servers.Count.ShouldBe(0);
        });
    }

    [Test]
    public void GetEnabledServerConfigurations_GivenOnlyDisabledServers_ShouldReturnEmptyCollection()
    {
        // Arrange
        _mcpServersOptions.Servers["disabled1"] = new McpServerOptions
        {
            Enabled = false,
            ServerUrl = "https://api1.example.com/mcp"
        };
        _mcpServersOptions.Servers["disabled2"] = new McpServerOptions
        {
            Enabled = false,
            ServerUrl = "https://api2.example.com/mcp"
        };

        // Act
        var result = _resolver.GetEnabledServerConfigurations();

        // Assert
        result.ShouldBeSuccessAnd(servers =>
        {
            servers.ShouldNotBeNull();
            servers.Count.ShouldBe(0);
        });

        // Verify debug logging for skipped servers
        VerifyLoggerDebug("Skipping disabled MCP server 'disabled1'");
        VerifyLoggerDebug("Skipping disabled MCP server 'disabled2'");
    }

    [Test]
    public void GetEnabledServerConfigurations_GivenMixedEnabledDisabledServers_ShouldReturnOnlyEnabled()
    {
        // Arrange
        const string enabledServerId = "enabled_server";
        const string enabledServerUrl = "https://api.enabled.com/mcp";
        const string enabledServerLabel = "Enabled Server";
        
        _mcpServersOptions.Servers[enabledServerId] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = enabledServerUrl,
            ServerLabel = enabledServerLabel,
            Headers = new Dictionary<string, string> { { "Authorization", "Bearer token" } },
            AllowedTools = ["weather", "search"],
            RequireApproval = true
        };
        
        _mcpServersOptions.Servers["disabled_server"] = new McpServerOptions
        {
            Enabled = false,
            ServerUrl = "https://api.disabled.com/mcp"
        };

        // Act
        var result = _resolver.GetEnabledServerConfigurations();

        // Assert
        result.ShouldBeSuccessAnd(servers =>
        {
            servers.ShouldNotBeNull();
            servers.Count.ShouldBe(1);
            
            var server = servers.First();
            server.ServerUrl.ShouldBe(enabledServerUrl);
            server.ServerLabel.ShouldBe(enabledServerLabel);
            server.Headers.ShouldNotBeNull();
            server.Headers!["Authorization"].ShouldBe("Bearer token");
            server.AllowedTools.ShouldNotBeNull();
            server.AllowedTools!.Length.ShouldBe(2);
            server.AllowedTools.ShouldContain("weather");
            server.AllowedTools.ShouldContain("search");
            server.RequireApproval.ShouldBeTrue();
        });

        // Verify logging
        VerifyLoggerDebug($"Loaded enabled MCP server '{enabledServerId}' at URL '{enabledServerUrl}' with 2 allowed tools");
        VerifyLoggerDebug("Skipping disabled MCP server 'disabled_server'");
    }

    [Test]
    public void GetEnabledServerConfigurations_GivenMultipleEnabledServers_ShouldReturnAll()
    {
        // Arrange
        _mcpServersOptions.Servers["weather"] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://weather.example.com/mcp",
            ServerLabel = "Weather Service",
            Headers = new Dictionary<string, string> { { "API-Key", "weather-key" } },
            AllowedTools = ["weather"]
        };
        
        _mcpServersOptions.Servers["search"] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://search.example.com/mcp",
            ServerLabel = null, // Should use server ID as label
            AllowedTools = ["search", "summarize"]
        };

        // Act
        var result = _resolver.GetEnabledServerConfigurations();

        // Assert
        result.ShouldBeSuccessAnd(servers =>
        {
            servers.ShouldNotBeNull();
            servers.Count.ShouldBe(2);
            
            var weatherServer = servers.First(s => s.ServerUrl.Contains("weather", StringComparison.OrdinalIgnoreCase));
            weatherServer.ServerLabel.ShouldBe("Weather Service");
            weatherServer.Headers!["API-Key"].ShouldBe("weather-key");
            weatherServer.AllowedTools!.Length.ShouldBe(1);
            
            var searchServer = servers.First(s => s.ServerUrl.Contains("search", StringComparison.OrdinalIgnoreCase));
            searchServer.ServerLabel.ShouldBe("search"); // Uses server ID as label
            searchServer.Headers.ShouldBeNull();
            searchServer.AllowedTools!.Length.ShouldBe(2);
        });
    }

    [Test]
    public void GetEnabledServerConfigurations_GivenServerWithEmptyHeaders_ShouldReturnNullHeaders()
    {
        // Arrange
        _mcpServersOptions.Servers["test_server"] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            Headers = new Dictionary<string, string>()
        };

        // Act
        var result = _resolver.GetEnabledServerConfigurations();

        // Assert
        result.ShouldBeSuccessAnd(servers =>
        {
            servers.ShouldNotBeNull();
            servers.Count.ShouldBe(1);
            servers.First().Headers.ShouldBeNull();
        });
    }

    [Test]
    public void GetEnabledServerConfigurations_GivenServerWithEmptyAllowedTools_ShouldReturnNullAllowedTools()
    {
        // Arrange
        _mcpServersOptions.Servers["test_server"] = new McpServerOptions
        {
            Enabled = true,
            ServerUrl = "https://api.example.com/mcp",
            AllowedTools = []
        };

        // Act
        var result = _resolver.GetEnabledServerConfigurations();

        // Assert
        result.ShouldBeSuccessAnd(servers =>
        {
            servers.ShouldNotBeNull();
            servers.Count.ShouldBe(1);
            servers.First().AllowedTools.ShouldBeNull();
        });
    }

    #endregion


    #region Constructor Tests

    [Test]
    public void Constructor_GivenNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new McpServerResolver(null!, _loggerMock.Object));
    }

    [Test]
    public void Constructor_GivenValidParameters_ShouldCreateInstance()
    {
        // Act & Assert (no exception should be thrown)
        var resolver = new McpServerResolver(_optionsMock.Object, _loggerMock.Object);
        resolver.ShouldNotBeNull();
    }

    #endregion

    #region Test Helpers

    private void VerifyLoggerWarning(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyLoggerDebug(string expectedMessage)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}