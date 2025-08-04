using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Infrastructure.Configuration;
using Axon.Modules.Chat.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Tests.Services;

/// <summary>
/// Simplified unit tests for McpServerResolver service
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

    [Test]
    public void GetEnabledServerConfigurations_GivenNoServers_ShouldReturnEmptyCollection()
    {
        // Act
        var result = _resolver.GetEnabledServerConfigurations();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(0);
    }

    [Test]
    public void Constructor_WithValidDependencies_ShouldCreateInstance()
    {
        // Act & Assert
        var resolver = new McpServerResolver(_optionsMock.Object, _loggerMock.Object);
        resolver.ShouldNotBeNull();
    }

    [Test]
    public void McpServerResolver_Infrastructure_PlaceholderTest()
    {
        // This is a placeholder test to ensure comprehensive testing would be implemented
        // when the infrastructure patterns are fully established
        _resolver.ShouldNotBeNull();
        _mcpServersOptions.ShouldNotBeNull();
    }
}