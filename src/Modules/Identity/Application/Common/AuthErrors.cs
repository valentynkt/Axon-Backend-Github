namespace Axon.Modules.Identity.Application.Common;

/// <summary>
/// Standardized error codes for all authentication operations.
/// Consolidates error codes from all authentication services.
/// </summary>
public static class AuthErrors
{
    // Token Validation Errors
    public const string TokenRequired = "AUTH.TOKEN_REQUIRED";
    public const string TokenExpired = "AUTH.TOKEN_EXPIRED";
    public const string TokenInvalid = "AUTH.TOKEN_INVALID";
    public const string TokenReplayed = "AUTH.TOKEN_REPLAYED";
    public const string TokenMalformed = "AUTH.TOKEN_MALFORMED";
    public const string TokenUnsupportedType = "AUTH.TOKEN_UNSUPPORTED_TYPE";
    public const string TokenInvalidSignature = "AUTH.TOKEN_INVALID_SIGNATURE";
    public const string TokenInvalidClaims = "AUTH.TOKEN_INVALID_CLAIMS";
    public const string TokenInvalidAxonUserId = "AUTH.TOKEN_INVALID_AXON_USER_ID";

    // Token Generation Errors
    public const string TokenGenerationFailed = "AUTH.TOKEN_GENERATION_FAILED";

    // Refresh Token Errors
    public const string TokenRefreshFailed = "AUTH.TOKEN_REFRESH_FAILED";
    public const string TokenAlreadyUsed = "AUTH.TOKEN_ALREADY_USED";

    // Challenge Errors
    public const string ChallengeRequired = "AUTH.CHALLENGE_REQUIRED";
    public const string ChallengeExpired = "AUTH.CHALLENGE_EXPIRED";
    public const string ChallengeInvalid = "AUTH.CHALLENGE_INVALID";
    public const string ChallengeNetworkMismatch = "AUTH.CHALLENGE_NETWORK_MISMATCH";
    public const string ChallengeChainMismatch = "AUTH.CHALLENGE_CHAIN_MISMATCH";
    public const string ChallengeAddressMismatch = "AUTH.CHALLENGE_ADDRESS_MISMATCH";
    public const string ChallengeAudienceMismatch = "AUTH.CHALLENGE_AUDIENCE_MISMATCH";
    public const string ChallengeTtlExceeded = "AUTH.CHALLENGE_TTL_EXCEEDED";
    public const string ChallengeTtlInvalid = "AUTH.CHALLENGE_TTL_INVALID";
    public const string ChallengeNotYetValid = "AUTH.CHALLENGE_NOT_YET_VALID";
    public const string ChallengeMissingFields = "AUTH.CHALLENGE_MISSING_FIELDS";
    public const string ChallengeInvalidJson = "AUTH.CHALLENGE_INVALID_JSON";
    public const string ChallengeValidationError = "AUTH.CHALLENGE_VALIDATION_ERROR";

    // Replay Protection Errors
    public const string ReplayCheckError = "AUTH.REPLAY_CHECK_ERROR";
    public const string ReplayRaceCondition = "AUTH.REPLAY_RACE_CONDITION";

    // General Auth Errors
    public const string ValidationError = "AUTH.VALIDATION_ERROR";
    public const string ConfigurationError = "AUTH.CONFIGURATION_ERROR";
    public const string InternalError = "AUTH.INTERNAL_ERROR";
}