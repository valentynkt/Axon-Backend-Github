using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Axon.Modules.Chat.Application.Abstractions;
using System.Text;
using System.Text.Json;

namespace Axon.Tests.Shared.TestBase;

/// <summary>
/// Base class for Integration tests providing WebApplicationFactory setup
/// </summary>
[TestFixture]
public abstract class IntegrationTestBase
{
    protected AxonWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Factory = new AxonWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }

    /// <summary>
    /// Creates JSON content for HTTP requests 
    /// </summary>
    protected static StringContent CreateJsonContent<T>(T data)
    {
        var json = JsonSerializer.Serialize(data);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// Deserializes HTTP response content to specified type
    /// </summary>
    protected static async Task<T?> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}

/// <summary>
/// Custom WebApplicationFactory for testing
/// </summary>
public class AxonWebApplicationFactory : WebApplicationFactory<object>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // Replace real AI client with mock
            services.RemoveAll<IAiClient>();
            services.AddScoped<IAiClient, MockAiClient>();
        });
    }
}

/// <summary>
/// Mock AI client for integration testing
/// </summary>
public class MockAiClient : IAiClient
{
    public Task<Result<Axon.Modules.Chat.Application.DTOs.AiResponse>> ProcessMessageAsync(
        Axon.Modules.Chat.Application.DTOs.AiRequest request, 
        CancellationToken cancellationToken = default)
    {
        var response = new Axon.Modules.Chat.Application.DTOs.AiResponse(
            Content: $"Mock response for: {request.Message}",
            ResponseId: Guid.NewGuid().ToString(),
            ToolExecutions: null);

        return Task.FromResult(Result<Axon.Modules.Chat.Application.DTOs.AiResponse>.Success(response));
    }
}

/// <summary>
/// Extension methods for service collection manipulation in tests
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection RemoveAll<T>(this IServiceCollection services)
    {
        var serviceDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(T)).ToList();
        foreach (var serviceDescriptor in serviceDescriptors)
        {
            services.Remove(serviceDescriptor);
        }
        return services;
    }
}