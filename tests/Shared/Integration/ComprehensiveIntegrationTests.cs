using Axon.Shared.Common;
using Axon.Shared.Common.Abstractions;
// // using Axon.Tests.Shared.Tests.Extensions; // Removed to prevent circular dependency // Removed to fix circular dependency
using Axon.Tests.Shared.Builders;
using Axon.Tests.Shared.Generators;

namespace Axon.Tests.Shared.Integration;

/// <summary>
/// Comprehensive integration tests for cross-module shared component usage
/// Tests how Result, Error, and abstractions work together across module boundaries
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public sealed class ComprehensiveIntegrationTests
{
    [TestFixture]
    public class ResultWithAbstractionsIntegrationTests
    {
        // Sample request/response types that use Result<T>
        private sealed record GetUserRequest(int UserId) : IRequest<Result<UserDto>>;
        private sealed record CreateUserRequest(string Name, string Email) : IRequest<Result<UserDto>>;
        private sealed record UpdateUserRequest(int UserId, string Name, string Email) : IRequest<Result<UserDto>>;
        private sealed record DeleteUserRequest(int UserId) : IRequest<Result>;

        // Sample handlers that demonstrate Result<T> integration
        private sealed class GetUserHandler : IRequestHandler<GetUserRequest, Result<UserDto>>
        {
            private static readonly Dictionary<int, UserDto> Users = new()
            {
                { 1, new UserDto { Id = 1, Name = "John Doe", Email = "john@example.com" } },
                { 2, new UserDto { Id = 2, Name = "Jane Smith", Email = "jane@example.com" } }
            };

            public Task<Result<UserDto>> HandleAsync(GetUserRequest request, CancellationToken cancellationToken)
            {
                if (Users.TryGetValue(request.UserId, out var user))
                {
                    return Task.FromResult(Result<UserDto>.Success(user));
                }

                return Task.FromResult(Result<UserDto>.Failure(
                    Error.NotFound($"User with ID {request.UserId} not found", "USER_NOT_FOUND")));
            }
        }

        private sealed class CreateUserHandler : IRequestHandler<CreateUserRequest, Result<UserDto>>
        {
            public Task<Result<UserDto>> HandleAsync(CreateUserRequest request, CancellationToken cancellationToken)
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Task.FromResult(Result<UserDto>.Failure(
                        Error.Validation("Name is required", "NAME_REQUIRED")));
                }

                if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
                {
                    return Task.FromResult(Result<UserDto>.Failure(
                        Error.Validation("Valid email is required", "EMAIL_INVALID")));
                }

                // Simulate successful creation
                var user = new UserDto
                {
                    Id = Random.Shared.Next(1000, 9999),
                    Name = request.Name,
                    Email = request.Email
                };

                return Task.FromResult(Result<UserDto>.Success(user));
            }
        }

        private sealed class UpdateUserHandler : IRequestHandler<UpdateUserRequest, Result<UserDto>>
        {
            public Task<Result<UserDto>> HandleAsync(UpdateUserRequest request, CancellationToken cancellationToken)
            {
                // Simulate business logic validation
                if (request.UserId <= 0)
                {
                    return Task.FromResult(Result<UserDto>.Failure(
                        Error.Validation("User ID must be positive", "INVALID_USER_ID")));
                }

                // Simulate user not found
                if (request.UserId == 999)
                {
                    return Task.FromResult(Result<UserDto>.Failure(
                        Error.NotFound($"User with ID {request.UserId} not found", "USER_NOT_FOUND")));
                }

                // Simulate conflict (user email already exists)
                if (request.Email == "conflict@example.com")
                {
                    return Task.FromResult(Result<UserDto>.Failure(
                        Error.Conflict("Email already exists", "EMAIL_CONFLICT")));
                }

                // Simulate successful update
                var updatedUser = new UserDto
                {
                    Id = request.UserId,
                    Name = request.Name,
                    Email = request.Email
                };

                return Task.FromResult(Result<UserDto>.Success(updatedUser));
            }
        }

        private sealed class DeleteUserHandler : IRequestHandler<DeleteUserRequest, Result>
        {
            public Task<Result> HandleAsync(DeleteUserRequest request, CancellationToken cancellationToken)
            {
                if (request.UserId <= 0)
                {
                    return Task.FromResult(Result.Failure(
                        Error.Validation("User ID must be positive", "INVALID_USER_ID")));
                }

                // Simulate user not found
                if (request.UserId == 404)
                {
                    return Task.FromResult(Result.Failure(
                        Error.NotFound($"User with ID {request.UserId} not found", "USER_NOT_FOUND")));
                }

                // Simulate successful deletion
                return Task.FromResult(Result.Success());
            }
        }

        [Test]
        public async Task GetUserHandler_WithValidId_ShouldReturnSuccessResult()
        {
            // Arrange
            var handler = new GetUserHandler();
            var request = new GetUserRequest(1);

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Id.ShouldBe(1);
            result.Value.Name.ShouldBe("John Doe");
            result.Value.Email.ShouldBe("john@example.com");
        }

        [Test]
        public async Task GetUserHandler_WithInvalidId_ShouldReturnNotFoundError()
        {
            // Arrange
            var handler = new GetUserHandler();
            var request = new GetUserRequest(999);

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.ShouldBeNotFound();
            result.Error.Code.ShouldBe("USER_NOT_FOUND");
            result.Error.Message.ShouldContain("999");
        }

        [Test]
        public async Task CreateUserHandler_WithValidData_ShouldReturnSuccessResult()
        {
            // Arrange
            var handler = new CreateUserHandler();
            var request = new CreateUserRequest("Alice Johnson", "alice@example.com");

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Name.ShouldBe("Alice Johnson");
            result.Value.Email.ShouldBe("alice@example.com");
            result.Value.Id.ShouldBeGreaterThan(0);
        }

        [Test]
        public async Task CreateUserHandler_WithInvalidName_ShouldReturnValidationError()
        {
            // Arrange
            var handler = new CreateUserHandler();
            var request = new CreateUserRequest("", "alice@example.com");

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.ShouldBeValidationFailure("Name is required");
            result.Error.Code.ShouldBe("NAME_REQUIRED");
        }

        [Test]
        public async Task CreateUserHandler_WithInvalidEmail_ShouldReturnValidationError()
        {
            // Arrange
            var handler = new CreateUserHandler();
            var request = new CreateUserRequest("Alice Johnson", "invalid-email");

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.ShouldBeValidationFailure("Valid email is required");
            result.Error.Code.ShouldBe("EMAIL_INVALID");
        }

        [Test]
        public async Task UpdateUserHandler_WithValidData_ShouldReturnSuccessResult()
        {
            // Arrange
            var handler = new UpdateUserHandler();
            var request = new UpdateUserRequest(1, "Updated Name", "updated@example.com");

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Id.ShouldBe(1);
            result.Value.Name.ShouldBe("Updated Name");
            result.Value.Email.ShouldBe("updated@example.com");
        }

        [Test]
        public async Task UpdateUserHandler_WithConflictingEmail_ShouldReturnConflictError()
        {
            // Arrange
            var handler = new UpdateUserHandler();
            var request = new UpdateUserRequest(1, "Test User", "conflict@example.com");

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.ShouldBeFailureWith(ErrorType.Conflict, "Email already exists");
            result.Error.Code.ShouldBe("EMAIL_CONFLICT");
        }

        [Test]
        public async Task DeleteUserHandler_WithValidId_ShouldReturnSuccess()
        {
            // Arrange
            var handler = new DeleteUserHandler();
            var request = new DeleteUserRequest(1);

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
        }

        [Test]
        public async Task DeleteUserHandler_WithInvalidId_ShouldReturnNotFoundError()
        {
            // Arrange
            var handler = new DeleteUserHandler();
            var request = new DeleteUserRequest(404);

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.ShouldBeFailureWith(ErrorType.NotFound);
            result.Error.Code.ShouldBe("USER_NOT_FOUND");
        }

        private class UserDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }
    }

    [TestFixture]
    public class CrossModuleWorkflowTests
    {
        // Simulate cross-module workflow that uses shared components
        private sealed record ProcessOrderRequest(int OrderId, List<OrderItem> Items) : IRequest<Result<OrderProcessingResult>>;
        private sealed record ValidateInventoryRequest(List<OrderItem> Items) : IRequest<Result<InventoryValidationResult>>;
        private sealed record CalculatePricingRequest(List<OrderItem> Items) : IRequest<Result<PricingResult>>;

        private sealed class ProcessOrderHandler : IRequestHandler<ProcessOrderRequest, Result<OrderProcessingResult>>
        {
            private readonly IRequestHandler<ValidateInventoryRequest, Result<InventoryValidationResult>> _inventoryHandler;
            private readonly IRequestHandler<CalculatePricingRequest, Result<PricingResult>> _pricingHandler;

            public ProcessOrderHandler(
                IRequestHandler<ValidateInventoryRequest, Result<InventoryValidationResult>> inventoryHandler,
                IRequestHandler<CalculatePricingRequest, Result<PricingResult>> pricingHandler)
            {
                _inventoryHandler = inventoryHandler;
                _pricingHandler = pricingHandler;
            }

            public async Task<Result<OrderProcessingResult>> HandleAsync(ProcessOrderRequest request, CancellationToken cancellationToken)
            {
                // Validate order
                if (request.OrderId <= 0)
                {
                    return Result<OrderProcessingResult>.Failure(
                        Error.Validation("Order ID must be positive", "INVALID_ORDER_ID"));
                }

                if (request.Items.Count == 0)
                {
                    return Result<OrderProcessingResult>.Failure(
                        Error.Validation("Order must contain at least one item", "EMPTY_ORDER"));
                }

                // Step 1: Validate inventory
                var inventoryRequest = new ValidateInventoryRequest(request.Items);
                var inventoryResult = await _inventoryHandler.HandleAsync(inventoryRequest, cancellationToken);

                if (inventoryResult.IsFailure)
                {
                    return Result<OrderProcessingResult>.Failure(inventoryResult.Error);
                }

                // Step 2: Calculate pricing
                var pricingRequest = new CalculatePricingRequest(request.Items);
                var pricingResult = await _pricingHandler.HandleAsync(pricingRequest, cancellationToken);

                if (pricingResult.IsFailure)
                {
                    return Result<OrderProcessingResult>.Failure(pricingResult.Error);
                }

                // Step 3: Process order
                var result = new OrderProcessingResult
                {
                    OrderId = request.OrderId,
                    TotalPrice = pricingResult.Value.TotalPrice,
                    ProcessedAt = DateTime.UtcNow,
                    Status = "Processed"
                };

                return Result<OrderProcessingResult>.Success(result);
            }
        }

        private sealed class ValidateInventoryHandler : IRequestHandler<ValidateInventoryRequest, Result<InventoryValidationResult>>
        {
            public Task<Result<InventoryValidationResult>> HandleAsync(ValidateInventoryRequest request, CancellationToken cancellationToken)
            {
                // Simulate inventory validation
                var unavailableItems = request.Items.Where(item => item.ProductId == 999).ToList();

                if (unavailableItems.Count != 0)
                {
                    var productIds = string.Join(", ", unavailableItems.Select(i => i.ProductId));
                    return Task.FromResult(Result<InventoryValidationResult>.Failure(
                        Error.NotFound($"Products not in stock: {productIds}", "INSUFFICIENT_INVENTORY")));
                }

                var result = new InventoryValidationResult
                {
                    IsValid = true,
                    AvailableItems = request.Items.ToList()
                };

                return Task.FromResult(Result<InventoryValidationResult>.Success(result));
            }
        }

        private sealed class CalculatePricingHandler : IRequestHandler<CalculatePricingRequest, Result<PricingResult>>
        {
            public Task<Result<PricingResult>> HandleAsync(CalculatePricingRequest request, CancellationToken cancellationToken)
            {
                // Simulate pricing calculation
                try
                {
                    var totalPrice = request.Items.Sum(item => item.Quantity * item.UnitPrice);
                    
                    if (totalPrice < 0)
                    {
                        return Task.FromResult(Result<PricingResult>.Failure(
                            Error.Validation("Total price cannot be negative", "NEGATIVE_PRICE")));
                    }

                    var result = new PricingResult
                    {
                        TotalPrice = totalPrice,
                        ItemCount = request.Items.Sum(i => i.Quantity)
                    };

                    return Task.FromResult(Result<PricingResult>.Success(result));
                }
                catch (OverflowException ex)
                {
                    return Task.FromResult(Result<PricingResult>.Failure(
                        Error.InternalError("Price calculation overflow", "CALCULATION_OVERFLOW", ex)));
                }
            }
        }

        [Test]
        public async Task ProcessOrderWorkflow_WithValidOrder_ShouldSucceed()
        {
            // Arrange
            var inventoryHandler = new ValidateInventoryHandler();
            var pricingHandler = new CalculatePricingHandler();
            var orderHandler = new ProcessOrderHandler(inventoryHandler, pricingHandler);

            var items = new List<OrderItem>
            {
                new() { ProductId = 1, Quantity = 2, UnitPrice = 10.00m },
                new() { ProductId = 2, Quantity = 1, UnitPrice = 25.00m }
            };

            var request = new ProcessOrderRequest(123, items);

            // Act
            var result = await orderHandler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.OrderId.ShouldBe(123);
            result.Value.TotalPrice.ShouldBe(45.00m);
            result.Value.Status.ShouldBe("Processed");
        }

        [Test]
        public async Task ProcessOrderWorkflow_WithInventoryError_ShouldReturnError()
        {
            // Arrange
            var inventoryHandler = new ValidateInventoryHandler();
            var pricingHandler = new CalculatePricingHandler();
            var orderHandler = new ProcessOrderHandler(inventoryHandler, pricingHandler);

            var items = new List<OrderItem>
            {
                new() { ProductId = 999, Quantity = 1, UnitPrice = 10.00m } // This will cause inventory error
            };

            var request = new ProcessOrderRequest(123, items);

            // Act
            var result = await orderHandler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
            result.Error.Code.ShouldBe("INSUFFICIENT_INVENTORY");
        }

        [Test]
        public async Task ProcessOrderWorkflow_WithEmptyOrder_ShouldReturnValidationError()
        {
            // Arrange
            var inventoryHandler = new ValidateInventoryHandler();
            var pricingHandler = new CalculatePricingHandler();
            var orderHandler = new ProcessOrderHandler(inventoryHandler, pricingHandler);

            var request = new ProcessOrderRequest(123, new List<OrderItem>());

            // Act
            var result = await orderHandler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.ShouldBeValidationFailure("Order must contain at least one item");
            result.Error.Code.ShouldBe("EMPTY_ORDER");
        }

        private class OrderItem
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }

        private class OrderProcessingResult
        {
            public int OrderId { get; set; }
            public decimal TotalPrice { get; set; }
            public DateTime ProcessedAt { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        private class InventoryValidationResult
        {
            public bool IsValid { get; set; }
            public List<OrderItem> AvailableItems { get; set; } = new();
        }

        private class PricingResult
        {
            public decimal TotalPrice { get; set; }
            public int ItemCount { get; set; }
        }
    }

    [TestFixture]
    public class ErrorPropagationTests
    {
        // Tests how errors propagate through different layers of the application
        private sealed record ComplexOperationRequest(string Data) : IRequest<Result<string>>;

        private sealed class ComplexOperationHandler : IRequestHandler<ComplexOperationRequest, Result<string>>
        {
            public async Task<Result<string>> HandleAsync(ComplexOperationRequest request, CancellationToken cancellationToken)
            {
                // Simulate a complex operation with multiple potential failure points
                var step1Result = await ValidateInput(request.Data);
                if (step1Result.IsFailure) return step1Result;

                var step2Result = await ProcessData(step1Result.Value);
                if (step2Result.IsFailure) return Result<string>.Failure(step2Result.Error);

                var step3Result = await SaveData(step2Result.Value);
                if (step3Result.IsFailure) return Result<string>.Failure(step3Result.Error);

                return Result<string>.Success($"Completed: {step3Result.Value}");
            }

            private static async Task<Result<string>> ValidateInput(string data)
            {
                await Task.Delay(1); // Simulate async work

                if (string.IsNullOrWhiteSpace(data))
                    return Result<string>.Failure(Error.Validation("Data cannot be empty", "EMPTY_DATA"));

                if (data.Length > 100)
                    return Result<string>.Failure(Error.Validation("Data too long", "DATA_TOO_LONG"));

                return Result<string>.Success(data.Trim());
            }

            private static async Task<Result<string>> ProcessData(string data)
            {
                await Task.Delay(1); // Simulate async work

                if (data.Contains("error"))
                    return Result<string>.Failure(Error.InternalError("Processing failed", "PROCESSING_ERROR"));

                if (data.Contains("external"))
                    return Result<string>.Failure(Error.ExternalService("External service unavailable", "SERVICE_DOWN"));

                return Result<string>.Success($"Processed: {data}");
            }

            private static async Task<Result<string>> SaveData(string data)
            {
                await Task.Delay(1); // Simulate async work

                if (data.Contains("conflict"))
                    return Result<string>.Failure(Error.Conflict("Data already exists", "DUPLICATE_DATA"));

                if (data.Contains("unauthorized"))
                    return Result<string>.Failure(Error.Unauthorized("Not authorized to save", "SAVE_UNAUTHORIZED"));

                return Result<string>.Success($"Saved: {data}");
            }
        }

        [Test]
        public async Task ComplexOperation_WithValidData_ShouldSucceed()
        {
            // Arrange
            var handler = new ComplexOperationHandler();
            var request = new ComplexOperationRequest("valid data");

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe("Completed: Saved: Processed: valid data");
        }

        [Test]
        public async Task ComplexOperation_WithValidationError_ShouldReturnValidationError()
        {
            // Arrange
            var handler = new ComplexOperationHandler();
            var request = new ComplexOperationRequest("");

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.ShouldBeValidationFailure("Data cannot be empty");
            result.Error.Code.ShouldBe("EMPTY_DATA");
        }

        [Test]
        public async Task ComplexOperation_WithProcessingError_ShouldReturnInternalError()
        {
            // Arrange
            var handler = new ComplexOperationHandler();
            var request = new ComplexOperationRequest("error data");

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.ShouldBeFailureWith(ErrorType.InternalError, "Processing failed");
            result.Error.Code.ShouldBe("PROCESSING_ERROR");
        }

        [Test]
        public async Task ComplexOperation_WithExternalServiceError_ShouldReturnExternalServiceError()
        {
            // Arrange
            var handler = new ComplexOperationHandler();
            var request = new ComplexOperationRequest("external data");

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.ShouldBeExternalServiceFailure();
            result.Error.Code.ShouldBe("SERVICE_DOWN");
        }

        [Test]
        public async Task ComplexOperation_WithConflictError_ShouldReturnConflictError()
        {
            // Arrange
            var handler = new ComplexOperationHandler();
            var request = new ComplexOperationRequest("conflict data");

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.ShouldBeFailureWith(ErrorType.Conflict, "Data already exists");
            result.Error.Code.ShouldBe("DUPLICATE_DATA");
        }

        [Test]
        public async Task ComplexOperation_WithUnauthorizedError_ShouldReturnUnauthorizedError()
        {
            // Arrange
            var handler = new ComplexOperationHandler();
            var request = new ComplexOperationRequest("unauthorized data");

            // Act
            var result = await handler.HandleAsync(request, CancellationToken.None);

            // Assert
            result.ShouldBeFailureWith(ErrorType.Unauthorized, "Not authorized to save");
            result.Error.Code.ShouldBe("SAVE_UNAUTHORIZED");
        }
    }

    [TestFixture]
    public class ComponentInteroperabilityTests
    {
        [Test]
        public void SharedComponents_ShouldWorkWithBuilders()
        {
            // Arrange & Act
            var successResult = ResultBuilder<string>.Success().WithValue("test").Build();
            var errorResult = ResultBuilder<string>.Failure().WithValidationError("error").Build();
            var error = ErrorBuilder.Validation().WithMessage("test error").Build();

            // Assert
            successResult.IsSuccess.ShouldBeTrue();
            successResult.Value.ShouldBe("test");

            errorResult.IsFailure.ShouldBeTrue();
            errorResult.Error.Type.ShouldBe(ErrorType.Validation);

            error.Type.ShouldBe(ErrorType.Validation);
            error.Message.ShouldBe("test error");
        }

        [Test]
        public void SharedComponents_ShouldWorkWithGenerators()
        {
            // Arrange & Act
            var stringResults = TestDataGenerators.Results.StringResults().Take(5).ToList();
            var errors = TestDataGenerators.Errors.ValidationErrors().Take(3).ToList();

            // Assert
            stringResults.Count.ShouldBe(5);
            stringResults.ShouldContain(r => r.IsSuccess);
            stringResults.ShouldContain(r => r.IsFailure);

            errors.Count.ShouldBe(3);
            errors.All(e => e.Type == ErrorType.Validation).ShouldBeTrue();
        }

        [Test]
        public void SharedComponents_ShouldWorkWithExtensions()
        {
            // Arrange
            var successResult = Result<string>.Success("test");
            var failureResult = Result<string>.Failure(Error.NotFound("not found"));

            // Act & Assert
            successResult.ShouldBeSuccess();
            successResult.ShouldBeSuccessWithValue().ShouldBe("test");

            failureResult.ShouldBeFailure();
            failureResult.ShouldBeNotFound();
        }
    }
}