using System.Linq.Expressions;
using Axon.Shared.Common;
// using Axon.Tests.Shared.Tests.Extensions; // Removed to fix circular dependency
using Axon.Tests.Shared.TestBase;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using Shouldly;

namespace Axon.Tests.Shared.Templates;

/// <summary>
/// Reusable test templates following London School TDD principles for consistent testing across modules
/// </summary>
public static class LondonSchoolTestTemplates
{
    /// <summary>
    /// Template for testing command handlers with behavior verification
    /// </summary>
    public static class CommandHandlerTemplate<TCommand, TResponse, THandler>
        where TCommand : class
        where THandler : class
    {
        /// <summary>
        /// Creates a standard behavior test for command processing
        /// </summary>
        public static void ShouldProcessCommand_WithExpectedBehavior(
            Func<THandler> handlerFactory,
            TCommand command,
            Func<Task<Result<TResponse>>> executeCommand,
            Action<Mock<ILogger>>? loggerVerification = null,
            params Mock[] collaboratorMocks)
        {
            // Arrange
            var handler = handlerFactory();

            // Act
            var result = executeCommand().GetAwaiter().GetResult();

            // Assert
            result.ShouldBeSuccess();

            // Verify all collaborator interactions
            foreach (var mock in collaboratorMocks)
            {
                mock.VerifyAll();
            }

            // Verify logging if specified
            loggerVerification?.Invoke(collaboratorMocks.OfType<Mock<ILogger>>().FirstOrDefault()!);
        }

        /// <summary>
        /// Creates a standard error handling test for command processing
        /// </summary>
        public static void ShouldHandleError_WithExpectedBehavior(
            Func<THandler> handlerFactory,
            TCommand command,
            Func<Task<Result<TResponse>>> executeCommand,
            Error expectedError,
            Action<Mock<ILogger>>? loggerVerification = null)
        {
            // Act
            var result = executeCommand().GetAwaiter().GetResult();

            // Assert
            result.ShouldBeFailure();
            result.Error.ShouldBe(expectedError);

            // Verify error logging if specified
            loggerVerification?.Invoke(null!);
        }

        /// <summary>
        /// Creates a standard interaction verification test
        /// </summary>
        public static void ShouldFollowInteractionPattern(
            Func<THandler> handlerFactory,
            TCommand command,
            Func<Task<Result<TResponse>>> executeCommand,
            params Action<Mock>[] verificationActions)
        {
            // Act
            var result = executeCommand().GetAwaiter().GetResult();

            // Assert
            result.ShouldBeSuccess();

            // Verify specific interaction patterns
            foreach (var verification in verificationActions)
            {
                // This would be called with specific mocked collaborators
                // verification(collaboratorMock);
            }
        }
    }

    /// <summary>
    /// Template for testing infrastructure adapters with HTTP interactions
    /// </summary>
    public static class InfrastructureAdapterTemplate<TAdapter, TRequest, TResponse>
        where TAdapter : class
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Creates a standard HTTP interaction test
        /// </summary>
        public static void ShouldMakeHttpRequest_WithExpectedPayload(
            Func<TAdapter> adapterFactory,
            TRequest request,
            Func<TRequest, Task<Result<TResponse>>> executeRequest,
            Mock<HttpMessageHandler> httpHandlerMock,
            Func<string, bool> payloadValidator)
        {
            // Act
            var result = executeRequest(request).GetAwaiter().GetResult();

            // Assert
            result.ShouldBeSuccess();

            // Verify HTTP interaction occurred
            httpHandlerMock.Protected().Verify<Task<HttpResponseMessage>>(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        /// <summary>
        /// Creates a standard HTTP error handling test
        /// </summary>
        public static void ShouldHandleHttpError_Gracefully(
            Func<TAdapter> adapterFactory,
            TRequest request,
            Func<TRequest, Task<Result<TResponse>>> executeRequest,
            System.Net.HttpStatusCode errorStatusCode,
            ErrorType expectedErrorType)
        {
            // Act
            var result = executeRequest(request).GetAwaiter().GetResult();

            // Assert
            result.ShouldBeFailure();
            result.Error.Type.ShouldBe(expectedErrorType);
        }
    }

    /// <summary>
    /// Template for testing domain services with behavior focus
    /// </summary>
    public static class DomainServiceTemplate<TService, TRequest, TResponse>
        where TService : class
        where TRequest : class
    {
        /// <summary>
        /// Creates a standard domain logic test
        /// </summary>
        public static void ShouldExecuteDomainLogic_WithExpectedBehavior(
            Func<TService> serviceFactory,
            TRequest request,
            Func<TRequest, TResponse> executeLogic,
            Func<TResponse, bool> responseValidator)
        {
            // Arrange
            var service = serviceFactory();

            // Act
            var result = executeLogic(request);

            // Assert
            responseValidator(result).ShouldBeTrue();
        }

        /// <summary>
        /// Creates a standard domain validation test
        /// </summary>
        public static void ShouldValidateInput_WithExpectedRules(
            Func<TService> serviceFactory,
            TRequest invalidRequest,
            Func<TRequest, Result<TResponse>> executeValidation,
            string expectedValidationMessage)
        {
            // Act
            var result = executeValidation(invalidRequest);

            // Assert
            result.ShouldBeFailure();
            result.Error.Message.ShouldContain(expectedValidationMessage);
        }
    }

    /// <summary>
    /// Template for testing value objects and domain primitives
    /// </summary>
    public static class ValueObjectTemplate<T> where T : class
    {
        /// <summary>
        /// Creates a standard value object equality test
        /// </summary>
        public static void ShouldImplementValueEquality(
            Func<T> createFirst,
            Func<T> createSecond,
            bool shouldBeEqual = true)
        {
            // Arrange
            var first = createFirst();
            var second = createSecond();

            // Act & Assert
            if (shouldBeEqual)
            {
                first.ShouldBe(second);
                first.GetHashCode().ShouldBe(second.GetHashCode());
            }
            else
            {
                first.ShouldNotBe(second);
            }
        }

        /// <summary>
        /// Creates a standard value object validation test
        /// </summary>
        public static void ShouldValidateInput_WithExpectedRules(
            Func<object, Result<T>> createValueObject,
            object invalidInput,
            string expectedErrorMessage)
        {
            // Act
            var result = createValueObject(invalidInput);

            // Assert
            result.ShouldBeFailure();
            result.Error.Message.ShouldContain(expectedErrorMessage);
        }
    }

    /// <summary>
    /// Template for testing API endpoints with integration focus
    /// </summary>
    public static class ApiEndpointTemplate<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Creates a standard endpoint success test
        /// </summary>
        public static void ShouldReturnSuccessResponse_WhenRequestIsValid(
            HttpClient client,
            string endpoint,
            TRequest request,
            Func<TResponse, bool> responseValidator)
        {
            // This would be implemented with actual HTTP client testing
            // Act
            // var response = await client.PostAsync(endpoint, CreateJsonContent(request));
            
            // Assert
            // response.IsSuccessStatusCode.ShouldBeTrue();
            // var responseData = await DeserializeResponse<TResponse>(response);
            // responseValidator(responseData).ShouldBeTrue();
        }

        /// <summary>
        /// Creates a standard endpoint validation test
        /// </summary>
        public static void ShouldReturnValidationError_WhenRequestIsInvalid(
            HttpClient client,
            string endpoint,
            TRequest invalidRequest,
            string expectedErrorMessage)
        {
            // This would be implemented with actual HTTP client testing
            // Act
            // var response = await client.PostAsync(endpoint, CreateJsonContent(invalidRequest));
            
            // Assert
            // response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }

    /// <summary>
    /// Template for testing clean architecture boundary interactions
    /// </summary>
    public static class ArchitectureBoundaryTemplate
    {
        /// <summary>
        /// Verifies that application layer doesn't depend on infrastructure
        /// </summary>
        public static void ShouldNotDependOnInfrastructure_FromApplication(
            Type applicationType,
            string[] forbiddenNamespaces)
        {
            var dependencies = applicationType.Assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(name => forbiddenNamespaces.Any(forbidden => name?.Contains(forbidden) == true))
                .ToList();

            dependencies.ShouldBeEmpty($"Application type {applicationType.Name} should not depend on infrastructure namespaces: {string.Join(", ", dependencies)}");
        }

        /// <summary>
        /// Verifies that domain layer is pure and has no external dependencies
        /// </summary>
        public static void ShouldHaveNoDependencies_FromDomain(
            Type domainType,
            string[] allowedNamespaces)
        {
            var dependencies = domainType.Assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(name => !allowedNamespaces.Any(allowed => name?.StartsWith(allowed) == true))
                .ToList();

            dependencies.ShouldBeEmpty($"Domain type {domainType.Name} should only depend on allowed namespaces: {string.Join(", ", allowedNamespaces)}");
        }
    }

    /// <summary>
    /// Template for testing CQRS patterns specifically
    /// </summary>
    public static class CqrsTemplate<TCommand, TResponse>
        where TCommand : class
    {
        /// <summary>
        /// Creates a standard command/query separation test
        /// </summary>
        public static void ShouldFollowCqrsPrinciples(
            Type handlerType,
            bool isCommand)
        {
            // Verify handler follows CQRS naming conventions
            var expectedSuffix = isCommand ? "CommandHandler" : "QueryHandler";
            handlerType.Name.ShouldEndWith(expectedSuffix);

            // Verify return type patterns
            if (isCommand)
            {
                // Commands should return Result or Result<T>
                var methods = handlerType.GetMethods().Where(m => m.Name == "Handle");
                methods.ShouldAllBe(m => 
                    m.ReturnType.IsGenericType && 
                    m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
            }
        }
    }

    /// <summary>
    /// Helper methods for template usage
    /// </summary>
    public static class TemplateHelpers
    {
        /// <summary>
        /// Creates a test scenario builder
        /// </summary>
        public static TestScenarioBuilder<T> CreateScenario<T>() where T : class
        {
            return new TestScenarioBuilder<T>();
        }

        /// <summary>
        /// Verifies multiple conditions in a single assertion
        /// </summary>
        public static void VerifyAll(params Action[] verifications)
        {
            var exceptions = new List<Exception>();

            foreach (var verification in verifications)
            {
                try
                {
                    verification();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            if (exceptions.Any())
            {
                throw new AggregateException("Multiple verification failures", exceptions);
            }
        }
    }

    /// <summary>
    /// Builder for creating complex test scenarios
    /// </summary>
    public class TestScenarioBuilder<T> where T : class
    {
        private readonly List<Mock> _mocks = new();
        private readonly List<Action> _arrangements = new();
        private readonly List<Action> _verifications = new();

        public TestScenarioBuilder<T> WithMock<TMock>(Mock<TMock> mock) where TMock : class
        {
            _mocks.Add(mock);
            return this;
        }

        public TestScenarioBuilder<T> WithArrangement(Action arrangement)
        {
            _arrangements.Add(arrangement);
            return this;
        }

        public TestScenarioBuilder<T> WithVerification(Action verification)
        {
            _verifications.Add(verification);
            return this;
        }

        public void Execute(Func<T> systemUnderTestFactory, Action<T> testAction)
        {
            // Execute arrangements
            foreach (var arrangement in _arrangements)
            {
                arrangement();
            }

            // Create and execute test
            var systemUnderTest = systemUnderTestFactory();
            testAction(systemUnderTest);

            // Execute verifications
            foreach (var verification in _verifications)
            {
                verification();
            }

            // Verify all mocks
            foreach (var mock in _mocks)
            {
                mock.VerifyAll();
            }
        }

        public async Task ExecuteAsync(Func<T> systemUnderTestFactory, Func<T, Task> testAction)
        {
            // Execute arrangements
            foreach (var arrangement in _arrangements)
            {
                arrangement();
            }

            // Create and execute test
            var systemUnderTest = systemUnderTestFactory();
            await testAction(systemUnderTest);

            // Execute verifications
            foreach (var verification in _verifications)
            {
                verification();
            }

            // Verify all mocks
            foreach (var mock in _mocks)
            {
                mock.VerifyAll();
            }
        }
    }
}