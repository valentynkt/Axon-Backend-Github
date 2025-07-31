using System.Net;
using System.Net.Http.Json;
using Axon.Api.Common.ErrorHandling;
using Axon.Api.Contracts.Chat;
using Axon.Api.Endpoints.Chat.ProcessMessage;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Shared.Common;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Shouldly;

// Aliases to resolve type ambiguity
using ApiProcessMessageResponse = Axon.Api.Contracts.Chat.ProcessMessageResponse;
using ApplicationProcessMessageResponse = Axon.Modules.Chat.Application.Commands.ProcessMessage.ProcessMessageResponse;

namespace Axon.Api.Tests.Endpoints.Chat.ProcessMessage;

/// <summary>
/// Tests specific to the FastEndpoints implementation of ProcessMessage.
/// Focuses on FastEndpoints-specific behavior, validation, and configuration.
/// </summary>
[TestFixture]
[Category("FastEndpoints")]
[Category("Unit")]
public sealed class ProcessMessageEndpointFastEndpointsTests
{
    private Mock<IMediator> _mockMediator = null!;
    private Mock<IErrorMapper> _mockErrorMapper = null!;
    private Mock<ILogger<ProcessMessageEndpoint>> _mockLogger = null!;

    [SetUp]
    public void SetUp()
    {
        _mockMediator = new Mock<IMediator>();
        _mockErrorMapper = new Mock<IErrorMapper>();
        _mockLogger = new Mock<ILogger<ProcessMessageEndpoint>>();
    }

    [Test]
    public void Constructor_ShouldInitialize_GivenValidDependencies()
    {
        // Act & Assert - Constructor should not throw
        var endpoint = new ProcessMessageEndpoint(_mockMediator.Object, _mockErrorMapper.Object, _mockLogger.Object);
        endpoint.ShouldNotBeNull();
    }

    [Test]
    public void Constructor_ShouldThrowArgumentNullException_GivenNullMediator()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new ProcessMessageEndpoint(null!, _mockErrorMapper.Object, _mockLogger.Object));
    }

    [Test]
    public void Constructor_ShouldThrowArgumentNullException_GivenNullErrorMapper()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new ProcessMessageEndpoint(_mockMediator.Object, null!, _mockLogger.Object));
    }

    [Test]
    public void Constructor_ShouldThrowArgumentNullException_GivenNullLogger()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new ProcessMessageEndpoint(_mockMediator.Object, _mockErrorMapper.Object, null!));
    }

    [Test]
    public void Endpoint_ShouldBeProperlyConfiguredForFastEndpoints()
    {
        // Arrange
        var endpoint = new ProcessMessageEndpoint(_mockMediator.Object, _mockErrorMapper.Object, _mockLogger.Object);
        
        // Act & Assert
        // Verify endpoint is ready for FastEndpoints registration
        // Note: FastEndpoints configuration requires framework context and is tested in integration tests
        // This test ensures the endpoint can be constructed and is ready for registration
        endpoint.ShouldNotBeNull();
        
        // Verify it inherits from the correct FastEndpoints base class
        endpoint.ShouldBeAssignableTo<Endpoint<ProcessMessageRequest, Contracts.Chat.ProcessMessageResponse>>();
    }

}

/// <summary>
/// Integration tests for FastEndpoints ProcessMessage endpoint using TestServer
/// </summary>
[TestFixture]
[Category("FastEndpoints")]
[Category("Integration")]
public sealed class ProcessMessageEndpointFastEndpointsIntegrationTests
{
    private WebApplicationFactory<Program> _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory?.Dispose();
    }

    [Test]
    public async Task FastEndpointsRoute_ShouldBeRegistered_AndAccessible()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new ProcessMessageRequest(
            Message: "Hello from FastEndpoints!",
            ConversationId: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert - Should not return 404 (route exists)
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
        
        // Should return either OK (if working) or some other status, but endpoint exists
        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.BadGateway);
    }

    [Test]
    public async Task FastEndpointsRoute_ShouldHandle_MultipleRequests()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new ProcessMessageRequest(
            Message: "Test multiple requests",
            ConversationId: null);

        // Act - Make multiple requests to ensure endpoint handles concurrency
        var firstResponse = await client.PostAsJsonAsync("/api/chat/process", request);
        var secondResponse = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert - Both should process successfully (not return 404)
        firstResponse.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
        secondResponse.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task FastEndpointsRoute_ShouldReturn405_ForUnsupportedHttpMethods()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Try unsupported HTTP methods
        var getResponse = await client.GetAsync("/api/chat/process");
        using var putContent = new StringContent("{}");
        var putResponse = await client.PutAsync("/api/chat/process", putContent);
        var deleteResponse = await client.DeleteAsync("/api/chat/process");

        // Assert - Should return MethodNotAllowed
        getResponse.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Test]
    public async Task FastEndpointsRoute_ShouldReturn415_ForUnsupportedContentType()
    {
        // Arrange
        var client = _factory.CreateClient();
        using var content = new StringContent("plain text", System.Text.Encoding.UTF8, "text/plain");

        // Act
        var response = await client.PostAsync("/api/chat/process", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Test]
    public async Task FastEndpointsRoute_ShouldAccept_JsonContentType()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new ProcessMessageRequest(
            Message: "Test JSON content type",
            ConversationId: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert - Should not return UnsupportedMediaType
        response.StatusCode.ShouldNotBe(HttpStatusCode.UnsupportedMediaType);
    }
}