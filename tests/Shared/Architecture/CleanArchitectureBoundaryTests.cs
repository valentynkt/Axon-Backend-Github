using System.Reflection;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Domain.ValueObjects;
// using Axon.Tests.Shared.Tests.Extensions; // Removed to fix circular dependency
using Axon.Tests.Shared.Mocks;
using Axon.Tests.Shared.TestBase;
using Axon.Tests.Shared.TestDoubles;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace Axon.Tests.Shared.Architecture;

/// <summary>
/// Clean Architecture boundary tests using London School TDD principles with layered mocking
/// </summary>
[TestFixture]
[Category("Architecture")]
[Category("LondonSchool")]
[Category("BoundaryTest")]
public sealed class CleanArchitectureBoundaryTests : LondonSchoolTestBase
{
    /// <summary>
    /// Tests that verify application layer boundaries through interaction patterns
    /// </summary>
    [TestFixture]
    public class ApplicationLayerBoundaryTests : LondonSchoolTestBase
    {
        [Test]
        [BehaviorTest]
        public void ApplicationLayer_ShouldOnlyDependOnAbstractions_NotConcreteInfrastructure()
        {
            // Arrange - Get all application layer types
            var applicationAssembly = typeof(ProcessMessageHandler).Assembly;
            var applicationTypes = applicationAssembly.GetTypes()
                .Where(t => t.Namespace?.Contains("Application") == true)
                .ToList();

            // Act & Assert - Verify dependencies
            foreach (var type in applicationTypes)
            {
                var dependencies = GetTypeDependencies(type);
                
                // Should not depend on Infrastructure concrete types
                var infrastructureDependencies = dependencies
                    .Where(d => d.Namespace?.Contains("Infrastructure") == true && 
                               !d.IsInterface && !d.IsAbstract)
                    .ToList();

                infrastructureDependencies.ShouldBeEmpty(
                    $"Application type {type.Name} should not depend on concrete infrastructure types: {string.Join(", ", infrastructureDependencies.Select(d => d.Name))}");

                // Should depend on abstractions
                var abstractionDependencies = dependencies
                    .Where(d => d.IsInterface || d.IsAbstract)
                    .ToList();

                if (type.IsClass && !type.IsAbstract)
                {
                    abstractionDependencies.ShouldNotBeEmpty(
                        $"Application type {type.Name} should depend on abstractions");
                }
            }
        }

        [Test]
        [InteractionTest]
        public async Task ApplicationHandler_ShouldInteractOnlyThroughAbstractions_WithInfrastructure()
        {
            // Arrange - Create handler with all dependencies as abstractions
            var aiClientMock = CreateStrictMock<IAiClient>();
            var mcpResolverMock = CreateStrictMock<IMcpServerResolver>();
            var loggerMock = CreateLooseMock<ILogger<ProcessMessageHandler>>();

            var handler = new ProcessMessageHandler(
                aiClientMock.Object, 
                mcpResolverMock.Object, 
                loggerMock.Object);

            var command = new ProcessMessageCommand("Test boundary interaction");

            // Setup abstraction behavior
            CqrsContractMocks.SetupMcpResolverBehavior(mcpResolverMock, 
                new List<Axon.Modules.Chat.Application.DTOs.McpServerConfig>().AsReadOnly());
            
            CqrsContractMocks.SetupAiClientBehavior(aiClientMock, 
                new Axon.Modules.Chat.Application.DTOs.AiResponse("Boundary test response", "boundary-123", null));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert - Verify interactions only through abstractions
            result.ShouldBeSuccess();
            
            // Verify abstraction interactions
            CqrsContractMocks.VerifyMcpResolverInteraction(mcpResolverMock, Times.Once);
            CqrsContractMocks.VerifyAiClientInteraction(aiClientMock, req => true, Times.Once);
        }

        [Test]
        [ContractTest]
        public void ApplicationAbstractions_ShouldNotLeakInfrastructureDetails()
        {
            // Arrange - Get all application abstractions
            var applicationAssembly = typeof(IAiClient).Assembly;
            var abstractionTypes = applicationAssembly.GetTypes()
                .Where(t => t.IsInterface && t.Namespace?.Contains("Application.Abstractions") == true)
                .ToList();

            // Act & Assert
            foreach (var abstractionType in abstractionTypes)
            {
                var methods = abstractionType.GetMethods();
                
                foreach (var method in methods)
                {
                    // Parameters should not expose infrastructure types
                    var parameters = method.GetParameters();
                    foreach (var parameter in parameters)
                    {
                        parameter.ParameterType.Namespace?.Contains("Infrastructure").ShouldBeFalse(
                            $"Abstraction {abstractionType.Name}.{method.Name} parameter {parameter.Name} should not expose infrastructure type {parameter.ParameterType.Name}");
                    }

                    // Return type should not expose infrastructure types
                    var returnType = method.ReturnType;
                    if (returnType.IsGenericType)
                    {
                        var genericArguments = returnType.GetGenericArguments();
                        foreach (var genericArg in genericArguments)
                        {
                            genericArg.Namespace?.Contains("Infrastructure").ShouldBeFalse(
                                $"Abstraction {abstractionType.Name}.{method.Name} return type should not expose infrastructure type {genericArg.Name}");
                        }
                    }
                    else
                    {
                        returnType.Namespace?.Contains("Infrastructure").ShouldBeFalse(
                            $"Abstraction {abstractionType.Name}.{method.Name} return type should not expose infrastructure type {returnType.Name}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tests that verify domain layer boundaries and isolation
    /// </summary>
    [TestFixture]
    public class DomainLayerBoundaryTests : LondonSchoolTestBase
    {
        [Test]
        [ContractTest]
        public void DomainLayer_ShouldHaveNoDependencies_OnOuterLayers()
        {
            // Arrange - Get all domain types
            var domainAssembly = typeof(ConversationId).Assembly;
            var domainTypes = domainAssembly.GetTypes()
                .Where(t => t.Namespace?.Contains("Domain") == true)
                .ToList();

            var forbiddenNamespaces = new[] { "Application", "Infrastructure", "Api" };

            // Act & Assert
            foreach (var domainType in domainTypes)
            {
                var dependencies = GetTypeDependencies(domainType);
                var forbiddenDependencies = dependencies
                    .Where(d => forbiddenNamespaces.Any(forbidden => d.Namespace?.Contains(forbidden) == true))
                    .ToList();

                forbiddenDependencies.ShouldBeEmpty(
                    $"Domain type {domainType.Name} should not depend on outer layer types: {string.Join(", ", forbiddenDependencies.Select(d => d.Name))}");
            }
        }

        [Test]
        [BehaviorTest]
        public void DomainValueObjects_ShouldFollowValueObjectPattern_WithProperBehavior()
        {
            // Arrange - Test ConversationId as example
            var id1 = ConversationId.New();
            var id2 = ConversationId.New();
            var id3 = ConversationId.From(id1.Value);

            // Act & Assert - Value equality behavior
            id1.ShouldNotBe(id2); // Different values
            id1.ShouldBe(id3); // Same value
            id1.GetHashCode().ShouldBe(id3.GetHashCode()); // Same hash code
        }

        [Test]
        [InteractionTest]
        public void DomainTypes_ShouldNotDependOnMocking_OrTestingFrameworks()
        {
            // Arrange - Get domain assembly
            var domainAssembly = typeof(ConversationId).Assembly;
            var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

            var testingFrameworks = new[] { "Moq", "NUnit", "xUnit", "MSTest" };

            // Act & Assert
            var testingDependencies = referencedAssemblies
                .Where(a => testingFrameworks.Any(framework => a.Name?.Contains(framework) == true))
                .ToList();

            testingDependencies.ShouldBeEmpty(
                $"Domain assembly should not reference testing frameworks: {string.Join(", ", testingDependencies.Select(d => d.Name))}");
        }
    }

    /// <summary>
    /// Tests that verify infrastructure layer boundaries and adapter patterns
    /// </summary>
    [TestFixture]
    public class InfrastructureLayerBoundaryTests : LondonSchoolTestBase
    {
        [Test]
        [InteractionTest]
        public async Task InfrastructureAdapter_ShouldImplementApplicationAbstraction_WithCorrectBehavior()
        {
            // Arrange - Create infrastructure adapter with mocked dependencies
            var httpHandlerMock = InfrastructureContractMocks.CreateHttpMessageHandlerMock();
            var loggerMock = CreateLooseMock<ILogger<OpenAiClient>>();
            
            var options = new Axon.Modules.Chat.Infrastructure.Ai.OpenAiOptions
            {
                ApiKey = "test-key",
                Model = "gpt-4",
                TimeoutSeconds = 30,
                McpEnabled = true
            };
            var optionsMock = InfrastructureContractMocks.CreateOptionsMock(options);

            using var httpClient = new HttpClient(httpHandlerMock.Object);
            var adapter = new OpenAiClient(httpClient, optionsMock.Object, loggerMock.Object);

            // Verify adapter implements application abstraction
            (adapter is IAiClient).ShouldBeTrue("Infrastructure adapter should implement application abstraction");

            var request = new Axon.Modules.Chat.Application.DTOs.AiRequest("Test", null, null);
            
            InfrastructureContractMocks.SetupOpenAiResponse(httpHandlerMock, "test-id", "Test response");

            // Act - Use adapter through abstraction
            IAiClient abstractionReference = adapter;
            var result = await abstractionReference.ProcessMessageAsync(request, CancellationToken.None);

            // Assert - Verify behavior through abstraction
            result.ShouldBeSuccess();
            InfrastructureContractMocks.VerifyHttpRequest(httpHandlerMock, "api.openai.com", HttpMethod.Post, Times.Once);
        }

        [Test]
        [ContractTest]
        public void InfrastructureAdapters_ShouldNotExposeThirdPartyTypes_ThroughAbstractions()
        {
            // Arrange - Get infrastructure types that implement application abstractions
            var infrastructureAssembly = typeof(OpenAiClient).Assembly;
            var adapterTypes = infrastructureAssembly.GetTypes()
                .Where(t => t.GetInterfaces().Any(i => i.Assembly != infrastructureAssembly))
                .ToList();

            // Act & Assert
            foreach (var adapterType in adapterTypes)
            {
                var publicMethods = adapterType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName) // Exclude properties, constructors, etc.
                    .ToList();

                foreach (var method in publicMethods)
                {
                    // Check parameters
                    foreach (var parameter in method.GetParameters())
                    {
                        VerifyTypeDoesNotExposeThirdParty(parameter.ParameterType, 
                            $"{adapterType.Name}.{method.Name} parameter {parameter.Name}");
                    }

                    // Check return type
                    VerifyTypeDoesNotExposeThirdParty(method.ReturnType, 
                        $"{adapterType.Name}.{method.Name} return type");
                }
            }
        }

        [Test]
        [BehaviorTest]
        public void InfrastructureConfiguration_ShouldBeInjectable_ThroughAbstractions()
        {
            // Arrange - Verify configuration types are properly abstracted
            var infrastructureAssembly = typeof(OpenAiClient).Assembly;
            var configurationTypes = infrastructureAssembly.GetTypes()
                .Where(t => t.Name.EndsWith("Options") || t.Name.EndsWith("Configuration"))
                .ToList();

            // Act & Assert
            foreach (var configurationType in configurationTypes)
            {
                // Configuration should be injectable (have public constructor or be record)
                var hasPublicConstructor = configurationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Any();
                var isRecord = configurationType.GetMethods().Any(m => m.Name == "<Clone>$");

                (hasPublicConstructor || isRecord).ShouldBeTrue(
                    $"Configuration type {configurationType.Name} should be injectable");

                // Configuration should not depend on domain types inappropriately
                var properties = configurationType.GetProperties();
                foreach (var property in properties)
                {
                    if (property.PropertyType.Namespace?.Contains("Domain") == true)
                    {
                        // Domain dependencies in configuration should be carefully considered
                        TestContext.WriteLine($"Configuration {configurationType.Name} depends on domain type {property.PropertyType.Name} - verify this is appropriate");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Integration tests that verify proper layering through mock collaborations
    /// </summary>
    [TestFixture]
    public class LayerIntegrationTests : LondonSchoolTestBase
    {
        [Test]
        [InteractionTest]
        public async Task FullStack_ShouldFollowDependencyInversion_ThroughAllLayers()
        {
            // Arrange - Create full stack with mocked external dependencies
            var scenario = InfrastructureContractMocks.CreateInfrastructureScenario()
                .WithSuccessfulHttpResponse("integration-123", "Full stack response");

            using var httpClient = scenario.CreateHttpClient();
            
            var options = new Axon.Modules.Chat.Infrastructure.Ai.OpenAiOptions
            {
                ApiKey = "integration-key",
                Model = "gpt-4",
                TimeoutSeconds = 30,
                McpEnabled = false
            };
            var optionsMock = InfrastructureContractMocks.CreateOptionsMock(options);
            var loggerMock = CreateLooseMock<ILogger<OpenAiClient>>();

            // Infrastructure layer
            var aiClient = new OpenAiClient(httpClient, optionsMock.Object, loggerMock.Object);

            // Application layer with injected infrastructure
            var mcpResolverMock = CreateStrictMock<IMcpServerResolver>();
            var handlerLoggerMock = CreateLooseMock<ILogger<ProcessMessageHandler>>();
            
            CqrsContractMocks.SetupMcpResolverBehavior(mcpResolverMock, 
                new List<Axon.Modules.Chat.Application.DTOs.McpServerConfig>().AsReadOnly());

            var handler = new ProcessMessageHandler(aiClient, mcpResolverMock.Object, handlerLoggerMock.Object);

            var command = new ProcessMessageCommand("Full stack test");

            // Act - Execute through all layers
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert - Verify layered interaction
            result.ShouldBeSuccess();
            result.Value.Response.ShouldBe("Full stack response");
            result.Value.ConversationId.ShouldBe("integration-123");

            // Verify interactions flowed through abstractions
            scenario.VerifyAllBehaviors();
            CqrsContractMocks.VerifyMcpResolverInteraction(mcpResolverMock, Times.Once);
        }

        [Test]
        [BehaviorTest]
        public async Task ErrorHandling_ShouldFlowCorrectly_ThroughArchitecturalLayers()
        {
            // Arrange - Setup error scenario at infrastructure level
            var scenario = InfrastructureContractMocks.CreateInfrastructureScenario()
                .WithHttpFailure(System.Net.HttpStatusCode.ServiceUnavailable, "Service down");

            using var httpClient = scenario.CreateHttpClient();
            
            var options = new Axon.Modules.Chat.Infrastructure.Ai.OpenAiOptions
            {
                ApiKey = "error-key",
                Model = "gpt-4",
                TimeoutSeconds = 30
            };
            var optionsMock = InfrastructureContractMocks.CreateOptionsMock(options);
            var loggerMock = CreateLooseMock<ILogger<OpenAiClient>>();

            var aiClient = new OpenAiClient(httpClient, optionsMock.Object, loggerMock.Object);

            var mcpResolverMock = CreateStrictMock<IMcpServerResolver>();
            var handlerLoggerMock = CreateLooseMock<ILogger<ProcessMessageHandler>>();
            
            CqrsContractMocks.SetupMcpResolverBehavior(mcpResolverMock, 
                new List<Axon.Modules.Chat.Application.DTOs.McpServerConfig>().AsReadOnly());

            var handler = new ProcessMessageHandler(aiClient, mcpResolverMock.Object, handlerLoggerMock.Object);

            var command = new ProcessMessageCommand("Error test");

            // Act - Execute error scenario
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert - Verify error flows correctly through layers
            result.ShouldBeFailure();
            result.Error.Type.ShouldBe(Axon.Shared.Common.ErrorType.ExternalService);

            // Verify error handling logged appropriately at each layer
            InfrastructureContractMocks.VerifyInfrastructureLogging(
                loggerMock, LogLevel.Error, "Failed to process message", Times.AtLeastOnce);
        }
    }

    #region Helper Methods

    /// <summary>
    /// Gets all dependencies of a type through reflection
    /// </summary>
    private static IEnumerable<Type> GetTypeDependencies(Type type)
    {
        var dependencies = new HashSet<Type>();

        // Constructor dependencies
        var constructors = type.GetConstructors();
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            foreach (var parameter in parameters)
            {
                dependencies.Add(parameter.ParameterType);
            }
        }

        // Property dependencies
        var properties = type.GetProperties();
        foreach (var property in properties)
        {
            dependencies.Add(property.PropertyType);
        }

        // Method dependencies
        var methods = type.GetMethods();
        foreach (var method in methods)
        {
            dependencies.Add(method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                dependencies.Add(parameter.ParameterType);
            }
        }

        return dependencies.Where(d => d.Assembly != typeof(object).Assembly); // Exclude system types
    }

    /// <summary>
    /// Verifies that a type doesn't expose third-party library types
    /// </summary>
    private static void VerifyTypeDoesNotExposeThirdParty(Type type, string context)
    {
        var thirdPartyNamespaces = new[] { "Newtonsoft", "System.Net.Http", "Microsoft.Extensions.Http" };
        
        if (type.IsGenericType)
        {
            foreach (var genericArg in type.GetGenericArguments())
            {
                VerifyTypeDoesNotExposeThirdParty(genericArg, context);
            }
        }

        var exposesThirdParty = thirdPartyNamespaces.Any(ns => type.Namespace?.StartsWith(ns) == true);
        exposesThirdParty.ShouldBeFalse($"{context} should not expose third-party type {type.Name}");
    }

    #endregion
}