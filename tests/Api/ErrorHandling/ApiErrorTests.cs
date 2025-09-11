using System.Text.Json;
using Axon.Api.Contracts.Common;
using NUnit.Framework;
using Shouldly;

namespace Axon.Api.ErrorHandling.Tests;

/// <summary>
/// Unit tests for ApiError model serialization and factory methods.
/// Verifies JSON serialization, factory methods, and privacy-safe error creation.
/// </summary>
[TestFixture]
public class ApiErrorTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Test]
    public void Create_WithAllParameters_ShouldCreateApiError()
    {
        // Arrange
        const string code = "TEST_ERROR";
        const string message = "Test error message";
        var details = new { field = "test", value = 42 };

        // Act
        var apiError = ApiError.Create(code, message, details);

        // Assert
        apiError.Code.ShouldBe(code);
        apiError.Message.ShouldBe(message);
        apiError.Details.ShouldBe(details);
    }

    [Test]
    public void Create_WithNullDetails_ShouldCreateApiErrorWithNullDetails()
    {
        // Act
        var apiError = ApiError.Create("TEST_ERROR", "Test message", null);

        // Assert
        apiError.Code.ShouldBe("TEST_ERROR");
        apiError.Message.ShouldBe("Test message");
        apiError.Details.ShouldBeNull();
    }

    [Test]
    public void Validation_ShouldCreateValidationError()
    {
        // Arrange
        const string message = "Email is required";
        var details = new { field = "email" };

        // Act
        var apiError = ApiError.Validation(message, details);

        // Assert
        apiError.Code.ShouldBe("VALIDATION_ERROR");
        apiError.Message.ShouldBe(message);
        apiError.Details.ShouldBe(details);
    }

    [Test]
    public void Unauthorized_ShouldCreateUnauthorizedError()
    {
        // Act
        var apiError = ApiError.Unauthorized();

        // Assert
        apiError.Code.ShouldBe("UNAUTHORIZED");
        apiError.Message.ShouldBe("Authentication required");
        apiError.Details.ShouldBeNull();
    }

    [Test]
    public void Unauthorized_WithCustomMessage_ShouldCreateUnauthorizedErrorWithMessage()
    {
        // Arrange
        const string message = "Invalid JWT token";

        // Act
        var apiError = ApiError.Unauthorized(message);

        // Assert
        apiError.Code.ShouldBe("UNAUTHORIZED");
        apiError.Message.ShouldBe(message);
        apiError.Details.ShouldBeNull();
    }

    [Test]
    public void WalletOwnershipConflict_ShouldCreatePrivacySafeConflictError()
    {
        // Arrange
        const string chainId = "solana";
        const string address = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";

        // Act
        var apiError = ApiError.WalletOwnershipConflict(chainId, address);

        // Assert
        apiError.Code.ShouldBe("WALLET_OWNERSHIP_CONFLICT");
        apiError.Message.ShouldBe("wallet already owned");
        
        var details = apiError.Details.ShouldBeOfType<object>();
        var json = JsonSerializer.Serialize(details, _jsonOptions);
        var expectedJson = JsonSerializer.Serialize(new { chainId, address }, _jsonOptions);
        json.ShouldBe(expectedJson);
    }

    [Test]
    public void BusinessRuleViolation_ShouldCreateBusinessRuleError()
    {
        // Arrange
        const string message = "Domain constraint violated";
        var details = new { rule = "TestRule" };

        // Act
        var apiError = ApiError.BusinessRuleViolation(message, details);

        // Assert
        apiError.Code.ShouldBe("BUSINESS_RULE_VIOLATION");
        apiError.Message.ShouldBe(message);
        apiError.Details.ShouldBe(details);
    }

    [Test]
    public void RateLimitExceeded_ShouldCreateRateLimitError()
    {
        // Act
        var apiError = ApiError.RateLimitExceeded();

        // Assert
        apiError.Code.ShouldBe("RATE_LIMIT_EXCEEDED");
        apiError.Message.ShouldBe("Rate limit exceeded");
        apiError.Details.ShouldBeNull();
    }

    [Test]
    public void RateLimitExceeded_WithRetryAfter_ShouldCreateRateLimitErrorWithRetryAfter()
    {
        // Arrange
        const string message = "Too many requests";
        const int retryAfter = 60;

        // Act
        var apiError = ApiError.RateLimitExceeded(message, retryAfter);

        // Assert
        apiError.Code.ShouldBe("RATE_LIMIT_EXCEEDED");
        apiError.Message.ShouldBe(message);
        
        var details = apiError.Details.ShouldBeOfType<object>();
        var json = JsonSerializer.Serialize(details, _jsonOptions);
        var expectedJson = JsonSerializer.Serialize(new { retryAfter }, _jsonOptions);
        json.ShouldBe(expectedJson);
    }

    [Test]
    public void InternalError_ShouldCreateInternalError()
    {
        // Act
        var apiError = ApiError.InternalError();

        // Assert
        apiError.Code.ShouldBe("INTERNAL_ERROR");
        apiError.Message.ShouldBe("An internal error occurred");
        apiError.Details.ShouldBeNull();
    }

    [Test]
    public void InternalError_WithCustomMessage_ShouldCreateInternalErrorWithMessage()
    {
        // Arrange
        const string message = "Database connection failed";

        // Act
        var apiError = ApiError.InternalError(message);

        // Assert
        apiError.Code.ShouldBe("INTERNAL_ERROR");
        apiError.Message.ShouldBe(message);
        apiError.Details.ShouldBeNull();
    }

    [Test]
    public void Serialization_ShouldUseCorrectJsonPropertyNames()
    {
        // Arrange
        var apiError = ApiError.Create("TEST_CODE", "Test message", new { testField = "value" });

        // Act
        var json = JsonSerializer.Serialize(apiError, _jsonOptions);

        // Assert
        json.ShouldContain("\"code\":");
        json.ShouldContain("\"message\":");
        json.ShouldContain("\"details\":");
        json.ShouldNotContain("Code");
        json.ShouldNotContain("Message");
        json.ShouldNotContain("Details");
    }

    [Test]
    public void Serialization_WithNullDetails_ShouldExcludeDetailsFromJson()
    {
        // Arrange
        var apiError = ApiError.Create("TEST_CODE", "Test message", null);

        // Act
        var json = JsonSerializer.Serialize(apiError, _jsonOptions);

        // Assert
        json.ShouldNotContain("details");
        json.ShouldContain("\"code\":\"TEST_CODE\"");
        json.ShouldContain("\"message\":\"Test message\"");
    }

    [Test]
    public void Deserialization_ShouldRecreateApiError()
    {
        // Arrange
        var original = ApiError.WalletOwnershipConflict("solana", "test-address");
        var json = JsonSerializer.Serialize(original, _jsonOptions);

        // Act
        var deserialized = JsonSerializer.Deserialize<ApiError>(json, _jsonOptions);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Code.ShouldBe(original.Code);
        deserialized.Message.ShouldBe(original.Message);
        
        // Details comparison requires JSON serialization due to anonymous types
        var originalDetailsJson = JsonSerializer.Serialize(original.Details, _jsonOptions);
        var deserializedDetailsJson = JsonSerializer.Serialize(deserialized.Details, _jsonOptions);
        deserializedDetailsJson.ShouldBe(originalDetailsJson);
    }
}