using Axon.Api.Tests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using Shouldly;
using System.Net;

namespace Axon.Api.Tests;

[TestFixture]
public class SmokeTests : IDisposable
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task GET_Me_WithoutAuth_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task POST_Exchange_WithoutJWT_Returns401()
    {
        // Act
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/auth/exchange", content);

        // Assert - Endpoint requires Bearer token for authentication, returns 401
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task HealthCheck_Returns200()
    {
        // Act
        var response = await _client.GetAsync("/health");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}