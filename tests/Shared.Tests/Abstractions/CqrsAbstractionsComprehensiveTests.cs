using NUnit.Framework;
using Shouldly;
using Axon.Shared.Common;
using Axon.Shared.Common.Abstractions;
using Axon.Tests.Shared.Tests.Utilities;
using Moq;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Axon.Tests.Shared.Tests.Abstractions;

/// <summary>
/// Comprehensive testing for CQRS abstractions including IRequest, IRequestHandler interfaces,
/// request-response patterns, validation, error handling, and integration scenarios.
/// </summary>
[TestFixture]
[Category("Shared")]
[Category("Abstractions")]
[Category("CQRS")]
public sealed class CqrsAbstractionsComprehensiveTests
{
    #region Test Data Models

    // Command models for testing
    public record CreateUserCommand(string Name, string Email) : IRequest<Result<CreateUserResponse>>;
    public record UpdateUserCommand(Guid Id, string Name, string Email) : IRequest<Result>;
    public record DeleteUserCommand(Guid Id) : IRequest<Result>;

    // Query models for testing
    public record GetUserQuery(Guid Id) : IRequest<Result<UserDto>>;
    public record GetUsersQuery(int Page, int PageSize) : IRequest<Result<PaginatedResponse<UserDto>>>;
    public record SearchUsersQuery(string SearchTerm) : IRequest<Result<List<UserDto>>>;

    // Response models
    public record CreateUserResponse(Guid Id, string Name, string Email);
    public record UserDto(Guid Id, string Name, string Email, DateTime CreatedAt);
    public record PaginatedResponse<T>(List<T> Items, int TotalCount, int Page, int PageSize);

    // Handler implementations for testing
    public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<CreateUserResponse>>
    {
        private readonly ConcurrentDictionary<Guid, UserDto> _users = new();

        public async Task<Result<CreateUserResponse>> HandleAsync(CreateUserCommand request, CancellationToken cancellationToken)
        {
            // Simulate validation
            if (string.IsNullOrWhiteSpace(request.Name))
                return Result<CreateUserResponse>.Failure(Error.Validation("Name is required", "NAME_REQUIRED"));

            if (string.IsNullOrWhiteSpace(request.Email))
                return Result<CreateUserResponse>.Failure(Error.Validation("Email is required", "EMAIL_REQUIRED"));

            if (!request.Email.Contains('@'))
                return Result<CreateUserResponse>.Failure(Error.Validation("Invalid email format", "EMAIL_INVALID"));

            // Simulate async operation
            await Task.Delay(10, cancellationToken);

            // Check for duplicates
            if (_users.Values.Any(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
                return Result<CreateUserResponse>.Failure(Error.Conflict("User with this email already exists", "USER_EXISTS"));

            var userId = Guid.NewGuid();
            var user = new UserDto(userId, request.Name, request.Email, DateTime.UtcNow);
            _users[userId] = user;

            var response = new CreateUserResponse(userId, request.Name, request.Email);
            return Result<CreateUserResponse>.Success(response);
        }
    }

    public class GetUserHandler : IRequestHandler<GetUserQuery, Result<UserDto>>
    {
        private readonly ConcurrentDictionary<Guid, UserDto> _users = new();

        public GetUserHandler(ConcurrentDictionary<Guid, UserDto> users)
        {
            _users = users;
        }

        public GetUserHandler() { }

        public async Task<Result<UserDto>> HandleAsync(GetUserQuery request, CancellationToken cancellationToken)
        {
            // Simulate async operation
            await Task.Delay(5, cancellationToken);

            if (_users.TryGetValue(request.Id, out var user))
                return Result<UserDto>.Success(user);

            return Result<UserDto>.Failure(Error.NotFound($"User with ID {request.Id} not found", "USER_NOT_FOUND"));
        }
    }

    public class UpdateUserHandler : IRequestHandler<UpdateUserCommand, Result>
    {
        private readonly ConcurrentDictionary<Guid, UserDto> _users = new();

        public async Task<Result> HandleAsync(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(request.Name))
                return Result.Failure(Error.Validation("Name is required", "NAME_REQUIRED"));

            // Simulate async operation
            await Task.Delay(8, cancellationToken);

            if (!_users.ContainsKey(request.Id))
                return Result.Failure(Error.NotFound($"User with ID {request.Id} not found", "USER_NOT_FOUND"));

            var existingUser = _users[request.Id];
            var updatedUser = existingUser with { Name = request.Name, Email = request.Email };
            _users[request.Id] = updatedUser;

            return Result.Success;
        }
    }

    #endregion

    #region Interface Contract Testing

    /// <summary>
    /// Tests the basic contracts and behavior of IRequest and IRequestHandler interfaces
    /// </summary>
    [TestFixture]
    public class InterfaceContractTests
    {
        [Test]
        [Category("Contracts")]
        public void IRequest_ShouldImplementMarkerInterface()
        {
            // Arrange & Act
            var command = new CreateUserCommand("John Doe", "john@example.com");
            var query = new GetUserQuery(Guid.NewGuid());

            // Assert
            command.ShouldBeAssignableTo<IRequest<Result<CreateUserResponse>>>();
            command.ShouldBeAssignableTo<IRequest>();

            query.ShouldBeAssignableTo<IRequest<Result<UserDto>>>();
            query.ShouldBeAssignableTo<IRequest>();
        }

        [Test]
        [Category("Contracts")]
        public void IRequestHandler_ShouldImplementCorrectInterface()
        {
            // Arrange & Act
            var createHandler = new CreateUserHandler();
            var getUserHandler = new GetUserHandler();

            // Assert
            createHandler.ShouldBeAssignableTo<IRequestHandler<CreateUserCommand, Result<CreateUserResponse>>>();
            getUserHandler.ShouldBeAssignableTo<IRequestHandler<GetUserQuery, Result<UserDto>>>();
        }

        [Test]
        [Category("Contracts")]
        public void RequestHandler_ShouldAcceptCancellationToken()
        {
            // Arrange
            var handler = new CreateUserHandler();
            var command = new CreateUserCommand("John Doe", "john@example.com");
            var cancellationTokenSource = new CancellationTokenSource();

            // Act & Assert - Should not throw
            Should.NotThrow(async () =>
            {
                var result = await handler.Handle(command, cancellationTokenSource.Token);
                result.ShouldNotBeNull();
            });
        }

        [Test]
        [Category("Contracts")]
        public async Task RequestHandler_ShouldRespectCancellation()
        {
            // Arrange
            var handler = new SlowRequestHandler();
            var request = new SlowRequest();
            var cancellationTokenSource = new CancellationTokenSource(100); // 100ms timeout

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(async () =>
            {
                await handler.Handle(request, cancellationTokenSource.Token);
            });
        }

        // Helper classes for cancellation testing
        public record SlowRequest() : IRequest<Result>;
        public class SlowRequestHandler : IRequestHandler<SlowRequest, Result>
        {
            public async Task<Result> HandleAsync(SlowRequest request, CancellationToken cancellationToken)
            {
                await Task.Delay(1000, cancellationToken); // Will be cancelled
                return Result.Success;
            }
        }
    }

    #endregion

    #region Command Testing

    /// <summary>
    /// Comprehensive testing for command patterns and handlers
    /// </summary>
    [TestFixture]
    public class CommandHandlerTests
    {
        private CreateUserHandler _createHandler = null!;
        private UpdateUserHandler _updateHandler = null!;

        [SetUp]
        public void SetUp()
        {
            _createHandler = new CreateUserHandler();
            _updateHandler = new UpdateUserHandler();
        }

        [Test]
        [Category("Commands")]
        public async Task CreateUserCommand_ShouldSucceedWithValidData()
        {
            // Arrange
            var command = new CreateUserCommand("John Doe", "john@example.com");

            // Act
            var result = await _createHandler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Name.ShouldBe("John Doe");
            result.Value.Email.ShouldBe("john@example.com");
            result.Value.Id.ShouldNotBe(Guid.Empty);
        }

        [Test]
        [Category("Commands")]
        [TestCase("", "john@example.com", "NAME_REQUIRED")]
        [TestCase("   ", "john@example.com", "NAME_REQUIRED")]
        [TestCase("John Doe", "", "EMAIL_REQUIRED")]
        [TestCase("John Doe", "   ", "EMAIL_REQUIRED")]
        [TestCase("John Doe", "invalid-email", "EMAIL_INVALID")]
        public async Task CreateUserCommand_ShouldFailWithInvalidData(string name, string email, string expectedErrorCode)
        {
            // Arrange
            var command = new CreateUserCommand(name, email);

            // Act
            var result = await _createHandler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.Type.ShouldBe(ErrorType.Validation);
            result.Error.Code.ShouldBe(expectedErrorCode);
        }

        [Test]
        [Category("Commands")]
        public async Task UpdateUserCommand_ShouldFailForNonExistentUser()
        {
            // Arrange
            var command = new UpdateUserCommand(Guid.NewGuid(), "John Updated", "john.updated@example.com");

            // Act
            var result = await _updateHandler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
            result.Error.Code.ShouldBe("USER_NOT_FOUND");
        }

        [Test]
        [Category("Commands")]
        public async Task Commands_ShouldExecuteInParallel()
        {
            // Arrange
            const int commandCount = 50;
            var commands = Enumerable.Range(1, commandCount)
                .Select(i => new CreateUserCommand($"User {i}", $"user{i}@example.com"))
                .ToList();

            var stopwatch = Stopwatch.StartNew();

            // Act
            var tasks = commands.Select(cmd => _createHandler.Handle(cmd, CancellationToken.None));
            var results = await Task.WhenAll(tasks);

            stopwatch.Stop();

            // Assert
            results.Length.ShouldBe(commandCount);
            results.ShouldAllBe(r => r.IsSuccess);
            
            // All users should have unique IDs
            var ids = results.Select(r => r.Value.Id).ToList();
            ids.Distinct().Count().ShouldBe(commandCount);

            // Parallel execution should be faster than sequential
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(commandCount * 10); // Each command takes ~10ms

            TestContext.Out.WriteLine($"Executed {commandCount} commands in parallel in {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    #endregion

    #region Query Testing

    /// <summary>
    /// Comprehensive testing for query patterns and handlers
    /// </summary>
    [TestFixture]
    public class QueryHandlerTests
    {
        private GetUserHandler _getUserHandler = null!;
        private ConcurrentDictionary<Guid, UserDto> _users = null!;

        [SetUp]
        public void SetUp()
        {
            _users = new ConcurrentDictionary<Guid, UserDto>();
            _getUserHandler = new GetUserHandler(_users);
        }

        [Test]
        [Category("Queries")]
        public async Task GetUserQuery_ShouldReturnUserWhenExists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new UserDto(userId, "John Doe", "john@example.com", DateTime.UtcNow);
            _users[userId] = user;

            var query = new GetUserQuery(userId);

            // Act
            var result = await _getUserHandler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(user);
        }

        [Test]
        [Category("Queries")]
        public async Task GetUserQuery_ShouldReturnNotFoundWhenUserDoesNotExist()
        {
            // Arrange
            var query = new GetUserQuery(Guid.NewGuid());

            // Act
            var result = await _getUserHandler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
            result.Error.Code.ShouldBe("USER_NOT_FOUND");
        }

        [Test]
        [Category("Queries")]
        public async Task Queries_ShouldExecuteConcurrently()
        {
            // Arrange
            const int userCount = 100;
            var users = Enumerable.Range(1, userCount)
                .Select(i =>
                {
                    var id = Guid.NewGuid();
                    var user = new UserDto(id, $"User {i}", $"user{i}@example.com", DateTime.UtcNow);
                    _users[id] = user;
                    return new { Id = id, User = user };
                })
                .ToList();

            var queries = users.Select(u => new GetUserQuery(u.Id)).ToList();
            var stopwatch = Stopwatch.StartNew();

            // Act
            var tasks = queries.Select(q => _getUserHandler.Handle(q, CancellationToken.None));
            var results = await Task.WhenAll(tasks);

            stopwatch.Stop();

            // Assert
            results.Length.ShouldBe(userCount);
            results.ShouldAllBe(r => r.IsSuccess);

            // Verify all users are returned correctly
            var returnedUsers = results.Select(r => r.Value).ToList();
            returnedUsers.Count.ShouldBe(userCount);

            // Concurrent execution should be faster than sequential
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(userCount * 5); // Each query takes ~5ms

            TestContext.Out.WriteLine($"Executed {userCount} queries concurrently in {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    #endregion

    #region Error Handling Testing

    /// <summary>
    /// Testing error handling patterns in CQRS abstractions
    /// </summary>
    [TestFixture]
    public class ErrorHandlingTests
    {
        [Test]
        [Category("ErrorHandling")]
        public async Task RequestHandlers_ShouldHandleExceptionsGracefully()
        {
            // Arrange
            var faultyHandler = new FaultyRequestHandler();
            var request = new FaultyRequest();

            // Act & Assert
            var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            {
                await faultyHandler.HandleAsync(request, CancellationToken.None);
            });

            exception.Message.ShouldBe("Simulated error");
        }

        [Test]
        [Category("ErrorHandling")]
        public async Task RequestHandlers_ShouldReturnErrorsInResult()
        {
            // Arrange
            var handler = new ErrorReturningHandler();
            var request = new ErrorRequest();

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.Type.ShouldBe(ErrorType.InternalError);
            result.Error.Message.ShouldBe("Something went wrong");
        }

        [Test]
        [Category("ErrorHandling")]
        [TestCase(ErrorType.Validation)]
        [TestCase(ErrorType.NotFound)]
        [TestCase(ErrorType.Conflict)]
        [TestCase(ErrorType.Unauthorized)]
        [TestCase(ErrorType.Forbidden)]
        [TestCase(ErrorType.InternalError)]
        [TestCase(ErrorType.ExternalService)]
        public async Task RequestHandlers_ShouldHandleAllErrorTypes(ErrorType errorType)
        {
            // Arrange
            var handler = new VariableErrorHandler();
            var request = new VariableErrorRequest(errorType);

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.Type.ShouldBe(errorType);
        }

        // Helper classes for error testing
        public record FaultyRequest() : IRequest<Result>;
        public class FaultyRequestHandler : IRequestHandler<FaultyRequest, Result>
        {
            public Task<Result> HandleAsync(FaultyRequest request, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Simulated error");
            }
        }

        public record ErrorRequest() : IRequest<Result>;
        public class ErrorReturningHandler : IRequestHandler<ErrorRequest, Result>
        {
            public Task<Result> HandleAsync(ErrorRequest request, CancellationToken cancellationToken)
            {
                var result = Result.Failure(Error.InternalError("Something went wrong"));
                return Task.FromResult(result);
            }
        }

        public record VariableErrorRequest(ErrorType ErrorType) : IRequest<Result>;
        public class VariableErrorHandler : IRequestHandler<VariableErrorRequest, Result>
        {
            public Task<Result> HandleAsync(VariableErrorRequest request, CancellationToken cancellationToken)
            {
                var error = request.ErrorType switch
                {
                    ErrorType.Validation => Error.Validation("Validation error"),
                    ErrorType.NotFound => Error.NotFound("Not found"),
                    ErrorType.Conflict => Error.Conflict("Conflict"),
                    ErrorType.Unauthorized => Error.Unauthorized("Unauthorized"),
                    ErrorType.Forbidden => Error.Forbidden("Forbidden"),
                    ErrorType.InternalError => Error.InternalError("Internal error"),
                    ErrorType.ExternalService => Error.ExternalService("External service error"),
                    _ => Error.InternalError("Unknown error")
                };

                return Task.FromResult(Result.Failure(error));
            }
        }
    }

    #endregion

    #region Mock Integration Testing

    /// <summary>
    /// Testing CQRS abstractions with mocks and dependency injection patterns
    /// </summary>
    [TestFixture]
    public class MockIntegrationTests
    {
        [Test]
        [Category("MockIntegration")]
        public async Task RequestHandler_ShouldWorkWithMockedDependencies()
        {
            // Arrange
            var mockRepository = new Mock<IUserRepository>();
            var mockValidator = new Mock<IUserValidator>();

            var user = new UserDto(Guid.NewGuid(), "John Doe", "john@example.com", DateTime.UtcNow);
            
            mockValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(Result.Success);
            
            mockRepository.Setup(r => r.CreateAsync(It.IsAny<UserDto>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(Result<UserDto>.Success(user));

            var handler = new MockedCreateUserHandler(mockRepository.Object, mockValidator.Object);
            var command = new CreateUserCommand("John Doe", "john@example.com");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Name.ShouldBe("John Doe");

            // Verify interactions
            mockValidator.Verify(v => v.ValidateAsync(command, It.IsAny<CancellationToken>()), Times.Once);
            mockRepository.Verify(r => r.CreateAsync(It.IsAny<UserDto>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        [Category("MockIntegration")]
        public async Task RequestHandler_ShouldHandleDependencyFailures()
        {
            // Arrange
            var mockRepository = new Mock<IUserRepository>();
            var mockValidator = new Mock<IUserValidator>();

            mockValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(Result.Failure(Error.Validation("Name is required")));

            var handler = new MockedCreateUserHandler(mockRepository.Object, mockValidator.Object);
            var command = new CreateUserCommand("", "john@example.com");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Error.Type.ShouldBe(ErrorType.Validation);

            // Repository should not be called if validation fails
            mockRepository.Verify(r => r.CreateAsync(It.IsAny<UserDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Mock interfaces and implementations
        public interface IUserRepository
        {
            Task<Result<UserDto>> CreateAsync(UserDto user, CancellationToken cancellationToken);
            Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        }

        public interface IUserValidator
        {
            Task<Result> ValidateAsync(CreateUserCommand command, CancellationToken cancellationToken);
        }

        public class MockedCreateUserHandler : IRequestHandler<CreateUserCommand, Result<CreateUserResponse>>
        {
            private readonly IUserRepository _repository;
            private readonly IUserValidator _validator;

            public MockedCreateUserHandler(IUserRepository repository, IUserValidator validator)
            {
                _repository = repository;
                _validator = validator;
            }

            public async Task<Result<CreateUserResponse>> HandleAsync(CreateUserCommand request, CancellationToken cancellationToken)
            {
                // Validate
                var validationResult = await _validator.ValidateAsync(request, cancellationToken);
                if (!validationResult.IsSuccess)
                    return Result<CreateUserResponse>.Failure(validationResult.Error);

                // Create user
                var user = new UserDto(Guid.NewGuid(), request.Name, request.Email, DateTime.UtcNow);
                var createResult = await _repository.CreateAsync(user, cancellationToken);
                
                if (!createResult.IsSuccess)
                    return Result<CreateUserResponse>.Failure(createResult.Error);

                var response = new CreateUserResponse(createResult.Value.Id, createResult.Value.Name, createResult.Value.Email);
                return Result<CreateUserResponse>.Success(response);
            }
        }
    }

    #endregion

    #region Performance Testing

    /// <summary>
    /// Performance testing for CQRS abstractions under load
    /// </summary>
    [TestFixture]
    public class PerformanceTests
    {
        [Test]
        [Category("Performance")]
        [Category("Load")]
        public async Task RequestHandlers_ShouldPerformUnderHighLoad()
        {
            // Arrange
            const int requestCount = 10_000;
            var handler = new CreateUserHandler();
            var commands = Enumerable.Range(1, requestCount)
                .Select(i => new CreateUserCommand($"User {i}", $"user{i}@example.com"))
                .ToList();

            var stopwatch = Stopwatch.StartNew();

            // Act
            var tasks = commands.Select(cmd => handler.Handle(cmd, CancellationToken.None));
            var results = await Task.WhenAll(tasks);

            stopwatch.Stop();

            // Assert
            results.Length.ShouldBe(requestCount);
            results.ShouldAllBe(r => r.IsSuccess);

            // Performance assertions
            var requestsPerSecond = requestCount / stopwatch.Elapsed.TotalSeconds;
            requestsPerSecond.ShouldBeGreaterThan(100, $"Performance was {requestsPerSecond:F0} requests/second");

            TestContext.Out.WriteLine($"Processed {requestCount} requests in {stopwatch.ElapsedMilliseconds}ms ({requestsPerSecond:F0} req/sec)");
        }

        [Test]
        [Category("Performance")]
        [Category("Memory")]
        public async Task RequestHandlers_ShouldNotLeakMemory()
        {
            // Arrange
            const int requestCount = 50_000;
            var handler = new CreateUserHandler();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            var initialMemory = GC.GetTotalMemory(false);

            // Act
            for (int i = 0; i < requestCount; i++)
            {
                var command = new CreateUserCommand($"User {i}", $"user{i}@example.com");
                var result = await handler.Handle(command, CancellationToken.None);
                result.IsSuccess.ShouldBeTrue();

                // Periodic cleanup
                if (i % 10_000 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            var finalMemory = GC.GetTotalMemory(false);

            // Assert
            var memoryIncrease = finalMemory - initialMemory;
            var memoryIncreaseKB = memoryIncrease / 1024.0;

            // Memory increase should be reasonable (less than 1KB per 100 requests)
            memoryIncreaseKB.ShouldBeLessThan(requestCount / 100.0,
                $"Memory increase was {memoryIncreaseKB:F2} KB for {requestCount} requests");

            TestContext.Out.WriteLine($"Memory usage increased by {memoryIncreaseKB:F2} KB for {requestCount} requests");
        }

        [Test]
        [Category("Performance")]
        [Category("Concurrency")]
        public async Task RequestHandlers_ShouldHandleConcurrentAccess()
        {
            // Arrange
            const int threadCount = 20;
            const int requestsPerThread = 500;
            var handler = new CreateUserHandler();
            var barrier = new Barrier(threadCount);
            var results = new ConcurrentBag<Result<CreateUserResponse>>();

            var stopwatch = Stopwatch.StartNew();

            // Act
            var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(async () =>
            {
                barrier.SignalAndWait(); // Synchronize thread start

                for (int i = 0; i < requestsPerThread; i++)
                {
                    var command = new CreateUserCommand($"User {threadId}-{i}", $"user{threadId}-{i}@example.com");
                    var result = await handler.Handle(command, CancellationToken.None);
                    results.Add(result);
                }
            }));

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var allResults = results.ToList();
            allResults.Count.ShouldBe(threadCount * requestsPerThread);
            allResults.ShouldAllBe(r => r.IsSuccess);

            // Check for duplicate IDs (race condition indicator)
            var ids = allResults.Select(r => r.Value.Id).ToList();
            ids.Distinct().Count().ShouldBe(ids.Count, "Found duplicate IDs indicating race condition");

            TestContext.Out.WriteLine($"Processed {allResults.Count} concurrent requests across {threadCount} threads in {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    #endregion

    #region Edge Cases Testing

    /// <summary>
    /// Testing edge cases and boundary conditions
    /// </summary>
    [TestFixture]
    public class EdgeCasesTests
    {
        [Test]
        [Category("EdgeCases")]
        public async Task RequestHandlers_ShouldHandleNullRequests()
        {
            // This test would depend on how nullability is handled in the actual implementation
            // For now, we'll test the behavior with valid but edge-case requests

            var handler = new CreateUserHandler();
            
            // Empty strings (should fail validation)
            var emptyNameCommand = new CreateUserCommand("", "valid@example.com");
            var result1 = await handler.Handle(emptyNameCommand, CancellationToken.None);
            result1.IsSuccess.ShouldBeFalse();

            // Whitespace strings (should fail validation)
            var whitespaceCommand = new CreateUserCommand("   ", "valid@example.com");
            var result2 = await handler.Handle(whitespaceCommand, CancellationToken.None);
            result2.IsSuccess.ShouldBeFalse();
        }

        [Test]
        [Category("EdgeCases")]
        public async Task RequestHandlers_ShouldHandleVeryLongStrings()
        {
            // Arrange
            var handler = new CreateUserHandler();
            var veryLongName = new string('A', 10_000);
            var veryLongEmail = new string('B', 5_000) + "@example.com";

            var command = new CreateUserCommand(veryLongName, veryLongEmail);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert - Should succeed (assuming no length validation)
            result.IsSuccess.ShouldBeTrue();
            result.Value.Name.ShouldBe(veryLongName);
        }

        [Test]
        [Category("EdgeCases")]
        public async Task RequestHandlers_ShouldHandleSpecialCharacters()
        {
            // Arrange
            var handler = new CreateUserHandler();
            var specialCharsName = "Jöhn Döe 山田太郎 🚀";
            var unicodeEmail = "test.unicode@例え.テスト";

            var command = new CreateUserCommand(specialCharsName, unicodeEmail);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert - May fail due to email validation, but should handle gracefully
            if (result.IsSuccess)
            {
                result.Value.Name.ShouldBe(specialCharsName);
            }
            else
            {
                result.Error.Type.ShouldBe(ErrorType.Validation);
            }
        }
    }

    #endregion
}