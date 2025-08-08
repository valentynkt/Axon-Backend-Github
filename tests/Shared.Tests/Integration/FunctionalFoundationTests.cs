using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FluentValidation;
using MediatR;
using NSubstitute;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Exceptions;
using BuildingBlocks.Core.Functional.Railway;
using BuildingBlocks.Core.Functional.Async;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Web.Middleware;
using BuildingBlocks.Core.Diagnostics;

namespace Axon.Tests.Shared.Tests.Integration;

/// <summary>
/// Comprehensive integration tests for the functional foundation (Epic 1).
/// Tests end-to-end scenarios across all Result pattern components.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public sealed class FunctionalFoundationTests
{
    #region Test Models

    public record TestCommand(string Data) : ICommand<string>
    {
        public Guid RequestId { get; } = Guid.NewGuid();
        public DateTime RequestedAt { get; } = DateTime.UtcNow;
    }

    public record TestQuery(string Query) : IQuery<string>
    {
        public Guid RequestId { get; } = Guid.NewGuid();
        public DateTime RequestedAt { get; } = DateTime.UtcNow;
    }

    public class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator()
        {
            RuleFor(x => x.Data)
                .NotEmpty()
                .WithMessage("Data cannot be empty");
        }
    }

    public class TestCommandHandler : IRequestHandler<TestCommand, Result<string>>
    {
        public Task<Result<string>> Handle(TestCommand request, CancellationToken cancellationToken)
        {
            if (request.Data == "fail")
                return Task.FromResult(Result<string>.Failure(Error.BusinessRule("Handler failure")));

            return Task.FromResult(Result<string>.Success($"Processed: {request.Data}"));
        }
    }

    #endregion

    #region Result-Aware MediatR Behaviors Tests

    [TestFixture]
    public class ResultValidationBehaviorTests
    {
        private IServiceProvider _serviceProvider;
        private ILogger<ResultValidationBehavior<TestCommand, Result<string>>> _logger;
        private ResultValidationBehavior<TestCommand, Result<string>> _behavior;

        [SetUp]
        public void Setup()
        {
            var services = new ServiceCollection();
            services.AddScoped<IValidator<TestCommand>, TestCommandValidator>();
            _serviceProvider = services.BuildServiceProvider();
            
            _logger = Substitute.For<ILogger<ResultValidationBehavior<TestCommand, Result<string>>>>();
            _behavior = new ResultValidationBehavior<TestCommand, Result<string>>(_serviceProvider, _logger);
        }

        [Test]
        public async Task Handle_WithValidRequest_ShouldProceedToHandler()
        {
            // Arrange
            var command = new TestCommand("valid data");
            var handlerCalled = false;
            
            RequestHandlerDelegate<Result<string>> next = () =>
            {
                handlerCalled = true;
                return Task.FromResult(Result<string>.Success("success"));
            };

            // Act
            var result = await _behavior.Handle(command, next, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            handlerCalled.ShouldBeTrue();
        }

        [Test]
        public async Task Handle_WithInvalidRequest_ShouldReturnValidationFailure()
        {
            // Arrange
            var command = new TestCommand(""); // Invalid - empty data
            var handlerCalled = false;
            
            RequestHandlerDelegate<Result<string>> next = () =>
            {
                handlerCalled = true;
                return Task.FromResult(Result<string>.Success("success"));
            };

            // Act
            var result = await _behavior.Handle(command, next, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
            result.Error.Message.ShouldBe("Data cannot be empty");
            handlerCalled.ShouldBeFalse();
        }

        [Test]
        public async Task Handle_WithNoValidator_ShouldProceedToHandler()
        {
            // Arrange
            var emptyServices = new ServiceCollection().BuildServiceProvider();
            var behaviorWithoutValidator = new ResultValidationBehavior<TestCommand, Result<string>>(emptyServices, _logger);
            
            var command = new TestCommand("");
            var handlerCalled = false;
            
            RequestHandlerDelegate<Result<string>> next = () =>
            {
                handlerCalled = true;
                return Task.FromResult(Result<string>.Success("success"));
            };

            // Act
            var result = await behaviorWithoutValidator.Handle(command, next, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            handlerCalled.ShouldBeTrue();
        }
    }

    [TestFixture]
    public class ResultLoggingBehaviorTests
    {
        private ILogger<ResultLoggingBehavior<TestCommand, Result<string>>> _logger;
        private ResultLoggingBehavior<TestCommand, Result<string>> _behavior;

        [SetUp]
        public void Setup()
        {
            _logger = Substitute.For<ILogger<ResultLoggingBehavior<TestCommand, Result<string>>>>();
            _behavior = new ResultLoggingBehavior<TestCommand, Result<string>>(_logger);
        }

        [Test]
        public async Task Handle_WithSuccessfulResult_ShouldLogSuccess()
        {
            // Arrange
            var command = new TestCommand("test");
            RequestHandlerDelegate<Result<string>> next = () =>
                Task.FromResult(Result<string>.Success("success"));

            // Act
            var result = await _behavior.Handle(command, next, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            
            // Verify logging calls were made
            _logger.Received().LogInformation(
                Arg.Is<string>(s => s.Contains("Starting request")),
                Arg.Any<object[]>());
            
            _logger.Received().LogInformation(
                Arg.Is<string>(s => s.Contains("completed successfully")),
                Arg.Any<object[]>());
        }

        [Test]
        public async Task Handle_WithFailedResult_ShouldLogFailure()
        {
            // Arrange
            var command = new TestCommand("test");
            var error = Error.BusinessRule("Test failure");
            RequestHandlerDelegate<Result<string>> next = () =>
                Task.FromResult(Result<string>.Failure(error));

            // Act
            var result = await _behavior.Handle(command, next, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            
            // Verify failure was logged
            _logger.Received().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("failed")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Test]
        public async Task Handle_WithException_ShouldLogError()
        {
            // Arrange
            var command = new TestCommand("test");
            var expectedException = new InvalidOperationException("Test exception");
            
            RequestHandlerDelegate<Result<string>> next = () =>
                throw expectedException;

            // Act & Assert
            var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await _behavior.Handle(command, next, CancellationToken.None));
            
            exception.ShouldBe(expectedException);
            
            // Verify error was logged
            _logger.Received().LogError(
                expectedException,
                Arg.Is<string>(s => s.Contains("failed with exception")),
                Arg.Any<object[]>());
        }
    }

    #endregion

    #region Exception Bridge Pattern Tests

    [TestFixture]
    public class ExceptionBridgeTests
    {
        [Test]
        public void ToException_WithSuccessResult_ShouldReturnNull()
        {
            // Arrange
            var result = Result<string>.Success("test");

            // Act
            var exception = result.ToException();

            // Assert
            exception.ShouldBeNull();
        }

        [Test]
        public void ToException_WithFailedResult_ShouldReturnAppropriateException()
        {
            // Arrange
            var error = Error.NotFound("Resource not found");
            var result = Result<string>.Failure(error);

            // Act
            var exception = result.ToException();

            // Assert
            exception.ShouldNotBeNull();
            exception.ShouldBeOfType<NotFoundException>();
            exception.Message.ShouldBe("Resource not found");
        }

        [Test]
        public void ToResult_WithException_ShouldReturnFailedResult()
        {
            // Arrange
            var exception = new ArgumentException("Invalid argument");

            // Act
            var result = exception.ToResult<string>();

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
            result.Error.Message.ShouldBe("Invalid argument");
        }

        [Test]
        public async Task TryExecuteAsync_WithSuccessfulOperation_ShouldReturnSuccess()
        {
            // Arrange
            Func<CancellationToken, Task<string>> operation = _ => Task.FromResult("success");

            // Act
            var result = await ResultExceptionMapper.TryExecuteAsync(operation);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe("success");
        }

        [Test]
        public async Task TryExecuteAsync_WithFailingOperation_ShouldReturnFailure()
        {
            // Arrange
            Func<CancellationToken, Task<string>> operation = _ => 
                throw new InvalidOperationException("Operation failed");

            // Act
            var result = await ResultExceptionMapper.TryExecuteAsync(operation);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Message.ShouldBe("Operation failed");
        }

        [Test]
        public async Task TryExecuteAsync_WithCancellation_ShouldReturnCancelledResult()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();
            
            Func<CancellationToken, Task<string>> operation = async ct =>
            {
                await Task.Delay(1000, ct);
                return "success";
            };

            // Act
            var result = await ResultExceptionMapper.TryExecuteAsync(operation, cts.Token);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Cancellation);
        }
    }

    [TestFixture]
    public class ResultExceptionMiddlewareTests
    {
        private ILogger<ResultExceptionMiddleware> _logger;
        private ResultExceptionMiddleware _middleware;

        [SetUp]
        public void Setup()
        {
            _logger = Substitute.For<ILogger<ResultExceptionMiddleware>>();
            RequestDelegate next = _ => throw new NotFoundException("Test not found");
            _middleware = new ResultExceptionMiddleware(next, _logger);
        }

        [Test]
        public async Task InvokeAsync_WithNotFoundException_ShouldReturn404()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            context.Response.StatusCode.ShouldBe(404);
            context.Response.ContentType.ShouldBe("application/problem+json");
        }

        [Test]
        public async Task InvokeAsync_WithValidationException_ShouldReturn400()
        {
            // Arrange
            RequestDelegate next = _ => throw new ValidationException("Validation failed");
            var middleware = new ResultExceptionMiddleware(next, _logger);
            
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            context.Response.StatusCode.ShouldBe(400);
        }

        [Test]
        public async Task InvokeAsync_WithGenericException_ShouldReturn500()
        {
            // Arrange
            RequestDelegate next = _ => throw new InvalidOperationException("Generic error");
            var middleware = new ResultExceptionMiddleware(next, _logger);
            
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            context.Response.StatusCode.ShouldBe(500);
        }
    }

    #endregion

    #region Railway Programming Extensions Tests

    [TestFixture]
    public class RailwayExtensionsTests
    {
        [Test]
        public void Sequence_WithAllSuccessResults_ShouldReturnSuccessWithAllValues()
        {
            // Arrange
            var results = new[]
            {
                Result<int>.Success(1),
                Result<int>.Success(2),
                Result<int>.Success(3)
            };

            // Act
            var combined = results.Sequence();

            // Assert
            combined.IsSuccess.ShouldBeTrue();
            combined.Value.ShouldBe(new[] { 1, 2, 3 });
        }

        [Test]
        public void Sequence_WithOneFailure_ShouldReturnFirstFailure()
        {
            // Arrange
            var error = Error.Validation("Validation error");
            var results = new[]
            {
                Result<int>.Success(1),
                Result<int>.Failure(error),
                Result<int>.Success(3)
            };

            // Act
            var combined = results.Sequence();

            // Assert
            combined.IsFailure.ShouldBeTrue();
            combined.Error.ShouldBe(error);
        }

        [Test]
        public void SequenceWithAllErrors_WithMultipleFailures_ShouldAggregateErrors()
        {
            // Arrange
            var error1 = Error.Validation("Error 1");
            var error2 = Error.Validation("Error 2");
            var results = new[]
            {
                Result<int>.Failure(error1),
                Result<int>.Success(2),
                Result<int>.Failure(error2)
            };

            // Act
            var combined = results.SequenceWithAllErrors();

            // Assert
            combined.IsFailure.ShouldBeTrue();
            combined.Error.Type.ShouldBe(ErrorType.Aggregate);
            combined.Error.Message.ShouldContain("Error 1");
            combined.Error.Message.ShouldContain("Error 2");
        }

        [Test]
        public async Task ParallelSequence_WithSuccessfulOperations_ShouldReturnAllResults()
        {
            // Arrange
            var operations = new[]
            {
                (Func<CancellationToken, Task<Result<int>>>)(_ => Task.FromResult(Result<int>.Success(1))),
                (Func<CancellationToken, Task<Result<int>>>)(_ => Task.FromResult(Result<int>.Success(2))),
                (Func<CancellationToken, Task<Result<int>>>)(_ => Task.FromResult(Result<int>.Success(3)))
            };

            // Act
            var result = await operations.ParallelSequence();

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Count.ShouldBe(3);
            result.Value.ShouldContain(1);
            result.Value.ShouldContain(2);
            result.Value.ShouldContain(3);
        }

        [Test]
        public async Task RetryAsync_WithTransientFailureThenSuccess_ShouldEventuallySucceed()
        {
            // Arrange
            var attempts = 0;
            Func<CancellationToken, Task<Result<string>>> operation = _ =>
            {
                attempts++;
                return attempts < 3 
                    ? Task.FromResult(Result<string>.Failure(Error.System("Transient error")))
                    : Task.FromResult(Result<string>.Success("Success"));
            };

            // Act
            var result = await RailwayExtensions.RetryAsync(operation, maxAttempts: 3);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe("Success");
            attempts.ShouldBe(3);
        }

        [Test]
        public void Pipeline_WithMultipleOperations_ShouldChainCorrectly()
        {
            // Arrange
            var initialResult = Result<int>.Success(5);
            
            Func<int, Result<int>> multiply2 = x => Result<int>.Success(x * 2);
            Func<int, Result<int>> add3 = x => Result<int>.Success(x + 3);
            Func<int, Result<int>> validatePositive = x => 
                x > 0 ? Result<int>.Success(x) : Result<int>.Failure(Error.Validation("Must be positive"));

            // Act
            var result = initialResult.Pipeline(multiply2, add3, validatePositive);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(13); // (5 * 2) + 3 = 13
        }

        [Test]
        public void Pipeline_WithFailingOperation_ShouldShortCircuit()
        {
            // Arrange
            var initialResult = Result<int>.Success(-5);
            
            Func<int, Result<int>> validatePositive = x => 
                x > 0 ? Result<int>.Success(x) : Result<int>.Failure(Error.Validation("Must be positive"));
            Func<int, Result<int>> multiply2 = x => Result<int>.Success(x * 2);

            // Act
            var result = initialResult.Pipeline(validatePositive, multiply2);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Message.ShouldBe("Must be positive");
        }
    }

    #endregion

    #region Async Result Extensions Tests

    [TestFixture]
    public class AsyncResultExtensionsTests
    {
        [Test]
        public async Task MapAsync_WithSuccessfulTaskResult_ShouldMapValue()
        {
            // Arrange
            var taskResult = Task.FromResult(Result<int>.Success(42));

            // Act
            var mapped = await taskResult.MapAsync(x => x.ToString());

            // Assert
            mapped.IsSuccess.ShouldBeTrue();
            mapped.Value.ShouldBe("42");
        }

        [Test]
        public async Task MapAsync_WithFailedTaskResult_ShouldPreserveError()
        {
            // Arrange
            var error = Error.Validation("Test error");
            var taskResult = Task.FromResult(Result<int>.Failure(error));

            // Act
            var mapped = await taskResult.MapAsync(x => x.ToString());

            // Assert
            mapped.IsFailure.ShouldBeTrue();
            mapped.Error.ShouldBe(error);
        }

        [Test]
        public async Task BindAsync_WithSuccessfulTaskResult_ShouldBind()
        {
            // Arrange
            var taskResult = Task.FromResult(Result<string>.Success("test"));

            // Act
            var bound = await taskResult.BindAsync(x => 
                Result<int>.Success(x.Length));

            // Assert
            bound.IsSuccess.ShouldBeTrue();
            bound.Value.ShouldBe(4);
        }

        [Test]
        public async Task TapAsync_WithSuccessfulResult_ShouldExecuteAction()
        {
            // Arrange
            var taskResult = Task.FromResult(Result<string>.Success("test"));
            var sideEffectExecuted = false;

            // Act
            var result = await taskResult.TapAsync(x => sideEffectExecuted = true);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe("test");
            sideEffectExecuted.ShouldBeTrue();
        }

        [Test]
        public async Task ToResultsAsync_WithAsyncEnumerable_ShouldWrapInResults()
        {
            // Arrange
            var items = new[] { 1, 2, 3 };
            var asyncEnumerable = items.ToAsyncEnumerable();

            // Act
            var results = new List<Result<int>>();
            await foreach (var result in asyncEnumerable.ToResultsAsync())
            {
                results.Add(result);
            }

            // Assert
            results.Count.ShouldBe(3);
            results.All(r => r.IsSuccess).ShouldBeTrue();
            results.Select(r => r.Value).ShouldBe(new[] { 1, 2, 3 });
        }

        [Test]
        public async Task BatchAsync_WithAsyncEnumerable_ShouldCreateBatches()
        {
            // Arrange
            var items = Enumerable.Range(1, 7);
            var asyncEnumerable = items.ToAsyncEnumerable();
            const int batchSize = 3;

            // Act
            var batches = new List<IReadOnlyList<int>>();
            await foreach (var batch in asyncEnumerable.BatchAsync(batchSize))
            {
                batches.Add(batch);
            }

            // Assert
            batches.Count.ShouldBe(3); // 3 batches: [1,2,3], [4,5,6], [7]
            batches[0].Count.ShouldBe(3);
            batches[1].Count.ShouldBe(3);
            batches[2].Count.ShouldBe(1);
            batches.SelectMany(b => b).ShouldBe(items);
        }
    }

    #endregion

    #region End-to-End Integration Tests

    [TestFixture]
    public class EndToEndIntegrationTests
    {
        private ServiceProvider _serviceProvider;

        [SetUp]
        public void Setup()
        {
            var services = new ServiceCollection();
            
            // Register MediatR
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(FunctionalFoundationTests).Assembly));
            
            // Register validators
            services.AddScoped<IValidator<TestCommand>, TestCommandValidator>();
            
            // Register behaviors
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ResultValidationBehavior<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ResultLoggingBehavior<,>));
            
            // Register handlers
            services.AddScoped<IRequestHandler<TestCommand, Result<string>>, TestCommandHandler>();
            
            // Add logging
            services.AddLogging();
            
            _serviceProvider = services.BuildServiceProvider();
        }

        [TearDown]
        public void TearDown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public async Task MediatR_WithValidCommand_ShouldProcessSuccessfully()
        {
            // Arrange
            var mediator = _serviceProvider.GetRequiredService<IMediator>();
            var command = new TestCommand("valid data");

            // Act
            var result = await mediator.Send(command);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe("Processed: valid data");
        }

        [Test]
        public async Task MediatR_WithInvalidCommand_ShouldFailValidation()
        {
            // Arrange
            var mediator = _serviceProvider.GetRequiredService<IMediator>();
            var command = new TestCommand(""); // Invalid

            // Act
            var result = await mediator.Send(command);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
            result.Error.Message.ShouldBe("Data cannot be empty");
        }

        [Test]
        public async Task MediatR_WithHandlerFailure_ShouldReturnBusinessRuleError()
        {
            // Arrange
            var mediator = _serviceProvider.GetRequiredService<IMediator>();
            var command = new TestCommand("fail"); // Triggers handler failure

            // Act
            var result = await mediator.Send(command);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.BusinessRule);
            result.Error.Message.ShouldBe("Handler failure");
        }

        [Test]
        public async Task ComplexWorkflow_WithRailwayPattern_ShouldWorkEndToEnd()
        {
            // Arrange
            var mediator = _serviceProvider.GetRequiredService<IMediator>();
            
            var commands = new[]
            {
                new TestCommand("data1"),
                new TestCommand("data2"),
                new TestCommand("data3")
            };

            // Act - Process commands using railway pattern
            var operations = commands.Select(cmd => 
                (Func<CancellationToken, Task<Result<string>>>)(ct => mediator.Send(cmd, ct)));

            var result = await operations.ParallelSequence();

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Count.ShouldBe(3);
            result.Value.ShouldContain("Processed: data1");
            result.Value.ShouldContain("Processed: data2");
            result.Value.ShouldContain("Processed: data3");
        }

        [Test]
        public async Task ExceptionToResult_RoundTrip_ShouldMaintainErrorInformation()
        {
            // Arrange
            var originalError = Error.NotFound("Original resource not found", "RESOURCE_404");
            var result = Result<string>.Failure(originalError);

            // Act - Convert to exception and back to result
            var exception = result.ToException();
            exception.ShouldNotBeNull();
            
            var roundTripResult = exception!.ToResult<string>();

            // Assert - Error information should be preserved
            roundTripResult.IsFailure.ShouldBeTrue();
            roundTripResult.Error.Type.ShouldBe(ErrorType.NotFound);
            roundTripResult.Error.Message.ShouldBe("Original resource not found");
        }
    }

    #endregion
}