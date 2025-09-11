using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Axon.Api.Contracts.V1.Identity.Webhooks;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using Shouldly;

namespace Axon.Api.Tests.Endpoints.V1.Webhooks.Dynamic;

[TestFixture]
public class ProcessDynamicWebhookEndpointTests : IDisposable
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    };

    [SetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
    
    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Test]
    public async Task ProcessWebhook_WithValidUserCreatedEvent_Returns202Accepted()
    {
        // Arrange
        var webhookEvent = new DynamicWebhookEventDto
        {
            EventId = "evt_01HQXYZ789ABCDEF01234567",
            EventName = "users.created",
            WebhookId = "whk_config_123456",
            CreatedAt = DateTime.UtcNow,
            Data = new Dictionary<string, object>
            {
                ["userId"] = "usr_01HQABC123DEF456789",
                ["email"] = "user@example.com",
                ["createdAt"] = DateTime.UtcNow.ToString("O")
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/dynamic")
        {
            Content = JsonContent.Create(webhookEvent, options: JsonOptions)
        };
        request.Headers.Add("X-Dynamic-Signature", "test-signature");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var responseDto = JsonSerializer.Deserialize<DynamicWebhookResponseDto>(responseContent, JsonOptions);
        
        responseDto.ShouldNotBeNull();
        responseDto.Received.ShouldBeTrue();
        responseDto.SyncScheduled.ShouldBeTrue(); // users.* events should schedule sync
        responseDto.Acknowledgment.ShouldNotBeNull();
        responseDto.Acknowledgment.EventId.ShouldBe(webhookEvent.EventId);
        responseDto.Acknowledgment.Status.ShouldBe("processed");
        responseDto.Acknowledgment.RetryCount.ShouldBe(0);
        responseDto.Acknowledgment.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task ProcessWebhook_WithValidWalletLinkedEvent_Returns202WithSyncScheduled()
    {
        // Arrange
        var webhookEvent = new DynamicWebhookEventDto
        {
            EventId = "evt_WALLET12345",
            EventName = "wallets.linked",
            WebhookId = "whk_wallet_config",
            CreatedAt = DateTime.UtcNow,
            Data = new Dictionary<string, object>
            {
                ["walletAddress"] = "0x1234567890abcdef",
                ["userId"] = "usr_123"
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/dynamic")
        {
            Content = JsonContent.Create(webhookEvent, options: JsonOptions)
        };
        request.Headers.Add("X-Dynamic-Signature", "test-signature");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var responseDto = JsonSerializer.Deserialize<DynamicWebhookResponseDto>(responseContent, JsonOptions);
        
        responseDto.ShouldNotBeNull();
        responseDto.SyncScheduled.ShouldBeTrue(); // wallets.* events should schedule sync
    }

    [Test]
    public async Task ProcessWebhook_WithSessionCreatedEvent_Returns202WithoutSync()
    {
        // Arrange
        var webhookEvent = new DynamicWebhookEventDto
        {
            EventId = "evt_SESSION789",
            EventName = "sessions.created",
            CreatedAt = DateTime.UtcNow,
            Data = new Dictionary<string, object>
            {
                ["sessionId"] = "sess_123"
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/dynamic")
        {
            Content = JsonContent.Create(webhookEvent, options: JsonOptions)
        };
        request.Headers.Add("X-Dynamic-Signature", "test-signature");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var responseDto = JsonSerializer.Deserialize<DynamicWebhookResponseDto>(responseContent, JsonOptions);
        
        responseDto.ShouldNotBeNull();
        responseDto.SyncScheduled.ShouldBeFalse(); // sessions.* events should not schedule sync
    }

    [Test]
    public async Task ProcessWebhook_WithMissingSignatureHeader_Returns401()
    {
        // Arrange
        var webhookEvent = new DynamicWebhookEventDto
        {
            EventId = "evt_TEST123",
            EventName = "users.created",
            CreatedAt = DateTime.UtcNow,
            Data = new Dictionary<string, object>()
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/dynamic")
        {
            Content = JsonContent.Create(webhookEvent, options: JsonOptions)
        };
        // Intentionally not adding X-Dynamic-Signature header

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.ShouldContain("Missing X-Dynamic-Signature header");
    }

    [Test]
    public async Task ProcessWebhook_WithInvalidEventType_Returns400()
    {
        // Arrange
        var webhookEvent = new DynamicWebhookEventDto
        {
            EventId = "evt_INVALID123",
            EventName = "invalid.event.type",
            CreatedAt = DateTime.UtcNow,
            Data = new Dictionary<string, object>()
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/dynamic")
        {
            Content = JsonContent.Create(webhookEvent, options: JsonOptions)
        };
        request.Headers.Add("X-Dynamic-Signature", "test-signature");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.ShouldContain("Invalid event type");
    }

    [Test]
    public async Task ProcessWebhook_WithFutureTimestamp_Returns400()
    {
        // Arrange
        var webhookEvent = new DynamicWebhookEventDto
        {
            EventId = "evt_FUTURE123",
            EventName = "users.created",
            CreatedAt = DateTime.UtcNow.AddMinutes(10), // Too far in the future
            Data = new Dictionary<string, object>()
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/dynamic")
        {
            Content = JsonContent.Create(webhookEvent, options: JsonOptions)
        };
        request.Headers.Add("X-Dynamic-Signature", "test-signature");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.ShouldContain("timestamp cannot be more than 5 minutes in the future");
    }

    [Test]
    public async Task ProcessWebhook_WithMissingEventId_Returns400()
    {
        // Arrange
        var json = @"{
            ""eventName"": ""users.created"",
            ""createdAt"": """ + DateTime.UtcNow.ToString("O") + @""",
            ""data"": {}
        }";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/dynamic")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Dynamic-Signature", "test-signature");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.ShouldContain("EventId is required");
    }

    [Test]
    public async Task ProcessWebhook_WithTooLongEventId_Returns400()
    {
        // Arrange
        var webhookEvent = new DynamicWebhookEventDto
        {
            EventId = new string('x', 101), // Exceeds 100 character limit
            EventName = "users.created",
            CreatedAt = DateTime.UtcNow,
            Data = new Dictionary<string, object>()
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/dynamic")
        {
            Content = JsonContent.Create(webhookEvent, options: JsonOptions)
        };
        request.Headers.Add("X-Dynamic-Signature", "test-signature");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.ShouldContain("EventId must not exceed 100 characters");
    }

    [Test]
    public async Task ProcessWebhook_WithNullData_Returns400()
    {
        // Arrange
        var json = @"{
            ""eventId"": ""evt_123"",
            ""eventName"": ""users.created"",
            ""createdAt"": """ + DateTime.UtcNow.ToString("O") + @""",
            ""data"": null
        }";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/dynamic")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Dynamic-Signature", "test-signature");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.ShouldContain("Data payload is required");
    }

    [Test]
    public async Task ProcessWebhook_ValidatesAllEventTypes()
    {
        // Arrange
        var validEventTypes = new[]
        {
            "users.created", "users.updated", "users.deleted",
            "wallets.linked", "wallets.unlinked", "wallets.updated",
            "sessions.created", "sessions.deleted",
            "environments.updated"
        };

        foreach (var eventType in validEventTypes)
        {
            var webhookEvent = new DynamicWebhookEventDto
            {
                EventId = $"evt_{eventType}_{Guid.NewGuid()}",
                EventName = eventType,
                CreatedAt = DateTime.UtcNow,
                Data = new Dictionary<string, object>()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/dynamic")
            {
                Content = JsonContent.Create(webhookEvent, options: JsonOptions)
            };
            request.Headers.Add("X-Dynamic-Signature", "test-signature");

            // Act
            var response = await _client!.SendAsync(request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted, $"Event type {eventType} should be accepted");
        }
    }
}