namespace Axon.Modules.Identity.Domain.Errors;

/// <summary>
/// Centralized error codes and messages for the Wallet domain.
/// This ensures consistency across all layers and provides a single source of truth for domain errors.
/// Maps to the error taxonomy WB1-WB9 from the domain specification.
/// </summary>
public static class WalletDomainErrors
{
    /// <summary>
    /// Chain related errors.
    /// </summary>
    public static class Chain
    {
        public const string UnsupportedCode = "WALLET.CHAIN.UNSUPPORTED";
        public const string UnsupportedMessage = "Unsupported chain.";
        
        public const string RequiredCode = "WALLET.CHAIN.REQUIRED";
        public const string RequiredMessage = "Chain is required.";
        
        public const string InvalidCode = "WALLET.CHAIN.INVALID";
        public const string InvalidMessage = "Invalid chain format.";

        // Error factory methods
        public static Error Unsupported(string? chainName = null) => Error.Validation(
            chainName is null ? UnsupportedMessage : $"Unsupported chain: {chainName}", 
            UnsupportedCode);
        public static Error Required() => Error.Validation(RequiredMessage, RequiredCode);
        public static Error Invalid(string? reason = null) => Error.Validation(
            reason is null ? InvalidMessage : $"{InvalidMessage}: {reason}", 
            InvalidCode);
    }

    /// <summary>
    /// Address related errors.
    /// </summary>
    public static class Address
    {
        public const string InvalidFormatCode = "WALLET.ADDRESS.INVALID_FORMAT";
        public const string InvalidFormatMessage = "Invalid address format for chain.";
        
        public const string RequiredCode = "WALLET.ADDRESS.REQUIRED";
        public const string RequiredMessage = "Address is required.";
        
        public const string CanonicalMismatchCode = "WALLET.ADDRESS.CANONICAL_MISMATCH";
        public const string CanonicalMismatchMessage = "Address canonical mismatch.";

        // Error factory methods
        public static Error InvalidFormat(string? chain = null) => Error.Validation(
            chain is null ? InvalidFormatMessage : $"Invalid address format for chain: {chain}", 
            InvalidFormatCode);
        public static Error Required() => Error.Validation(RequiredMessage, RequiredCode);
        public static Error CanonicalMismatch() => Error.Validation(CanonicalMismatchMessage, CanonicalMismatchCode);
    }

    /// <summary>
    /// Wallet aggregate related errors.
    /// </summary>
    public static class Wallet
    {
        public const string AlreadyExistsCode = "WALLET.ALREADY_EXISTS";
        public const string AlreadyExistsMessage = "Wallet already registered.";
        
        public const string NotFoundCode = "WALLET.NOT_FOUND";
        public const string NotFoundMessage = "Wallet not found.";
        
        public const string ImmutableFieldChangeCode = "WALLET.IMMUTABLE_FIELD_CHANGE";
        public const string ImmutableFieldChangeMessage = "Chain and address are immutable.";
        
        public const string TimestampRegressionCode = "WALLET.TIMESTAMP_REGRESSION";
        public const string TimestampRegressionMessage = "Observed time cannot regress.";
        
        public const string SoftDeletedCode = "WALLET.SOFT_DELETED";
        public const string SoftDeletedMessage = "Wallet is deleted; operation not permitted.";

        // Error factory methods
        public static Error AlreadyExists() => Error.BusinessRule(AlreadyExistsMessage, AlreadyExistsCode);
        public static Error NotFound() => Error.NotFound(NotFoundMessage, NotFoundCode);
        public static Error ImmutableFieldChange() => Error.BusinessRule(ImmutableFieldChangeMessage, ImmutableFieldChangeCode);
        public static Error TimestampRegression() => Error.BusinessRule(TimestampRegressionMessage, TimestampRegressionCode);
        public static Error SoftDeleted() => Error.BusinessRule(SoftDeletedMessage, SoftDeletedCode);
    }

    /// <summary>
    /// Metadata related errors.
    /// </summary>
    public static class Meta
    {
        public const string TooLargeCode = "WALLET.META.TOO_LARGE";
        public const string TooLargeMessage = "Meta exceeds limits or is invalid.";
        
        public const string InvalidKeyCode = "WALLET.META.INVALID_KEY";
        public const string InvalidKeyMessage = "Meta key is invalid.";
        
        public const string InvalidValueCode = "WALLET.META.INVALID_VALUE";
        public const string InvalidValueMessage = "Meta value is invalid.";

        // Error factory methods
        public static Error TooLarge(int actualSize) => Error.Validation(
            $"Meta size ({actualSize} bytes) exceeds maximum limit.", 
            TooLargeCode);
        public static Error InvalidKey(string key) => Error.Validation(
            $"Meta key '{key}' is invalid.", 
            InvalidKeyCode);
        public static Error InvalidValue(string key, string reason) => Error.Validation(
            $"Meta value for key '{key}' is invalid: {reason}", 
            InvalidValueCode);
    }

    /// <summary>
    /// Tag related errors.
    /// </summary>
    public static class Tag
    {
        public const string NotAllowedCode = "WALLET.TAG.NOT_ALLOWED";
        public const string NotAllowedMessage = "Tag is not allowed.";
        
        public const string RequiredCode = "WALLET.TAG.REQUIRED";
        public const string RequiredMessage = "Tag is required.";
        
        public const string InvalidCode = "WALLET.TAG.INVALID";
        public const string InvalidMessage = "Tag format is invalid.";

        // Error factory methods
        public static Error NotAllowed(string tag) => Error.BusinessRule(
            $"Tag '{tag}' is not allowed.", 
            NotAllowedCode);
        public static Error Required() => Error.Validation(RequiredMessage, RequiredCode);
        public static Error Invalid(string? reason = null) => Error.Validation(
            reason is null ? InvalidMessage : $"{InvalidMessage}: {reason}", 
            InvalidCode);
    }

    /// <summary>
    /// General domain errors.
    /// </summary>
    public static class General
    {
        public const string CreationFailedCode = "WALLET.CREATION_FAILED";
        public const string CreationFailedMessage = "Failed to create wallet.";
        
        public const string ValidationFailedCode = "WALLET.VALIDATION_FAILED";
        public const string ValidationFailedMessage = "Wallet validation failed.";

        // Error factory methods
        public static Error CreationFailed(string reason) => Error.Internal(
            $"{CreationFailedMessage}: {reason}", 
            CreationFailedCode);
        public static Error ValidationFailed(string reason) => Error.Validation(
            $"{ValidationFailedMessage}: {reason}", 
            ValidationFailedCode);
    }
}