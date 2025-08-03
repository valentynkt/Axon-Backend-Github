using Axon.Shared.Common;
using Axon.Shared.Common.Abstractions;
using Axon.Tests.Shared.Builders;
// using Axon.Tests.Shared.Tests.Extensions; // Removed to fix circular dependency
using Axon.Tests.Shared.Generators;

namespace Axon.Tests.Shared.Integration;

/// <summary>
/// Integration tests ensuring proper interaction between all shared utilities
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public sealed class SharedComponentsIntegrationTests
{
    [TestFixture]
    public class ResultErrorIntegrationTests
    {
        [Test]
        public void Result_ErrorIntegration_ShouldMaintainErrorIntegrity()
        {
            // Arrange
            var originalError = Error.Validation("Field is required", "FIELD_REQUIRED");

            // Act
            var result = Result<string>.Failure(originalError);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldBe(originalError);
            result.Error.Type.ShouldBe(ErrorType.Validation);
            result.Error.Message.ShouldBe("Field is required");
            result.Error.Code.ShouldBe("FIELD_REQUIRED");
        }

        [Test]
        public void Result_ImplicitConversionFromError_ShouldPreserveErrorDetails()
        {
            // Arrange
            var error = Error.ExternalService("API timeout", "API_TIMEOUT", new TimeoutException("Request timeout"));

            // Act
            Result<int> result = error; // Implicit conversion

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.ExternalService);
            result.Error.Message.ShouldBe("API timeout");
            result.Error.Code.ShouldBe("API_TIMEOUT");
            result.Error.InnerException.ShouldBeOfType<TimeoutException>();
            result.Error.InnerException!.Message.ShouldBe("Request timeout");
        }

        [Test]
        public void Result_ConversionBetweenGenericAndNonGeneric_ShouldPreserveError()
        {
            // Arrange
            var error = Error.NotFound("Resource not found", "RESOURCE_NOT_FOUND");
            var genericResult = Result<string>.Failure(error);

            // Act
            Result nonGenericResult = genericResult; // Implicit conversion

            // Assert
            nonGenericResult.IsFailure.ShouldBeTrue();
            nonGenericResult.Error.ShouldBe(error);
            nonGenericResult.Error.Type.ShouldBe(ErrorType.NotFound);
            nonGenericResult.Error.Message.ShouldBe("Resource not found");
            nonGenericResult.Error.Code.ShouldBe("RESOURCE_NOT_FOUND");
        }

        [Test]
        public void Result_MultipleConversions_ShouldMaintainIntegrity()
        {
            // Arrange
            var originalError = Error.Conflict("Duplicate entry", "DUPLICATE_ENTRY");
            
            // Act - Multiple conversions
            Result<string> genericResult = originalError;
            Result nonGenericResult = genericResult;
            var backToGeneric = nonGenericResult.IsFailure ? 
                Result<string>.Failure(nonGenericResult.Error) : 
                Result<string>.Success("dummy");

            // Assert
            backToGeneric.IsFailure.ShouldBeTrue();
            backToGeneric.Error.ShouldBe(originalError);
            backToGeneric.Error.Type.ShouldBe(ErrorType.Conflict);
            backToGeneric.Error.Message.ShouldBe("Duplicate entry");
            backToGeneric.Error.Code.ShouldBe("DUPLICATE_ENTRY");
        }
    }

    [TestFixture]
    public class BuilderIntegrationTests
    {
        [Test]
        public void ResultBuilder_WithErrorBuilder_ShouldIntegrateSeamlessly()
        {
            // Arrange
            var error = ErrorBuilder.Validation()
                .WithMessage("Username is required")
                .WithCode("USERNAME_REQUIRED")
                .Build();

            // Act
            var result = ResultBuilder<string>.Failure()
                .WithError(error)
                .Build();

            // Assert
            result.ShouldBeFailure();
            result.ShouldBeValidationFailure("Username is required");
            result.Error.Code.ShouldBe("USERNAME_REQUIRED");
        }

        [Test]
        public void ResultBuilder_AllErrorTypes_ShouldIntegrateCorrectly()
        {
            // Arrange & Act & Assert
            var validationResult = ResultBuilder<int>.Failure()
                .WithValidationError("Validation failed", "VALIDATION_FAILED")
                .Build();
            validationResult.ShouldBeValidationFailure("Validation failed");

            var notFoundResult = ResultBuilder<int>.Failure()
                .WithNotFoundError("Not found", "NOT_FOUND")
                .Build();
            notFoundResult.ShouldBeNotFound();

            var conflictResult = ResultBuilder<int>.Failure()
                .WithConflictError("Conflict", "CONFLICT")
                .Build();
            conflictResult.ShouldBeFailureWith(ErrorType.Conflict, "Conflict");

            var internalResult = ResultBuilder<int>.Failure()
                .WithInternalError("Internal error", "INTERNAL", new InvalidOperationException("Test"))
                .Build();
            internalResult.ShouldBeFailureWith(ErrorType.InternalError, "Internal error");

            var externalResult = ResultBuilder<int>.Failure()
                .WithExternalServiceError("External error", "EXTERNAL", new HttpRequestException("HTTP error"))
                .Build();
            externalResult.ShouldBeExternalServiceFailure();

            var unauthorizedResult = ResultBuilder<int>.Failure()
                .WithUnauthorizedError("Unauthorized", "UNAUTHORIZED")
                .Build();
            unauthorizedResult.ShouldBeFailureWith(ErrorType.Unauthorized, "Unauthorized");

            var forbiddenResult = ResultBuilder<int>.Failure()
                .WithForbiddenError("Forbidden", "FORBIDDEN")
                .Build();
            forbiddenResult.ShouldBeFailureWith(ErrorType.Forbidden, "Forbidden");
        }

        [Test]
        public void ErrorBuilder_WithAllFactoryMethods_ShouldCreateCorrectErrors()
        {
            // Act & Assert
            var requiredField = ErrorBuilders.RequiredField("Email");
            requiredField.Type.ShouldBe(ErrorType.Validation);
            requiredField.Message.ShouldBe("Email is required");
            requiredField.Code.ShouldBe("FIELD_REQUIRED");

            var invalidFormat = ErrorBuilders.InvalidFormat("Phone", "+1-XXX-XXX-XXXX");
            invalidFormat.Type.ShouldBe(ErrorType.Validation);
            invalidFormat.Message.ShouldBe("Phone must be in format: +1-XXX-XXX-XXXX");

            var entityNotFound = ErrorBuilders.EntityNotFound("User", "123");
            entityNotFound.Type.ShouldBe(ErrorType.NotFound);
            entityNotFound.Message.ShouldBe("User with identifier '123' was not found");
            entityNotFound.Code.ShouldBe("USER_NOT_FOUND");

            var duplicateEntity = ErrorBuilders.DuplicateEntity("Product", "SKU123");
            duplicateEntity.Type.ShouldBe(ErrorType.Conflict);
            duplicateEntity.Message.ShouldBe("Product with identifier 'SKU123' already exists");
            duplicateEntity.Code.ShouldBe("PRODUCT_ALREADY_EXISTS");
        }
    }

    [TestFixture]
    public class ExtensionIntegrationTests
    {
        [Test]
        public void Extensions_WithGeneratedData_ShouldWorkAcrossAllTypes()
        {
            // Arrange
            var stringResults = TestDataGenerators.Results.StringResults().ToList();
            var intResults = TestDataGenerators.Results.IntResults().ToList();
            var dateTimeResults = TestDataGenerators.Results.DateTimeResults().ToList();
            var guidResults = TestDataGenerators.Results.GuidResults().ToList();

            // Act & Assert
            foreach (var result in stringResults)
            {
                if (result.IsSuccess)
                {
                    result.ShouldBeSuccess();
                    var value = result.ShouldBeSuccessWithValue();
                    value.ShouldNotBeNull();
                }
                else
                {
                    result.ShouldBeFailure();
                    result.Error.ShouldNotBeNull();
                }
            }

            foreach (var result in intResults)
            {
                if (result.IsSuccess)
                {
                    result.ShouldBeSuccess();
                    result.ShouldBeSuccessAnd(value => value.ShouldBeOfType<int>());
                }
                else
                {
                    result.ShouldBeFailure();
                }
            }

            foreach (var result in dateTimeResults)
            {
                if (result.IsSuccess)
                {
                    result.ShouldBeSuccessAnd(value => value.ShouldBeOfType<DateTime>());
                }
                else
                {
                    result.ShouldBeFailure();
                }
            }

            foreach (var result in guidResults)
            {
                if (result.IsSuccess)
                {
                    result.ShouldBeSuccessAnd(value => value.ShouldBeOfType<Guid>());
                }
                else
                {
                    result.ShouldBeFailure();
                }
            }
        }

        [Test]
        public void Extensions_WithBusinessScenarios_ShouldIntegrateCorrectly()
        {
            // Arrange
            var crudScenarios = TestDataGenerators.Scenarios.CrudScenarios().ToList();

            // Act & Assert
            foreach (var (operation, expected) in crudScenarios)
            {
                if (expected.IsSuccess)
                {
                    expected.ShouldBeSuccess();
                    expected.ShouldBeSuccessWithValue().ShouldNotBeNullOrEmpty();
                }
                else
                {
                    expected.ShouldBeFailure();
                    
                    // Verify appropriate error types for operations
                    switch (operation)
                    {
                        case "CreateDuplicate":
                            expected.ShouldBeFailureWith(ErrorType.Conflict);
                            break;
                        case "ReadNotFound":
                        case "UpdateNotFound":
                        case "DeleteNotFound":
                            expected.ShouldBeNotFound();
                            break;
                    }
                }
            }
        }
    }

    [TestFixture]
    public class AbstractionsIntegrationTests
    {
        // Test request implementation
        private class TestRequest : IRequest<Result<string>>
        {
            public string Data { get; set; } = string.Empty;
        }

        // Test handler implementation
        private class TestHandler : IRequestHandler<TestRequest, Result<string>>
        {
            public Task<Result<string>> HandleAsync(TestRequest request, CancellationToken cancellationToken)
            {
                if (string.IsNullOrEmpty(request.Data))
                {
                    return Task.FromResult(Result<string>.Failure(Error.Validation("Data is required")));
                }

                return Task.FromResult(Result<string>.Success($"Processed: {request.Data}"));
            }
        }

        [Test]
        public async Task Abstractions_WithResultPattern_ShouldIntegrateCorrectly()
        {
            // Arrange
            var handler = new TestHandler();
            var validRequest = new TestRequest { Data = "test data" };
            var invalidRequest = new TestRequest { Data = "" };

            // Act
            var validResult = await handler.HandleAsync(validRequest, CancellationToken.None);
            var invalidResult = await handler.HandleAsync(invalidRequest, CancellationToken.None);

            // Assert
            validResult.ShouldBeSuccess();
            validResult.ShouldBeSuccessWithValue().ShouldBe("Processed: test data");

            invalidResult.ShouldBeFailure();
            invalidResult.ShouldBeValidationFailure("Data is required");
        }

        [Test]
        public void Abstractions_TypeHierarchy_ShouldBeCorrect()
        {
            // Arrange
            var request = new TestRequest();

            // Act & Assert
            request.ShouldBeAssignableTo<IRequest<Result<string>>>();
            request.ShouldBeAssignableTo<IRequest>();

            var handler = new TestHandler();
            handler.ShouldBeAssignableTo<IRequestHandler<TestRequest, Result<string>>>();
        }
    }

    [TestFixture]
    public class ComplexScenarioIntegrationTests
    {
        [Test]
        public void ComplexScenario_UserRegistrationWorkflow_ShouldIntegrateAllComponents()
        {
            // Arrange - Simulate user registration workflow using all shared components
            var userEmail = "test@example.com";
            var password = "password123";

            // Act & Assert - Step 1: Validate email
            var emailValidationResult = ValidateEmail(userEmail);
            emailValidationResult.ShouldBeSuccess();

            // Act & Assert - Step 2: Validate password
            var passwordValidationResult = ValidatePassword(password);
            passwordValidationResult.ShouldBeSuccess();

            // Act & Assert - Step 3: Check if user exists (simulate not found)
            var userExistsResult = CheckUserExists(userEmail);
            userExistsResult.ShouldBeNotFound();

            // Act & Assert - Step 4: Create user (simulate success)
            var createUserResult = CreateUser(userEmail, password);
            createUserResult.ShouldBeSuccess();
            createUserResult.ShouldBeSuccessWithValue().ShouldContain("User created successfully");
        }

        [Test]
        public void ComplexScenario_UserRegistrationWithErrors_ShouldHandleAllErrorTypes()
        {
            // Arrange
            var invalidEmail = "invalid-email";
            var weakPassword = "123";
            var existingEmail = "existing@example.com";

            // Act & Assert - Email validation error
            var emailResult = ValidateEmail(invalidEmail);
            emailResult.ShouldBeValidationFailure("Invalid email format");

            // Act & Assert - Password validation error
            var passwordResult = ValidatePassword(weakPassword);
            passwordResult.ShouldBeValidationFailure("Password must be at least 8 characters");

            // Act & Assert - User already exists error
            var existingUserResult = CheckUserExists(existingEmail);
            existingUserResult.ShouldBeSuccessWithValue().ShouldBe("User exists");

            var createExistingUserResult = CreateUser(existingEmail, "validpassword");
            createExistingUserResult.ShouldBeFailureWith(ErrorType.Conflict, "User already exists");
        }

        [Test]
        public void ComplexScenario_DataProcessingPipeline_ShouldChainResults()
        {
            // Arrange - Simulate data processing pipeline
            var inputData = new[] { "data1", "data2", "data3", "invalid" };

            // Act
            var results = inputData.Select(ProcessData).ToList();

            // Assert
            results.Count.ShouldBe(4);
            results.Take(3).ShouldAllBe(r => r.IsSuccess);
            results.Last().ShouldBeFailure();
            results.Last().ShouldBeValidationFailure("Invalid data format");

            // Chain successful results
            var successfulResults = results.Where(r => r.IsSuccess).ToList();
            successfulResults.Count.ShouldBe(3);
            successfulResults.ShouldAllBe(r => r.Value.StartsWith("Processed:"));
        }

        // Helper methods simulating business logic
        private static Result<string> ValidateEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return ErrorBuilders.RequiredField("Email");
            
            if (!email.Contains("@") || !email.Contains("."))
                return ErrorBuilders.InvalidFormat("Email", "user@domain.com");
            
            return Result<string>.Success(email);
        }

        private static Result<string> ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return ErrorBuilders.RequiredField("Password");
            
            if (password.Length < 8)
                return Error.Validation("Password must be at least 8 characters");
            
            return Result<string>.Success(password);
        }

        private static Result<string> CheckUserExists(string email)
        {
            // Simulate database lookup
            return email == "existing@example.com" 
                ? Result<string>.Success("User exists")
                : Result<string>.Failure(Error.NotFound("User not found"));
        }

        private static Result<string> CreateUser(string email, string password)
        {
            // Simulate user creation
            if (email == "existing@example.com")
                return Error.Conflict("User already exists");
            
            return Result<string>.Success($"User created successfully with email: {email}");
        }

        private static Result<string> ProcessData(string data)
        {
            if (data == "invalid")
                return Error.Validation("Invalid data format");
            
            return Result<string>.Success($"Processed: {data}");
        }
    }

    [TestFixture]
    public class PerformanceIntegrationTests
    {
        [Test]
        [Category("Performance")]
        public void IntegrationPerformance_LargeDatasets_ShouldPerformWell()
        {
            // Arrange
            const int itemCount = 10_000;
            var items = Enumerable.Range(0, itemCount).Select(i => $"item{i}").ToList();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var results = items.Select(item =>
            {
                var validation = ValidateItem(item);
                if (validation.IsFailure) return validation;
                
                return ProcessItem(item);
            }).ToList();

            stopwatch.Stop();

            // Assert
            results.Count.ShouldBe(itemCount);
            results.All(r => r.IsSuccess).ShouldBeTrue();
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500); // Should be reasonably fast
        }

        [Test]
        [Category("Performance")]
        public void IntegrationPerformance_ParallelProcessing_ShouldScaleWell()
        {
            // Arrange
            const int itemCount = 5_000;
            var items = Enumerable.Range(0, itemCount).Select(i => $"item{i}").ToList();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var results = items.AsParallel().Select(item =>
            {
                var validation = ValidateItem(item);
                if (validation.IsFailure) return validation;
                
                return ProcessItem(item);
            }).ToList();

            stopwatch.Stop();

            // Assert
            results.Count.ShouldBe(itemCount);
            results.All(r => r.IsSuccess).ShouldBeTrue();
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(200); // Should be faster than sequential
        }

        private static Result<string> ValidateItem(string item)
        {
            return string.IsNullOrEmpty(item) 
                ? Error.Validation("Item cannot be empty")
                : Result<string>.Success(item);
        }

        private static Result<string> ProcessItem(string item)
        {
            return Result<string>.Success($"Processed: {item}");
        }
    }
}