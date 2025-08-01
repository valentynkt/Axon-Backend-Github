using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Domain.Types;
using Axon.Shared.Common;
using Axon.Tests.Shared.Builders;
using Axon.Tests.Shared.Mocks;
using Axon.Tests.Shared.TestBase;
using Axon.Tests.Shared.TestDoubles;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Application.Tests.Commands.ProcessMessage;

/// <summary>
/// London School TDD tests for ProcessMessageHandler focusing on behavior verification and interaction testing
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Application")]
[Category("LondonSchool")]
public sealed class ProcessMessageHandlerLondonSchoolTests : LondonSchoolTestBase
{
    private ProcessMessageHandler _handler = null!;
    private Mock<IAiClient> _aiClientMock = null!;
    private Mock<IMcpServerResolver> _mcpServerResolverMock = null!;
    private Mock<ILogger<ProcessMessageHandler>> _loggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        // Create strict mocks for all collaborators
        _aiClientMock = CreateStrictMock<IAiClient>();
        _mcpServerResolverMock = CreateStrictMock<IMcpServerResolver>();
        _loggerMock = CreateLooseMock<ILogger<ProcessMessageHandler>>();

        // Create system under test with injected mocks
        _handler = new ProcessMessageHandler(_aiClientMock.Object, _mcpServerResolverMock.Object, _loggerMock.Object);
    }

    [Test]
    [BehaviorTest]
    public async Task Handle_ShouldCoordinateWithAllCollaborators_WhenProcessingValidMessage()
    {
        // Arrange - Define the conversation between objects
        var command = ProcessMessageCommandBuilder
            .ForMessage("Test message")
            .Build();

        var mcpConfigs = new List<McpServerConfig>().AsReadOnly();
        var expectedAiRequest = new AiRequest("Test message", mcpConfigs, null);
        var expectedAiResponse = AiResponseBuilder
            .ForContent("AI response")
            .WithResponseId("response-123")
            .Build();

        // Setup the expected conversation
        _mcpServerResolverMock
            .Setup(x => x.GetEnabledServerConfigurations())
            .Returns(Result<IReadOnlyCollection<McpServerConfig>>.Success(mcpConfigs));

        _aiClientMock
            .Setup(x => x.ProcessMessageAsync(
                It.Is<AiRequest>(req => req.Message == expectedAiRequest.Message && req.McpConfigs == expectedAiRequest.McpConfigs),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify the conversation occurred as expected
        result.ShouldBeSuccessAnd(response =>
        {
            response.Response.ShouldBe("AI response");
            response.ConversationId.ShouldBe("response-123");
        });

        // Verify interaction patterns
        CqrsContractMocks.VerifyMcpResolverInteraction(_mcpServerResolverMock, Times.Once);
        CqrsContractMocks.VerifyAiClientInteraction(_aiClientMock, req => req.Message == "Test message", Times.Once);
    }

    [Test]
    [InteractionTest]
    public Task Handle_ShouldFollowCorrectInteractionSequence_WhenProcessingWithMcpServers()
    {
        // Arrange - Setup collaboration scenario
        var scenario = CreateCollaborationScenario<ProcessMessageHandler>()
            .WithCollaborator(_mcpServerResolverMock)
            .WithCollaborator(_aiClientMock)
            .ExpectingInteraction("MCP resolver called first")
            .ExpectingInteraction("AI client called with MCP configuration");

        var command = ProcessMessageCommandBuilder
            .ForMessage("Weather query")
            .Build();

        var mcpServers = new List<McpServerConfig>
        {
            new McpServerConfig("https://weather.api.com/mcp", "Weather", null, new[] { "weather" }, false)
        }.AsReadOnly();

        var expectedAiResponse = AiResponseBuilder
            .ForContent("Weather is sunny")
            .WithResponseId("weather-123")
            .Build();

        // Setup expected interaction sequence
        _mcpServerResolverMock
            .Setup(x => x.GetEnabledServerConfigurations())
            .Returns(Result<IReadOnlyCollection<McpServerConfig>>.Success(mcpServers));

        _aiClientMock
            .Setup(x => x.ProcessMessageAsync(
                It.Is<AiRequest>(req => req.McpConfigs != null && req.McpConfigs.Count == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act & Assert
        scenario.Execute(
            async handler => await handler.Handle(command, CancellationToken.None),
            _handler);

        // Verify the specific interaction occurred
        VerifyInteractionPattern(_mcpServerResolverMock, "MCP Configuration Resolution",
            mock => mock.Verify(x => x.GetEnabledServerConfigurations(), Times.Once));

        VerifyInteractionPattern(_aiClientMock, "AI Processing with MCP",
            mock => mock.Verify(x => x.ProcessMessageAsync(
                It.Is<AiRequest>(req => req.McpConfigs!.Count == 1),
                It.IsAny<CancellationToken>()), Times.Once));
        return Task.CompletedTask;
    }

    [Test]
    [ContractTest]
    public async Task Handle_ShouldEnforceErrorHandlingContract_WhenMcpResolverFails()
    {
        // Arrange - Setup failure scenario
        var command = ProcessMessageCommandBuilder.ForMessage("Test").Build();
        var expectedError = Error.InternalError("MCP configuration failed");

        _mcpServerResolverMock
            .Setup(x => x.GetEnabledServerConfigurations())
            .Returns(Result<IReadOnlyCollection<McpServerConfig>>.Failure(expectedError));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify error handling contract
        result.ShouldBeFailure();
        result.Error.ShouldBe(expectedError);

        // Verify interaction contract: AI client should NOT be called when MCP resolver fails
        _aiClientMock.Verify(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        // Verify logging contract
        VerifyLoggingBehavior(_loggerMock, LogLevel.Error, "Failed to load MCP server configurations", Times.AtLeastOnce);
    }

    [Test]
    [BehaviorTest]
    public async Task Handle_ShouldMapToolExecutionsCorrectly_WhenAiReturnsToolResults()
    {
        // Arrange - Setup tool execution scenario
        var command = ProcessMessageCommandBuilder.ForMessage("Execute tools").Build();
        
        var toolExecutions = new[]
        {
            ToolExecution.Success("calculator", "{\"operation\":\"add\",\"a\":5,\"b\":3}", "8", TimeSpan.FromMilliseconds(150)),
            ToolExecution.Failure("weather", "{\"location\":\"unknown\"}", "Location not found", TimeSpan.FromMilliseconds(75))
        };

        var aiResponse = new AiResponse("Tool results processed", "tool-response-456", toolExecutions);

        _mcpServerResolverMock
            .Setup(x => x.GetEnabledServerConfigurations())
            .Returns(Result<IReadOnlyCollection<McpServerConfig>>.Success(new List<McpServerConfig>().AsReadOnly()));

        _aiClientMock
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(aiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify tool execution mapping behavior
        result.ShouldBeSuccessAnd(response =>
        {
            response.ToolExecutions.ShouldNotBeNull();
            response.ToolExecutions!.Length.ShouldBe(2);

            var calculatorExecution = response.ToolExecutions[0];
            calculatorExecution.ToolName.ShouldBe("calculator");
            calculatorExecution.Success.ShouldBeTrue();
            calculatorExecution.Duration.TotalMilliseconds.ShouldBe(150);

            var weatherExecution = response.ToolExecutions[1];
            weatherExecution.ToolName.ShouldBe("weather");
            weatherExecution.Success.ShouldBeFalse();
            weatherExecution.Duration.TotalMilliseconds.ShouldBe(75);
        });

        // Verify behavior with tools was logged
        VerifyLoggingBehavior(_loggerMock, LogLevel.Information, "tool executions", Times.AtLeastOnce);
    }

    [Test]
    [InteractionTest]
    public async Task Handle_ShouldGenerateConversationId_WhenAiResponseLacksId()
    {
        // Arrange - Setup scenario where AI doesn't provide response ID
        var command = ProcessMessageCommandBuilder.ForMessage("Test").Build();
        var aiResponseWithoutId = AiResponseBuilder
            .ForContent("Response without ID")
            .WithoutResponseId()
            .Build();

        _mcpServerResolverMock
            .Setup(x => x.GetEnabledServerConfigurations())
            .Returns(Result<IReadOnlyCollection<McpServerConfig>>.Success(new List<McpServerConfig>().AsReadOnly()));

        _aiClientMock
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(aiResponseWithoutId));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify ID generation behavior
        result.ShouldBeSuccessAnd(response =>
        {
            response.ConversationId.ShouldNotBeNullOrEmpty();
            Guid.TryParse(response.ConversationId, out _).ShouldBeTrue();
        });

        // Verify the expected interactions occurred
        _mcpServerResolverMock.Verify(x => x.GetEnabledServerConfigurations(), Times.Once);
        _aiClientMock.Verify(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    [ContractTest]
    public async Task Handle_ShouldEnforceAiClientContract_WhenProcessingFails()
    {
        // Arrange - Setup AI client failure scenario
        var command = ProcessMessageCommandBuilder.ForMessage("Failing message").Build();
        var expectedError = Error.ExternalService("AI service unavailable");

        _mcpServerResolverMock
            .Setup(x => x.GetEnabledServerConfigurations())
            .Returns(Result<IReadOnlyCollection<McpServerConfig>>.Success(new List<McpServerConfig>().AsReadOnly()));

        _aiClientMock
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Failure(expectedError));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify error contract enforcement
        result.ShouldBeFailure();
        result.Error.ShouldBe(expectedError);

        // Verify error logging contract
        VerifyLoggingBehavior(_loggerMock, LogLevel.Error, "AI client failed to process message", Times.AtLeastOnce);
    }

    [Test]
    [BehaviorTest]
    public async Task Handle_ShouldPassThroughPreviousResponseId_WhenProvidedInCommand()
    {
        // Arrange - Setup scenario with previous response ID
        var command = ProcessMessageCommandBuilder
            .ForMessage("Continue conversation")
            .WithPreviousResponseId("prev-response-123")
            .Build();

        var expectedAiResponse = AiResponseBuilder
            .ForContent("Continuation response")
            .WithResponseId("new-response-456")
            .Build();

        _mcpServerResolverMock
            .Setup(x => x.GetEnabledServerConfigurations())
            .Returns(Result<IReadOnlyCollection<McpServerConfig>>.Success(new List<McpServerConfig>().AsReadOnly()));

        _aiClientMock
            .Setup(x => x.ProcessMessageAsync(
                It.Is<AiRequest>(req => req.PreviousResponseId == "prev-response-123"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify previous response ID was passed through
        result.ShouldBeSuccess();

        // Verify the specific interaction with previous response ID
        CqrsContractMocks.VerifyAiClientInteraction(_aiClientMock, 
            req => req.PreviousResponseId == "prev-response-123", Times.Once);
    }

    [Test]
    [InteractionTest]
    public async Task Handle_ShouldUseCommandBehaviorScenario_ForComplexInteractionTesting()
    {
        // Arrange - Use behavior scenario builder
        var scenario = CqrsContractMocks.CreateCommandScenario<ProcessMessageCommand, ProcessMessageResponse>()
            .WithMcpConfiguration(new List<McpServerConfig>
            {
                new McpServerConfig("https://test.mcp.com", "Test", null, new[] { "test" }, false)
            }.AsReadOnly())
            .WithSuccessfulAiProcessing(AiResponseBuilder
                .ForContent("Scenario response")
                .WithResponseId("scenario-123")
                .Build());

        var command = ProcessMessageCommandBuilder
            .ForMessage("Scenario test")
            .Build();

        // Setup handler with scenario mocks
        var scenarioHandler = new ProcessMessageHandler(
            scenario.AiClientMock.Object, 
            scenario.McpResolverMock.Object, 
            _loggerMock.Object);

        // Act
        var result = await scenario.ExecuteAsync(scenarioHandler, command);

        // Assert - Verify scenario execution
        result.ShouldBeSuccess();
        result.Value.Response.ShouldBe("Scenario response");
        result.Value.ConversationId.ShouldBe("scenario-123");

        // Verify scenario interactions
        scenario.VerifyInteractions(s =>
        {
            CqrsContractMocks.VerifyMcpResolverInteraction(s.McpResolverMock, Times.Once);
            CqrsContractMocks.VerifyAiClientInteraction(s.AiClientMock, req => req.Message == "Scenario test", Times.Once);
        });
    }

    [Test]
    [ContractTest]
    public async Task Handle_ShouldThrowArgumentNullException_WhenCommandIsNull()
    {
        // Act & Assert - Verify null handling contract
        var exception = await Should.ThrowAsync<ArgumentException>(() => _handler.Handle(null!, CancellationToken.None));
        
        // In the current implementation, it returns a Result with validation error instead of throwing
        // This test verifies the contract behavior
        exception.ShouldNotBeNull();
    }

    [Test]
    [BehaviorTest]
    public async Task Handle_ShouldLogProcessingProgress_ThroughoutExecution()
    {
        // Arrange - Setup for logging verification
        var command = ProcessMessageCommandBuilder.ForMessage("Logging test").Build();
        var expectedResponse = AiResponseBuilder.ForContent("Logged response").Build();

        _mcpServerResolverMock
            .Setup(x => x.GetEnabledServerConfigurations())
            .Returns(Result<IReadOnlyCollection<McpServerConfig>>.Success(new List<McpServerConfig>().AsReadOnly()));

        _aiClientMock
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify logging behavior throughout execution
        result.ShouldBeSuccess();

        // Verify start logging
        VerifyLoggingBehavior(_loggerMock, LogLevel.Information, "Processing message", Times.AtLeastOnce);

        // Verify completion logging
        VerifyLoggingBehavior(_loggerMock, LogLevel.Information, "Successfully processed message", Times.AtLeastOnce);
    }
}