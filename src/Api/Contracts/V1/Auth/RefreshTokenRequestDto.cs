using System.ComponentModel.DataAnnotations;

namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Request DTO for refreshing access tokens
/// </summary>
public sealed class RefreshTokenRequestDto
{
    /// <summary>
    /// The refresh token to use for generating a new access token
    /// </summary>
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}