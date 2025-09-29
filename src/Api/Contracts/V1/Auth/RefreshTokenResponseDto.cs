namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Response DTO for refresh token operations
/// </summary>
public sealed class RefreshTokenResponseDto
{
    /// <summary>
    /// Indicates if the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The new access token (only present if Success = true)
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// The new refresh token (only present if Success = true)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token type (always "Bearer" if Success = true)
    /// </summary>
    public string? TokenType { get; set; }

    /// <summary>
    /// Access token expiration time in seconds (only present if Success = true)
    /// </summary>
    public int? ExpiresIn { get; set; }

    /// <summary>
    /// When the tokens were issued (only present if Success = true)
    /// </summary>
    public DateTimeOffset? IssuedAt { get; set; }

    /// <summary>
    /// When the access token expires (only present if Success = true)
    /// </summary>
    public DateTimeOffset? AccessTokenExpiresAt { get; set; }

    /// <summary>
    /// When the refresh token expires (only present if Success = true)
    /// </summary>
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Error message (only present if Success = false)
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Error code (only present if Success = false)
    /// </summary>
    public string? ErrorCode { get; set; }
}