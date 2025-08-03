using System.Linq.Expressions;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Shared.Common;
using Axon.Shared.Common.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using MediatRIMediator = MediatR.IMediator;
using MediatRIRequest = MediatR.IRequest;
// MediatR IRequestHandler is generic and cannot be aliased without type parameters
using AxonIRequest = Axon.Shared.Common.Abstractions.IRequest;
// Axon IRequestHandler is also generic and cannot be aliased without type parameters

namespace Axon.Tests.Shared.Mocks;

/// <summary>
/// Comprehensive mock strategies for CQRS patterns following London School TDD principles
/// </summary>
public static class CqrsContractMocks
{
    /// <summary>
    /// Creates a mock MediatR that tracks all requests and responses
    /// </summary>
    public static Mock<MediatRIMediator> CreateMediatorMock()
    {
        var mock = new Mock<MediatRIMediator>(MockBehavior.Strict);
        
        // Track all command/query interactions
        mock.Setup(x => x.Send(It.IsAny<MediatRIRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
            
        mock.Setup(x => x.Send(It.IsAny<MediatRIRequest<Result>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);
            
        return mock;
    }

    /// <summary>
    /// Creates a command handler mock with behavior verification
    /// </summary>
    public static Mock<IRequestHandler<TCommand, Result<TResponse>>> CreateCommandHandlerMock<TCommand, TResponse>()
        where TCommand : class, IRequest<Result<TResponse>>
    {
        var mock = new Mock<IRequestHandler<TCommand, Result<TResponse>>>(MockBehavior.Strict);
        
        // Default setup for successful command handling
        mock.Setup(x => x.Handle(It.IsAny<TCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TResponse>.Success(default(TResponse)!));
            
        return mock;
    }

    /// <summary>
    /// Creates a query handler mock with result verification
    /// </summary>
    public static Mock<IRequestHandler<TQuery, Result<TResponse>>> CreateQueryHandlerMock<TQuery, TResponse>()
        where TQuery : class, IRequest<Result<TResponse>>
    {
        var mock = new Mock<IRequestHandler<TQuery, Result<TResponse>>>(MockBehavior.Strict);
        
        // Default setup for successful query handling
        mock.Setup(x => x.Handle(It.IsAny<TQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TResponse>.Success(default(TResponse)!));
            
        return mock;
    }

    /// <summary>
    /// Creates an AI client mock with comprehensive interaction tracking
    /// </summary>
    public static Mock<IAiClient> CreateAiClientMock()
    {
        var mock = new Mock<IAiClient>(MockBehavior.Strict);
        
        // Track all AI processing interactions
        mock.Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(new AiResponse("Mock response", "mock-id", null)));
            
        return mock;
    }

    /// <summary>
    /// Creates MCP server resolver mock with configuration tracking
    /// </summary>
    public static Mock<IMcpServerResolver> CreateMcpServerResolverMock()
    {
        var mock = new Mock<IMcpServerResolver>(MockBehavior.Strict);
        
        // Default to empty configuration
        mock.Setup(x => x.GetEnabledServerConfigurations())
            .Returns(Result<IReadOnlyCollection<McpServerConfig>>.Success(
                new List<McpServerConfig>().AsReadOnly()));
                
        return mock;
    }

    /// <summary>
    /// Creates a logger mock with structured logging verification
    /// </summary>
    public static Mock<ILogger<T>> CreateLoggerMock<T>()
    {
        var mock = new Mock<ILogger<T>>(MockBehavior.Loose);
        
        // Allow all logging calls - we'll verify specific ones in tests
        mock.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
            
        mock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>()))
            .Returns(true);
            
        return mock;
    }

    /// <summary>
    /// Verifies command handler interaction pattern
    /// </summary>
    public static void VerifyCommandHandlerInteraction<TCommand, TResponse>(
        Mock<IRequestHandler<TCommand, Result<TResponse>>> handlerMock,
        TCommand expectedCommand,
        Times times)
        where TCommand : class, IRequest<Result<TResponse>>
    {
        handlerMock.Verify(
            x => x.Handle(It.Is<TCommand>(cmd => CommandMatches(cmd, expectedCommand)), It.IsAny<CancellationToken>()),
            times);
    }

    /// <summary>
    /// Verifies AI client interaction with specific request patterns
    /// </summary>
    public static void VerifyAiClientInteraction(
        Mock<IAiClient> aiClientMock,
        Expression<Func<AiRequest, bool>> requestMatcher,
        Func<Times> times)
    {
        aiClientMock.Verify(
            x => x.ProcessMessageAsync(It.Is(requestMatcher), It.IsAny<CancellationToken>()),
            times);
    }

    /// <summary>
    /// Verifies MCP resolver was called for configuration
    /// </summary>
    public static void VerifyMcpResolverInteraction(
        Mock<IMcpServerResolver> resolverMock,
        Func<Times> times)
    {
        resolverMock.Verify(
            x => x.GetEnabledServerConfigurations(),
            times);
    }

    /// <summary>
    /// Sets up AI client to return specific response for behavior testing
    /// </summary>
    public static void SetupAiClientBehavior(
        Mock<IAiClient> aiClientMock,
        AiResponse response)
    {
        aiClientMock.Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(response));
    }

    /// <summary>
    /// Sets up AI client to return failure for error behavior testing
    /// </summary>
    public static void SetupAiClientFailure(
        Mock<IAiClient> aiClientMock,
        Error error)
    {
        aiClientMock.Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Failure(error));
    }

    /// <summary>
    /// Sets up MCP resolver with specific configuration for behavior testing
    /// </summary>
    public static void SetupMcpResolverBehavior(
        Mock<IMcpServerResolver> resolverMock,
        IReadOnlyCollection<McpServerConfig> configs)
    {
        resolverMock.Setup(x => x.GetEnabledServerConfigurations())
            .Returns(Result<IReadOnlyCollection<McpServerConfig>>.Success(configs));
    }

    /// <summary>
    /// Sets up MCP resolver to return failure for error behavior testing
    /// </summary>
    public static void SetupMcpResolverFailure(
        Mock<IMcpServerResolver> resolverMock,
        Error error)
    {
        resolverMock.Setup(x => x.GetEnabledServerConfigurations())
            .Returns(Result<IReadOnlyCollection<McpServerConfig>>.Failure(error));
    }

    /// <summary>
    /// Creates a behavior verification scenario for command processing
    /// </summary>
    public static CommandBehaviorScenario<TCommand, TResponse> CreateCommandScenario<TCommand, TResponse>()
        where TCommand : class, IRequest<Result<TResponse>>
    {
        return new CommandBehaviorScenario<TCommand, TResponse>();
    }

    /// <summary>
    /// Helper method to compare commands for verification
    /// </summary>
    private static bool CommandMatches<TCommand>(TCommand actual, TCommand expected)
        where TCommand : class
    {
        // Deep comparison logic would be implemented here
        // For now, we'll use reference equality
        return ReferenceEquals(actual, expected) || actual.Equals(expected);
    }
}

/// <summary>
/// Behavior scenario builder for command testing
/// </summary>
public class CommandBehaviorScenario<TCommand, TResponse>
    where TCommand : class, IRequest<Result<TResponse>>
{
    private readonly Mock<IAiClient> _aiClientMock;
    private readonly Mock<IMcpServerResolver> _mcpResolverMock;
    private readonly Mock<ILogger> _loggerMock;

    public CommandBehaviorScenario()
    {
        _aiClientMock = CqrsContractMocks.CreateAiClientMock();
        _mcpResolverMock = CqrsContractMocks.CreateMcpServerResolverMock();
        _loggerMock = new Mock<ILogger>(MockBehavior.Loose);
    }

    public Mock<IAiClient> AiClientMock => _aiClientMock;
    public Mock<IMcpServerResolver> McpResolverMock => _mcpResolverMock;
    public Mock<ILogger> LoggerMock => _loggerMock;

    /// <summary>
    /// Sets up successful AI processing behavior
    /// </summary>
    public CommandBehaviorScenario<TCommand, TResponse> WithSuccessfulAiProcessing(AiResponse response)
    {
        CqrsContractMocks.SetupAiClientBehavior(_aiClientMock, response);
        return this;
    }

    /// <summary>
    /// Sets up AI processing failure behavior
    /// </summary>
    public CommandBehaviorScenario<TCommand, TResponse> WithAiProcessingFailure(Error error)
    {
        CqrsContractMocks.SetupAiClientFailure(_aiClientMock, error);
        return this;
    }

    /// <summary>
    /// Sets up MCP server configuration behavior
    /// </summary>
    public CommandBehaviorScenario<TCommand, TResponse> WithMcpConfiguration(IReadOnlyCollection<McpServerConfig> configs)
    {
        CqrsContractMocks.SetupMcpResolverBehavior(_mcpResolverMock, configs);
        return this;
    }

    /// <summary>
    /// Sets up MCP resolver failure behavior
    /// </summary>
    public CommandBehaviorScenario<TCommand, TResponse> WithMcpFailure(Error error)
    {
        CqrsContractMocks.SetupMcpResolverFailure(_mcpResolverMock, error);
        return this;
    }

    /// <summary>
    /// Executes the behavior test and verifies all interactions
    /// </summary>
    public async Task<Result<TResponse>> ExecuteAsync<THandler>(THandler handler, TCommand command)
    {
        var result = await handler.Handle(command, CancellationToken.None);
        
        // Verify all mocks were called as expected
        _aiClientMock.VerifyAll();
        _mcpResolverMock.VerifyAll();
        
        return result;
    }

    /// <summary>
    /// Verifies specific interaction patterns occurred
    /// </summary>
    public void VerifyInteractions(Action<CommandBehaviorScenario<TCommand, TResponse>> verificationAction)
    {
        verificationAction(this);
    }
}