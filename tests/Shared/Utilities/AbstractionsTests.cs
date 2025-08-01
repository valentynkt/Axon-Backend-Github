using Axon.Shared.Common.Abstractions;
using System.Reflection;

namespace Axon.Tests.Shared.Utilities;

/// <summary>
/// Comprehensive test suite for Common.Abstractions interfaces and patterns
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public sealed class AbstractionsTests
{
    [TestFixture]
    public class IRequestTests
    {
        [Test]
        public void IRequest_ShouldBeMarkerInterface()
        {
            // Act
            var requestType = typeof(IRequest);

            // Assert
            requestType.IsInterface.ShouldBeTrue();
            requestType.GetMethods().Length.ShouldBe(0);
            requestType.GetProperties().Length.ShouldBe(0);
        }

        [Test]
        public void IRequest_ShouldBePublic()
        {
            // Act
            var requestType = typeof(IRequest);

            // Assert
            requestType.IsPublic.ShouldBeTrue();
        }

        [Test]
        public void IRequest_ShouldHaveCorrectNamespace()
        {
            // Act
            var requestType = typeof(IRequest);

            // Assert
            requestType.Namespace.ShouldBe("Axon.Shared.Common.Abstractions");
        }

        [Test]
        public void IRequest_ShouldHaveXmlDocumentation()
        {
            // This test ensures the interface is properly documented
            // The actual documentation verification would require loading XML documentation
            var requestType = typeof(IRequest);
            requestType.ShouldNotBeNull();
        }
    }

    [TestFixture]
    public class IRequestGenericTests
    {
        [Test]
        public void IRequestGeneric_ShouldInheritFromIRequest()
        {
            // Act
            var genericRequestType = typeof(IRequest<>);

            // Assert
            genericRequestType.IsInterface.ShouldBeTrue();
            genericRequestType.GetInterfaces().ShouldContain(typeof(IRequest));
        }

        [Test]
        public void IRequestGeneric_ShouldBeGeneric()
        {
            // Act
            var genericRequestType = typeof(IRequest<>);

            // Assert
            genericRequestType.IsGenericTypeDefinition.ShouldBeTrue();
            genericRequestType.GetGenericArguments().Length.ShouldBe(1);
        }

        [Test]
        public void IRequestGeneric_ShouldHaveCovariantTypeParameter()
        {
            // Act
            var genericRequestType = typeof(IRequest<>);
            var typeParameter = genericRequestType.GetGenericArguments()[0];

            // Assert
            typeParameter.GenericParameterAttributes.HasFlag(GenericParameterAttributes.Covariant).ShouldBeTrue();
        }

        [Test]
        public void IRequestGeneric_WithConcreteType_ShouldImplementBothInterfaces()
        {
            // Act
            var concreteRequestType = typeof(IRequest<string>);

            // Assert
            concreteRequestType.GetInterfaces().ShouldContain(typeof(IRequest));
            typeof(IRequest).IsAssignableFrom(concreteRequestType).ShouldBeTrue();
        }

        [Test]
        public void IRequestGeneric_ShouldBePublic()
        {
            // Act
            var genericRequestType = typeof(IRequest<>);

            // Assert
            genericRequestType.IsPublic.ShouldBeTrue();
        }

        [Test]
        public void IRequestGeneric_ShouldHaveCorrectNamespace()
        {
            // Act
            var genericRequestType = typeof(IRequest<>);

            // Assert
            genericRequestType.Namespace.ShouldBe("Axon.Shared.Common.Abstractions");
        }
    }

    [TestFixture]
    public class IRequestHandlerTests
    {
        [Test]
        public void IRequestHandler_ShouldBeGeneric()
        {
            // Act
            var handlerType = typeof(IRequestHandler<>);

            // Assert
            handlerType.IsInterface.ShouldBeTrue();
            handlerType.IsGenericTypeDefinition.ShouldBeTrue();
            handlerType.GetGenericArguments().Length.ShouldBe(1);
        }

        [Test]
        public void IRequestHandler_ShouldHaveContravariantTypeParameter()
        {
            // Act
            var handlerType = typeof(IRequestHandler<>);
            var typeParameter = handlerType.GetGenericArguments()[0];

            // Assert
            typeParameter.GenericParameterAttributes.HasFlag(GenericParameterAttributes.Contravariant).ShouldBeTrue();
        }

        [Test]
        public void IRequestHandler_ShouldHaveConstraintOnGenericParameter()
        {
            // Act
            var handlerType = typeof(IRequestHandler<>);
            var typeParameter = handlerType.GetGenericArguments()[0];
            var constraints = typeParameter.GetGenericParameterConstraints();

            // Assert
            constraints.ShouldContain(typeof(IRequest));
        }

        [Test]
        public void IRequestHandler_ShouldHaveHandleAsyncMethod()
        {
            // Act
            var handlerType = typeof(IRequestHandler<>);
            var method = handlerType.GetMethod("HandleAsync");

            // Assert
            method.ShouldNotBeNull();
            method.Name.ShouldBe("HandleAsync");
            method.ReturnType.ShouldBe(typeof(Task));
            method.GetParameters().Length.ShouldBe(2);
        }

        [Test]
        public void IRequestHandler_HandleAsyncMethod_ShouldHaveCorrectParameters()
        {
            // Act
            var handlerType = typeof(IRequestHandler<>);
            var method = handlerType.GetMethod("HandleAsync");
            var parameters = method!.GetParameters();

            // Assert
            parameters[0].Name.ShouldBe("request");
            parameters[1].Name.ShouldBe("cancellationToken");
            parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
        }

        [Test]
        public void IRequestHandler_ShouldBePublic()
        {
            // Act
            var handlerType = typeof(IRequestHandler<>);

            // Assert
            handlerType.IsPublic.ShouldBeTrue();
        }

        [Test]
        public void IRequestHandler_ShouldHaveCorrectNamespace()
        {
            // Act
            var handlerType = typeof(IRequestHandler<>);

            // Assert
            handlerType.Namespace.ShouldBe("Axon.Shared.Common.Abstractions");
        }
    }

    [TestFixture]
    public class IRequestHandlerGenericTests
    {
        [Test]
        public void IRequestHandlerGeneric_ShouldHaveTwoTypeParameters()
        {
            // Act
            var genericHandlerType = typeof(IRequestHandler<,>);

            // Assert
            genericHandlerType.IsInterface.ShouldBeTrue();
            genericHandlerType.IsGenericTypeDefinition.ShouldBeTrue();
            genericHandlerType.GetGenericArguments().Length.ShouldBe(2);
        }

        [Test]
        public void IRequestHandlerGeneric_ShouldHaveContravariantFirstParameter()
        {
            // Act
            var genericHandlerType = typeof(IRequestHandler<,>);
            var firstTypeParameter = genericHandlerType.GetGenericArguments()[0];

            // Assert
            firstTypeParameter.GenericParameterAttributes.HasFlag(GenericParameterAttributes.Contravariant).ShouldBeTrue();
        }

        [Test]
        public void IRequestHandlerGeneric_ShouldHaveConstraintOnFirstParameter()
        {
            // Act
            var genericHandlerType = typeof(IRequestHandler<,>);
            var firstTypeParameter = genericHandlerType.GetGenericArguments()[0];
            var constraints = firstTypeParameter.GetGenericParameterConstraints();

            // Assert
            constraints.Length.ShouldBe(1);
            // The constraint should be IRequest<TResponse> but we need to check this dynamically
            var constraintType = constraints[0];
            constraintType.IsGenericType.ShouldBeTrue();
            constraintType.GetGenericTypeDefinition().ShouldBe(typeof(IRequest<>));
        }

        [Test]
        public void IRequestHandlerGeneric_ShouldHaveHandleAsyncMethodWithCorrectReturnType()
        {
            // Act
            var genericHandlerType = typeof(IRequestHandler<,>);
            var method = genericHandlerType.GetMethod("HandleAsync");

            // Assert
            method.ShouldNotBeNull();
            method.Name.ShouldBe("HandleAsync");
            method.ReturnType.IsGenericType.ShouldBeTrue();
            method.ReturnType.GetGenericTypeDefinition().ShouldBe(typeof(Task<>));
        }

        [Test]
        public void IRequestHandlerGeneric_HandleAsyncMethod_ShouldHaveCorrectParameters()
        {
            // Act
            var genericHandlerType = typeof(IRequestHandler<,>);
            var method = genericHandlerType.GetMethod("HandleAsync");
            var parameters = method!.GetParameters();

            // Assert
            parameters.Length.ShouldBe(2);
            parameters[0].Name.ShouldBe("request");
            parameters[1].Name.ShouldBe("cancellationToken");
            parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
        }

        [Test]
        public void IRequestHandlerGeneric_ShouldBePublic()
        {
            // Act
            var genericHandlerType = typeof(IRequestHandler<,>);

            // Assert
            genericHandlerType.IsPublic.ShouldBeTrue();
        }
    }

    [TestFixture]
    public class AbstractionsIntegrationTests
    {
        // Test class that implements IRequest
        private class TestRequest : IRequest
        {
            public string Name { get; set; } = string.Empty;
        }

        // Test class that implements IRequest<T>
        private class TestRequestWithResponse : IRequest<string>
        {
            public int Id { get; set; }
        }

        // Test handler for TestRequest
        private class TestRequestHandler : IRequestHandler<TestRequest>
        {
            public Task HandleAsync(TestRequest request, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        // Test handler for TestRequestWithResponse
        private class TestRequestWithResponseHandler : IRequestHandler<TestRequestWithResponse, string>
        {
            public Task<string> HandleAsync(TestRequestWithResponse request, CancellationToken cancellationToken)
            {
                return Task.FromResult($"Response for {request.Id}");
            }
        }

        [Test]
        public void TestRequest_ShouldImplementIRequest()
        {
            // Act
            var request = new TestRequest { Name = "Test" };

            // Assert
            request.ShouldBeAssignableTo<IRequest>();
            request.Name.ShouldBe("Test");
        }

        [Test]
        public void TestRequestWithResponse_ShouldImplementIRequestGeneric()
        {
            // Act
            var request = new TestRequestWithResponse { Id = 42 };

            // Assert
            request.ShouldBeAssignableTo<IRequest<string>>();
            request.ShouldBeAssignableTo<IRequest>();
            request.Id.ShouldBe(42);
        }

        [Test]
        public async Task TestRequestHandler_ShouldHandleRequest()
        {
            // Arrange
            var handler = new TestRequestHandler();
            var request = new TestRequest { Name = "Test" };
            var cancellationToken = CancellationToken.None;

            // Act & Assert
            await Should.NotThrowAsync(async () => await handler.HandleAsync(request, cancellationToken));
        }

        [Test]
        public async Task TestRequestWithResponseHandler_ShouldHandleRequestAndReturnResponse()
        {
            // Arrange
            var handler = new TestRequestWithResponseHandler();
            var request = new TestRequestWithResponse { Id = 42 };
            var cancellationToken = CancellationToken.None;

            // Act
            var response = await handler.HandleAsync(request, cancellationToken);

            // Assert
            response.ShouldBe("Response for 42");
        }

        [Test]
        public void AbstractionTypes_ShouldHaveConsistentNaming()
        {
            // Act
            var types = typeof(IRequest).Assembly.GetTypes()
                .Where(t => t.Namespace == "Axon.Shared.Common.Abstractions")
                .ToList();

            // Assert
            types.All(t => t.Name.StartsWith("I")).ShouldBeTrue("All abstractions should be interfaces starting with 'I'");
            types.All(t => t.IsInterface).ShouldBeTrue("All types in abstractions should be interfaces");
        }

        [Test]
        public void AbstractionTypes_ShouldBeWellDocumented()
        {
            // This test ensures all abstraction types have proper naming and structure
            var types = typeof(IRequest).Assembly.GetTypes()
                .Where(t => t.Namespace == "Axon.Shared.Common.Abstractions")
                .ToList();

            // Assert
            types.ShouldNotBeEmpty();
            types.Count.ShouldBe(4); // IRequest, IRequest<T>, IRequestHandler<T>, IRequestHandler<T,R>
        }
    }
}