using Axon.Shared.Common;
using Axon.Tests.Shared.Tests.Extensions;
using Axon.Tests.Shared.Generators;
using Axon.Tests.Shared.Builders;

namespace Axon.Tests.Shared.Tests.Extensions;

/// <summary>
/// Comprehensive tests for all extension methods including ResultTestExtensions
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public sealed class ExtensionMethodTests
{
    [TestFixture]
    public class ResultTestExtensionsTests
    {
        [TestFixture]
        public class GenericResultExtensionTests
        {
            [Test]
            public void ShouldBeSuccessGeneric_WithSuccessfulResult_ShouldNotThrow()
            {
                // Arrange
                var result = Result<string>.Success("test value");

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeSuccess());
            }

            [Test]
            public void ShouldBeSuccessGeneric_WithFailedResult_ShouldThrow()
            {
                // Arrange
                var result = Result<string>.Failure(Error.Validation("Test error"));

                // Act & Assert
                var exception = Should.Throw<ShouldAssertException>(() => result.ShouldBeSuccess());
                exception.Message.ShouldContain("Expected result to be successful but it failed with error: Test error");
            }

            [Test]
            public void ShouldBeSuccessWithValue_WithSuccessfulResult_ShouldReturnValue()
            {
                // Arrange
                const string expectedValue = "test value";
                var result = Result<string>.Success(expectedValue);

                // Act
                var actualValue = result.ShouldBeSuccessWithValue();

                // Assert
                actualValue.ShouldBe(expectedValue);
            }

            [Test]
            public void ShouldBeSuccessWithValue_WithFailedResult_ShouldThrow()
            {
                // Arrange
                var result = Result<string>.Failure(Error.NotFound("Not found"));

                // Act & Assert
                var exception = Should.Throw<ShouldAssertException>(() => result.ShouldBeSuccessWithValue());
                exception.Message.ShouldContain("Expected result to be successful but it failed with error: Not found");
            }

            [Test]
            public void ShouldBeFailure_WithFailedResult_ShouldNotThrow()
            {
                // Arrange
                var result = Result<string>.Failure(Error.Conflict("Conflict error"));

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeFailure());
            }

            [Test]
            public void ShouldBeFailure_WithSuccessfulResult_ShouldThrow()
            {
                // Arrange
                var result = Result<string>.Success("success value");

                // Act & Assert
                var exception = Should.Throw<ShouldAssertException>(() => result.ShouldBeFailure());
                exception.Message.ShouldContain("Expected result to be failure but it was successful with value: success value");
            }

            [Test]
            public void ShouldBeFailureWith_WithCorrectErrorType_ShouldNotThrow()
            {
                // Arrange
                var result = Result<string>.Failure(Error.Validation("Validation error"));

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeFailureWith(ErrorType.Validation));
            }

            [Test]
            public void ShouldBeFailureWith_WithIncorrectErrorType_ShouldThrow()
            {
                // Arrange
                var result = Result<string>.Failure(Error.Validation("Validation error"));

                // Act & Assert
                Should.Throw<ShouldAssertException>(() => result.ShouldBeFailureWith(ErrorType.NotFound));
            }

            [Test]
            public void ShouldBeFailureWith_WithTypeAndMessage_ShouldValidateBoth()
            {
                // Arrange
                const string expectedMessage = "Specific validation error";
                var result = Result<string>.Failure(Error.Validation(expectedMessage));

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeFailureWith(ErrorType.Validation, expectedMessage));
            }

            [Test]
            public void ShouldBeFailureWith_WithWrongMessage_ShouldThrow()
            {
                // Arrange
                var result = Result<string>.Failure(Error.Validation("Actual message"));

                // Act & Assert
                Should.Throw<ShouldAssertException>(() => result.ShouldBeFailureWith(ErrorType.Validation, "Expected message"));
            }

            [Test]
            public void ShouldBeValidationFailure_WithValidationError_ShouldNotThrow()
            {
                // Arrange
                var result = Result<string>.Failure(Error.Validation("Field is required"));

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeValidationFailure());
            }

            [Test]
            public void ShouldBeValidationFailure_WithNonValidationError_ShouldThrow()
            {
                // Arrange
                var result = Result<string>.Failure(Error.NotFound("Not found"));

                // Act & Assert
                Should.Throw<ShouldAssertException>(() => result.ShouldBeValidationFailure());
            }

            [Test]
            public void ShouldBeValidationFailureWithMessage_ShouldValidateMessage()
            {
                // Arrange
                const string expectedMessage = "Email is required";
                var result = Result<string>.Failure(Error.Validation(expectedMessage));

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeValidationFailure(expectedMessage));
            }

            [Test]
            public void ShouldBeNotFound_WithNotFoundError_ShouldNotThrow()
            {
                // Arrange
                var result = Result<string>.Failure(Error.NotFound("Resource not found"));

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeNotFound());
            }

            [Test]
            public void ShouldBeExternalServiceFailure_WithExternalServiceError_ShouldNotThrow()
            {
                // Arrange
                var result = Result<string>.Failure(Error.ExternalService("API unavailable"));

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeExternalServiceFailure());
            }

            [Test]
            public void ShouldBeSuccessAnd_WithSuccessfulResult_ShouldExecuteAssertion()
            {
                // Arrange
                var result = Result<string>.Success("test value");
                var assertionExecuted = false;

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeSuccessAnd(value =>
                {
                    value.ShouldBe("test value");
                    assertionExecuted = true;
                }));

                assertionExecuted.ShouldBeTrue();
            }

            [Test]
            public void ShouldBeSuccessAnd_WithFailedResult_ShouldThrowWithoutExecutingAssertion()
            {
                // Arrange
                var result = Result<string>.Failure(Error.Validation("Error"));
                var assertionExecuted = false;

                // Act & Assert
                Should.Throw<ShouldAssertException>(() => result.ShouldBeSuccessAnd(value =>
                {
                    assertionExecuted = true;
                }));

                assertionExecuted.ShouldBeFalse();
            }
        }

        [TestFixture]
        public class NonGenericResultExtensionTests
        {
            [Test]
            public void ShouldBeSuccess_WithSuccessfulResult_ShouldNotThrow()
            {
                // Arrange
                var result = Result.Success();

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeSuccess());
            }

            [Test]
            public void ShouldBeSuccess_WithFailedResult_ShouldThrow()
            {
                // Arrange
                var result = Result.Failure(Error.InternalError("Internal error"));

                // Act & Assert
                var exception = Should.Throw<ShouldAssertException>(() => result.ShouldBeSuccess());
                exception.Message.ShouldContain("Expected result to be successful but it failed with error: Internal error");
            }

            [Test]
            public void ShouldBeFailure_WithFailedResult_ShouldNotThrow()
            {
                // Arrange
                var result = Result.Failure(Error.Unauthorized("Unauthorized"));

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeFailure());
            }

            [Test]
            public void ShouldBeFailure_WithSuccessfulResult_ShouldThrow()
            {
                // Arrange
                var result = Result.Success();

                // Act & Assert
                var exception = Should.Throw<ShouldAssertException>(() => result.ShouldBeFailure());
                exception.Message.ShouldBe("Expected result to be failure but it was successful");
            }

            [Test]
            public void ShouldBeFailureWith_WithCorrectErrorType_ShouldNotThrow()
            {
                // Arrange
                var result = Result.Failure(Error.Forbidden("Access denied"));

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeFailureWith(ErrorType.Forbidden));
            }

            [Test]
            public void ShouldBeFailureWith_WithTypeAndMessage_ShouldValidateBoth()
            {
                // Arrange
                const string expectedMessage = "Configuration error";
                var result = Result.Failure(Error.InternalError(expectedMessage));

                // Act & Assert
                Should.NotThrow(() => result.ShouldBeFailureWith(ErrorType.InternalError, expectedMessage));
            }
        }

        [TestFixture]
        public class ExtensionMethodsWithGeneratedData
        {
            [Test]
            public void Extensions_WithGeneratedResults_ShouldWorkCorrectly()
            {
                // Arrange
                var results = TestDataGenerators.Results.StringResults().ToList();

                // Act & Assert
                foreach (var result in results)
                {
                    if (result.IsSuccess)
                    {
                        Should.NotThrow(() => result.ShouldBeSuccess());
                        Should.NotThrow(() => result.ShouldBeSuccessWithValue());
                        Should.Throw<ShouldAssertException>(() => result.ShouldBeFailure());
                    }
                    else
                    {
                        Should.NotThrow(() => result.ShouldBeFailure());
                        Should.Throw<ShouldAssertException>(() => result.ShouldBeSuccess());
                        Should.Throw<ShouldAssertException>(() => result.ShouldBeSuccessWithValue());

                        // Test error type specific assertions
                        switch (result.Error.Type)
                        {
                            case ErrorType.Validation:
                                Should.NotThrow(() => result.ShouldBeValidationFailure());
                                break;
                            case ErrorType.NotFound:
                                Should.NotThrow(() => result.ShouldBeNotFound());
                                break;
                            case ErrorType.ExternalService:
                                Should.NotThrow(() => result.ShouldBeExternalServiceFailure());
                                break;
                        }
                    }
                }
            }

            [Test]
            public void Extensions_WithAllErrorTypes_ShouldHandleCorrectly()
            {
                // Arrange
                var errors = TestDataGenerators.Errors.AllErrorTypes(1).ToList();

                // Act & Assert
                foreach (var error in errors)
                {
                    var result = Result<string>.Failure(error);
                    
                    Should.NotThrow(() => result.ShouldBeFailure());
                    Should.NotThrow(() => result.ShouldBeFailureWith(error.Type));
                    Should.NotThrow(() => result.ShouldBeFailureWith(error.Type, error.Message));
                }
            }

            [Test]
            public void Extensions_WithEdgeCaseErrors_ShouldHandleCorrectly()
            {
                // Arrange
                var edgeCaseErrors = TestDataGenerators.Errors.EdgeCaseErrors().ToList();

                // Act & Assert
                foreach (var error in edgeCaseErrors)
                {
                    var result = Result<string>.Failure(error);
                    
                    Should.NotThrow(() => result.ShouldBeFailure());
                    Should.NotThrow(() => result.ShouldBeFailureWith(error.Type));
                    
                    // Should handle edge case messages correctly
                    Should.NotThrow(() => result.ShouldBeFailureWith(error.Type, error.Message));
                }
            }
        }

        [TestFixture]
        public class ExtensionMethodPerformanceTests
        {
            [Test]
            [Category("Performance")]
            public void Extensions_PerformanceTest_ShouldBeEfficient()
            {
                // Arrange
                const int iterations = 10_000;
                var results = Enumerable.Range(0, iterations)
                    .Select(i => i % 2 == 0 ? Result<int>.Success(i) : Result<int>.Failure(Error.Validation($"Error {i}")))
                    .ToList();

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Act
                foreach (var result in results)
                {
                    if (result.IsSuccess)
                    {
                        result.ShouldBeSuccess();
                    }
                    else
                    {
                        result.ShouldBeFailure();
                    }
                }

                stopwatch.Stop();

                // Assert
                stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100); // Should be very fast
            }

            [Test]
            [Category("Performance")]
            public void Extensions_ParallelExecution_ShouldBeThreadSafe()
            {
                // Arrange
                const int iterations = 1_000;
                var results = Enumerable.Range(0, iterations)
                    .Select(i => Result<int>.Success(i))
                    .ToList();

                // Act & Assert
                Should.NotThrow(() =>
                {
                    Parallel.ForEach(results, result =>
                    {
                        result.ShouldBeSuccess();
                        var value = result.ShouldBeSuccessWithValue();
                        value.ShouldBeGreaterThanOrEqualTo(0);
                    });
                });
            }
        }

        [TestFixture]
        public class ExtensionMethodIntegrationTests
        {
            [Test]
            public void Extensions_WithBuilders_ShouldIntegrateCorrectly()
            {
                // Arrange & Act & Assert
                ResultBuilder<string>.Success()
                    .WithValue("test")
                    .Build()
                    .ShouldBeSuccess();

                ResultBuilder<string>.Failure()
                    .WithValidationError("Required field")
                    .Build()
                    .ShouldBeValidationFailure("Required field");

                ResultBuilder<int>.Success()
                    .WithValue(42)
                    .Build()
                    .ShouldBeSuccessAnd(value => value.ShouldBe(42));
            }

            [Test]
            public void Extensions_WithErrorBuilders_ShouldIntegrateCorrectly()
            {
                // Arrange
                var error = ErrorBuilder.Validation()
                    .WithMessage("Custom validation error")
                    .WithCode("CUSTOM_CODE")
                    .Build();

                var result = Result<string>.Failure(error);

                // Act & Assert
                result.ShouldBeFailureWith(ErrorType.Validation, "Custom validation error");
            }

            [Test]
            public void Extensions_WithTestScenarios_ShouldWorkCorrectly()
            {
                // Arrange
                var scenarios = TestDataGenerators.Scenarios.ValidationScenarios();

                // Act & Assert
                foreach (var (input, expected) in scenarios)
                {
                    if (expected.IsSuccess)
                    {
                        expected.ShouldBeSuccess();
                        expected.ShouldBeSuccessWithValue().ShouldBe(input);
                    }
                    else
                    {
                        expected.ShouldBeFailure();
                        if (expected.Error.Type == ErrorType.Validation)
                        {
                            expected.ShouldBeValidationFailure();
                        }
                    }
                }
            }
        }
    }
}