namespace BuildingBlocks.Application.Validation.Constants;

/// <summary>
/// Centralized validation error codes for consistency across the application
/// </summary>
public static class ValidationErrorCodes
{
    // Generic validation errors
    public const string Generic = "VAL.GENERIC";
    public const string Required = "VAL.REQUIRED";
    public const string InvalidFormat = "VAL.INVALID_FORMAT";

    // String validation errors
    public const string StringEmpty = "VAL.STRING.EMPTY";
    public const string StringEmptyOrWhitespace = "VAL.STRING.EMPTY_OR_WHITESPACE";
    public const string StringTooShort = "VAL.STRING.TOO_SHORT";
    public const string StringTooLong = "VAL.STRING.TOO_LONG";
    public const string StringInvalidLength = "VAL.STRING.INVALID_LENGTH";

    // Guid validation errors
    public const string GuidEmpty = "VAL.GUID.EMPTY";
    public const string GuidInvalid = "VAL.GUID.INVALID";

    // Domain object validation errors
    public const string DomainInvalid = "VAL.DOMAIN.INVALID";
    public const string StrongIdInvalid = "VAL.STRONGID.INVALID";

    // Content validation errors
    public const string ContentEmpty = "VAL.CONTENT.EMPTY";
    public const string ContentLength = "VAL.CONTENT.LENGTH";
    public const string ContentInvalid = "VAL.CONTENT.INVALID";

    // Pagination validation errors
    public const string PaginationInvalidSize = "VAL.PAGINATION.INVALID_SIZE";
    public const string PaginationInvalidPage = "VAL.PAGINATION.INVALID_PAGE";

    // Numeric validation errors
    public const string NumberTooSmall = "VAL.NUMBER.TOO_SMALL";
    public const string NumberTooLarge = "VAL.NUMBER.TOO_LARGE";
    public const string NumberInvalidRange = "VAL.NUMBER.INVALID_RANGE";

    // Email validation errors
    public const string EmailInvalid = "VAL.EMAIL.INVALID";
    public const string EmailTooLong = "VAL.EMAIL.TOO_LONG";

    // Collection validation errors
    public const string CollectionEmpty = "VAL.COLLECTION.EMPTY";
    public const string CollectionTooSmall = "VAL.COLLECTION.TOO_SMALL";
    public const string CollectionTooLarge = "VAL.COLLECTION.TOO_LARGE";
}