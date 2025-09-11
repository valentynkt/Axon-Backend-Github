using System.Text.Json;
using Axon.Api.ErrorHandling;
using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Shouldly;

namespace Axon.Api.ErrorHandling.Tests;

/// <summary>
/// Unit tests for ErrorMappingExtensions HTTP status code mapping and ApiError conversion.
/// Verifies all supported error types map to correct HTTP status codes and privacy protection.
/// </summary>
[TestFixture]
public class ErrorMappingExtensionsTests
{
    #region HTTP Status Code Mapping Tests

    [Test]
    public void ToHttpStatusCode_ValidationError_ShouldReturn400()
    {
        // Arrange
        var error = Error.Validation("Test validation error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Test]
    public void ToHttpStatusCode_SerializationError_ShouldReturn400()
    {
        // Arrange
        var error = Error.Serialization("Test serialization error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Test]
    public void ToHttpStatusCode_UnauthorizedError_ShouldReturn401()
    {
        // Arrange
        var error = Error.Unauthorized("Test unauthorized error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Test]
    public void ToHttpStatusCode_SecurityError_ShouldReturn401()
    {
        // Arrange
        var error = Error.Security("Test security error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Test]
    public void ToHttpStatusCode_ConflictError_ShouldReturn409()
    {
        // Arrange
        var error = Error.Conflict("Test conflict error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Test]
    public void ToHttpStatusCode_ConcurrencyError_ShouldReturn409()
    {
        // Arrange
        var error = Error.Concurrency("Test concurrency error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Test]
    public void ToHttpStatusCode_BusinessRuleError_ShouldReturn422()
    {
        // Arrange
        var error = Error.BusinessRule("Test business rule error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Test]
    public void ToHttpStatusCode_AggregateError_ShouldReturn422()
    {
        // Arrange
        var error = Error.Aggregate("Test aggregate error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Test]
    public void ToHttpStatusCode_RateLimitError_ShouldReturn429()
    {
        // Arrange
        var error = Error.RateLimit("Test rate limit error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
    }

    [Test]
    public void ToHttpStatusCode_InternalError_ShouldReturn500()
    {
        // Arrange
        var error = Error.Internal("Test internal error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Test]
    public void ToHttpStatusCode_ConfigurationError_ShouldReturn500()
    {
        // Arrange
        var error = Error.Configuration("Test configuration error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Test]
    public void ToHttpStatusCode_ExternalError_ShouldReturn500()
    {
        // Arrange
        var error = Error.External("Test external error");

        // Act
        var statusCode = error.ToHttpStatusCode();

        // Assert
        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    #endregion

    #region ApiError Conversion Tests

    [Test]
    public void ToApiError_ValidationError_ShouldCreateValidationApiError()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { { "field", "email" } };
        var error = Error.Validation("Email is required").WithMetadata(metadata);

        // Act
        var apiError = error.ToApiError();

        // Assert
        apiError.Code.ShouldBe("VALIDATION_ERROR");
        apiError.Message.ShouldBe("Email is required");
        apiError.Details.ShouldNotBeNull();
    }

    [Test]
    public void ToApiError_UnauthorizedError_ShouldCreateUnauthorizedApiError()
    {
        // Arrange
        var error = Error.Unauthorized("Invalid JWT token");

        // Act
        var apiError = error.ToApiError();

        // Assert
        apiError.Code.ShouldBe("UNAUTHORIZED");
        apiError.Message.ShouldBe("Invalid JWT token");
        apiError.Details.ShouldBeNull();
    }

    [Test]
    public void ToApiError_WalletOwnershipConflict_ShouldCreatePrivacySafeConflictError()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "chainId", "solana" },
            { "address", "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM" },
            { "principalId", "sensitive-principal-id" }, // Should be filtered out
            { "ownerId", "sensitive-owner-id" } // Should be filtered out
        };
        var error = Error.Conflict("wallet already owned", "WALLET_OWNERSHIP_CONFLICT").WithMetadata(metadata);

        // Act
        var apiError = error.ToApiError();

        // Assert
        apiError.Code.ShouldBe("WALLET_OWNERSHIP_CONFLICT");
        apiError.Message.ShouldBe("wallet already owned");
        
        var details = apiError.Details.ShouldNotBeNull();
        var json = JsonSerializer.Serialize(details, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        json.ShouldContain("solana");
        json.ShouldContain("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM");
        json.ShouldNotContain("principalId");
        json.ShouldNotContain("ownerId");
    }

    [Test]
    public void ToApiError_BusinessRuleError_ShouldCreateBusinessRuleApiError()
    {
        // Arrange
        var error = Error.BusinessRule("Domain constraint violated");

        // Act
        var apiError = error.ToApiError();

        // Assert
        apiError.Code.ShouldBe("BUSINESS_RULE_VIOLATION");
        apiError.Message.ShouldBe("Domain constraint violated");
    }

    [Test]
    public void ToApiError_RateLimitError_ShouldCreateRateLimitApiError()
    {
        // Arrange
        var error = Error.RateLimit("Too many requests", retryAfter: TimeSpan.FromSeconds(60));

        // Act
        var apiError = error.ToApiError();

        // Assert
        apiError.Code.ShouldBe("RATE_LIMIT_EXCEEDED");
        apiError.Message.ShouldBe("Too many requests");
        apiError.Details.ShouldNotBeNull();
    }

    [Test]
    public void ToApiError_InternalError_ShouldCreateInternalApiError()
    {
        // Arrange
        var error = Error.Internal("Database connection failed");

        // Act
        var apiError = error.ToApiError();

        // Assert
        apiError.Code.ShouldBe("INTERNAL_ERROR");
        apiError.Message.ShouldBe("An internal error occurred"); // Safe message
        apiError.Details.ShouldBeNull();
    }

    #endregion

    #region Privacy Protection Tests

    [Test]
    public void ToApiError_ShouldFilterOutSensitiveMetadata()
    {
        // Arrange
        var sensitiveMetadata = new Dictionary<string, object>
        {
            { "principalId", "secret-principal-id" },
            { "userId", "secret-user-id" },
            { "ownerId", "secret-owner-id" },
            { "jwt", "secret-jwt-token" },
            { "token", "secret-token" },
            { "credential", "secret-credential" },
            { "email", "user@example.com" },
            { "password", "secret-password" },
            { "safeField", "safe-value" }, // This should be included
            { "chainId", "solana" } // This should be included
        };
        var error = Error.Validation("Test error").WithMetadata(sensitiveMetadata);

        // Act
        var apiError = error.ToApiError();

        // Assert
        var details = apiError.Details.ShouldNotBeNull();
        var json = JsonSerializer.Serialize(details);
        
        // Should not contain sensitive fields
        json.ShouldNotContain("principalId");
        json.ShouldNotContain("userId");
        json.ShouldNotContain("ownerId");
        json.ShouldNotContain("jwt");
        json.ShouldNotContain("token");
        json.ShouldNotContain("credential");
        json.ShouldNotContain("email");
        json.ShouldNotContain("password");
        
        // Should contain safe fields
        json.ShouldContain("safeField");
        json.ShouldContain("chainId");
    }

    [Test]
    public void ToApiError_ConflictWithoutWalletMetadata_ShouldCreateGenericConflictError()
    {
        // Arrange
        var error = Error.Conflict("Generic conflict error");

        // Act
        var apiError = error.ToApiError();

        // Assert
        apiError.Code.ShouldBe("CONFLICT");
        apiError.Message.ShouldBe("Generic conflict error");
    }

    [Test]
    public void ToApiError_WalletConflictWithoutAddress_ShouldCreateGenericConflictError()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "chainId", "solana" }
            // Missing address field
        };
        var error = Error.Conflict("wallet conflict", "WALLET_OWNERSHIP_CONFLICT").WithMetadata(metadata);

        // Act
        var apiError = error.ToApiError();

        // Assert
        apiError.Code.ShouldBe("CONFLICT");
        apiError.Message.ShouldBe("wallet conflict");
    }

    #endregion

    #region Rate Limit Header Tests

    [Test]
    public void SendApiErrorAsync_WithRateLimitError_ShouldAddRateLimitHeaders()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var metadata = new Dictionary<string, object>
        {
            { "RetryAfter", 60.0 },
            { "Remaining", 0 },
            { "Reset", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };
        var error = Error.RateLimit("Too many requests").WithMetadata(metadata);

        // Act
        httpContext.SendApiErrorAsync(error).Wait();

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
        httpContext.Response.Headers.ShouldContainKey("Retry-After");
        httpContext.Response.Headers.ShouldContainKey("X-RateLimit-Remaining");
        httpContext.Response.Headers.ShouldContainKey("X-RateLimit-Reset");
    }

    [Test]
    public void SendApiErrorAsync_WithNonRateLimitError_ShouldNotAddRateLimitHeaders()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var error = Error.Validation("Validation error");

        // Act
        httpContext.SendApiErrorAsync(error).Wait();

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        httpContext.Response.Headers.ShouldNotContainKey("Retry-After");
        httpContext.Response.Headers.ShouldNotContainKey("X-RateLimit-Remaining");
        httpContext.Response.Headers.ShouldNotContainKey("X-RateLimit-Reset");
    }

    #endregion
}