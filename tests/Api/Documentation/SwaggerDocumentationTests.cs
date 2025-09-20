using System.Text.Json;
using Axon.Api.Swagger;
using Axon.Api.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using NUnit.Framework;
using Shouldly;

namespace Axon.Api.Documentation.Tests;

/// <summary>
/// Integration tests for OpenAPI specification generation and Swagger UI accessibility.
/// Verifies complete API documentation with authentication, error schemas, and examples.
/// </summary>
[TestFixture]
public class SwaggerDocumentationTests : IDisposable
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new TestWebApplicationFactory()
            .WithEnvironment("Development");

        _client = _factory.CreateClient();
    }

    [Test]
    public async Task SwaggerJson_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    [Test]
    public async Task SwaggerUI_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/swagger");

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Swagger UI");
        content.ShouldContain("Axon Identity Service API");
    }

    [Test]
    public async Task OpenApiSpecification_ShouldHaveBearerAuthentication()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var jsonString = await response.Content.ReadAsStringAsync();
        
        using var jsonDocument = JsonDocument.Parse(jsonString);
        var root = jsonDocument.RootElement;

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        
        // Check security schemes
        root.TryGetProperty("components", out var components).ShouldBeTrue();
        components.TryGetProperty("securitySchemes", out var securitySchemes).ShouldBeTrue();
        securitySchemes.TryGetProperty("bearerAuth", out var bearerAuth).ShouldBeTrue();
        
        bearerAuth.TryGetProperty("type", out var type).ShouldBeTrue();
        type.GetString().ShouldBe("http");
        
        bearerAuth.TryGetProperty("scheme", out var scheme).ShouldBeTrue();
        scheme.GetString().ShouldBe("bearer");
        
        bearerAuth.TryGetProperty("bearerFormat", out var bearerFormat).ShouldBeTrue();
        bearerFormat.GetString().ShouldBe("JWT");
    }

    [Test]
    public async Task OpenApiSpecification_ShouldHaveApiErrorSchema()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var jsonString = await response.Content.ReadAsStringAsync();
        
        using var jsonDocument = JsonDocument.Parse(jsonString);
        var root = jsonDocument.RootElement;

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        
        // Check if ApiError schema is defined
        root.TryGetProperty("components", out var components).ShouldBeTrue();
        components.TryGetProperty("schemas", out var schemas).ShouldBeTrue();
        
        // The schema might be referenced differently, so let's check for error-related schemas
        var schemaNames = schemas.EnumerateObject().Select(p => p.Name).ToList();
        schemaNames.ShouldContain(name => name.Contains("ApiError") || name.Contains("Error"), 
            customMessage: "Should contain ApiError or similar error schema");
    }

    [Test]
    public async Task OpenApiSpecification_ShouldHaveProperErrorResponses()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var jsonString = await response.Content.ReadAsStringAsync();
        
        using var jsonDocument = JsonDocument.Parse(jsonString);
        var root = jsonDocument.RootElement;

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        
        // Check paths for error responses
        root.TryGetProperty("paths", out var paths).ShouldBeTrue();
        
        var foundErrorResponses = false;
        foreach (var pathProperty in paths.EnumerateObject())
        {
            foreach (var methodProperty in pathProperty.Value.EnumerateObject())
            {
                if (methodProperty.Value.TryGetProperty("responses", out var responses))
                {
                    var responseKeys = responses.EnumerateObject().Select(r => r.Name).ToList();
                    
                    // Check for common error status codes
                    if (responseKeys.Any(k => k is "400" or "401" or "409" or "422" or "429" or "500"))
                    {
                        foundErrorResponses = true;
                        break;
                    }
                }
            }
            if (foundErrorResponses) break;
        }
        
        foundErrorResponses.ShouldBeTrue("Should have error responses defined for endpoints");
    }

    [Test]
    public async Task OpenApiSpecification_ShouldHaveApiInformation()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var jsonString = await response.Content.ReadAsStringAsync();
        
        using var jsonDocument = JsonDocument.Parse(jsonString);
        var root = jsonDocument.RootElement;

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        
        // Check API info
        root.TryGetProperty("info", out var info).ShouldBeTrue();
        
        info.TryGetProperty("title", out var title).ShouldBeTrue();
        title.GetString()?.ShouldContain("Axon");
        
        info.TryGetProperty("version", out var version).ShouldBeTrue();
        version.GetString().ShouldBe("v1");
        
        info.TryGetProperty("description", out var description).ShouldBeTrue();
        var descriptionText = description.GetString();
        descriptionText?.ShouldContain("Authentication");
        descriptionText?.ShouldContain("Rate Limiting");
        descriptionText?.ShouldContain("Error Handling");
    }

    [Test]
    public async Task OpenApiSpecification_ShouldParseWithoutErrors()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var jsonString = await response.Content.ReadAsStringAsync();
        
        var reader = new OpenApiStringReader();
        var openApiDocument = reader.Read(jsonString, out var diagnostic);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        openApiDocument.ShouldNotBeNull();
        diagnostic.Errors.ShouldBeEmpty($"OpenAPI document has errors: {string.Join(", ", diagnostic.Errors.Select(e => e.Message))}");
    }

    [Test]
    public async Task OpenApiSpecification_ShouldHaveSecurityRequirements()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var jsonString = await response.Content.ReadAsStringAsync();
        
        using var jsonDocument = JsonDocument.Parse(jsonString);
        var root = jsonDocument.RootElement;

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        
        // Check for global security requirements
        if (root.TryGetProperty("security", out var globalSecurity))
        {
            globalSecurity.GetArrayLength().ShouldBeGreaterThan(0);
        }
        
        // Or check individual endpoints for security requirements
        root.TryGetProperty("paths", out var paths).ShouldBeTrue();
        var foundSecuredEndpoint = false;
        
        foreach (var pathProperty in paths.EnumerateObject())
        {
            foreach (var methodProperty in pathProperty.Value.EnumerateObject())
            {
                if (methodProperty.Value.TryGetProperty("security", out var endpointSecurity))
                {
                    foundSecuredEndpoint = true;
                    endpointSecurity.GetArrayLength().ShouldBeGreaterThan(0);
                    break;
                }
            }
            if (foundSecuredEndpoint) break;
        }
        
        // Either global security or endpoint-level security should be present
        (globalSecurity.ValueKind != JsonValueKind.Undefined || foundSecuredEndpoint)
            .ShouldBeTrue("Should have security requirements defined either globally or per endpoint");
    }

    [Test]
    public async Task ScalarUI_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/scalar");

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("scalar"); // Scalar UI should be present
    }

    [Test] 
    public async Task DocumentationEndpoints_ShouldNotAffectOtherModules()
    {
        // This test verifies IV1: API discovery unaffected for other modules
        
        // Act - Try to access potential other module endpoints
        var healthResponse = await _client.GetAsync("/health");
        var chatResponse = await _client.GetAsync("/api/v1/chat/conversations");
        
        // Assert
        // Health endpoint should exist and be accessible
        healthResponse.IsSuccessStatusCode.ShouldBeTrue();
        
        // Chat endpoint may not be implemented yet, but should not return 500 due to documentation conflicts
        chatResponse.StatusCode.ShouldNotBe(System.Net.HttpStatusCode.InternalServerError);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}