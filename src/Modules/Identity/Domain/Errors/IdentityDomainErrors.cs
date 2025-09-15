namespace Axon.Modules.Identity.Domain.Errors;

/// <summary>
/// Centralized error codes and messages for the Identity domain.
/// This ensures consistency across all layers and provides a single source of truth for domain errors.
/// </summary>
public static class IdentityDomainErrors
{
    /// <summary>
    /// Principal related errors.
    /// </summary>
    public static class Principal
    {
        public const string NotFoundCode = "IDENTITY.PRINCIPAL.NOT_FOUND";
        public const string NotFoundMessage = "Account not found.";
        
        public const string DeletedCode = "IDENTITY.PRINCIPAL.DELETED";
        public const string DeletedMessage = "Account has been deleted.";
        
        public const string InvalidTypeCode = "IDENTITY.PRINCIPAL.TYPE.INVALID";
        public const string InvalidTypeMessage = "Invalid account type.";
        
        public const string MustBeActiveCode = "IDENTITY.PRINCIPAL.MUST_BE_ACTIVE";
        public const string MustBeActiveMessage = "Account must be active to perform this operation.";
        
        public const string CannotBeDeletedCode = "IDENTITY.PRINCIPAL.CANNOT_BE_DELETED";
        public const string CannotBeDeletedMessage = "Account cannot be deleted while having active wallet ownerships or credentials.";
        
        public const string CreationFailedCode = "IDENTITY.PRINCIPAL.CREATION.FAILED";
        public const string CreationFailedMessage = "Failed to create account.";

        // Error factory methods
        public static Error NotFound() => Error.NotFound(NotFoundMessage, NotFoundCode);
        public static Error Deleted() => Error.NotFound(DeletedMessage, DeletedCode);
        public static Error InvalidType() => Error.Validation(InvalidTypeMessage, InvalidTypeCode);
        public static Error MustBeActive() => Error.BusinessRule(MustBeActiveMessage, MustBeActiveCode);
        public static Error CannotBeDeleted() => Error.BusinessRule(CannotBeDeletedMessage, CannotBeDeletedCode);
        public static Error CreationFailed(string reason) => Error.Internal($"{CreationFailedMessage}: {reason}", CreationFailedCode);
    }

    /// <summary>
    /// Identity credential related errors.
    /// </summary>
    public static class Credential
    {
        public const string DuplicateCode = "IDENTITY.CREDENTIAL.DUPLICATE";
        public const string DuplicateMessage = "Credential already exists for this provider, issuer, and subject.";
        
        public const string BelongsToOtherCode = "IDENTITY.CREDENTIAL.BELONGS_TO_OTHER";
        public const string BelongsToOtherMessage = "Credential belongs to a different account.";
        
        public const string NotFoundCode = "IDENTITY.CREDENTIAL.NOT_FOUND";
        public const string NotFoundMessage = "Credential not found.";
        
        public const string ProviderRequiredCode = "IDENTITY.CREDENTIAL.PROVIDER.REQUIRED";
        public const string ProviderRequiredMessage = "Provider type is required.";
        
        public const string ProviderInvalidCode = "IDENTITY.CREDENTIAL.PROVIDER.INVALID";
        public const string ProviderInvalidMessage = "Invalid provider type.";
        
        public const string IssuerRequiredCode = "IDENTITY.CREDENTIAL.ISSUER.REQUIRED";
        public const string IssuerRequiredMessage = "Issuer is required.";
        
        public const string SubjectRequiredCode = "IDENTITY.CREDENTIAL.SUBJECT.REQUIRED";
        public const string SubjectRequiredMessage = "Subject is required.";
        
        public const string ProviderInvalidForContextCode = "IDENTITY.CREDENTIAL.PROVIDER.INVALID_FOR_CONTEXT";
        public const string ProviderInvalidForContextMessage = "Provider type is not valid for this operation context.";

        // Error factory methods
        public static Error Duplicate() => Error.BusinessRule(DuplicateMessage, DuplicateCode);
        public static Error BelongsToOther() => Error.Conflict(BelongsToOtherMessage, BelongsToOtherCode);
        public static Error NotFound() => Error.NotFound(NotFoundMessage, NotFoundCode);
        public static Error ProviderRequired() => Error.Validation(ProviderRequiredMessage, ProviderRequiredCode);
        public static Error ProviderInvalid() => Error.Validation(ProviderInvalidMessage, ProviderInvalidCode);
        public static Error IssuerRequired() => Error.Validation(IssuerRequiredMessage, IssuerRequiredCode);
        public static Error SubjectRequired() => Error.Validation(SubjectRequiredMessage, SubjectRequiredCode);
        public static Error ProviderInvalidForContext(string providerType, string context) => Error.BusinessRule(
            $"Provider type '{providerType}' is not valid for operation '{context}'.", ProviderInvalidForContextCode);
    }

    /// <summary>
    /// Wallet ownership related errors.
    /// </summary>
    public static class Wallet
    {
        public const string AlreadyOwnedCode = "IDENTITY.WALLET.ALREADY_OWNED";
        public const string AlreadyOwnedMessage = "Wallet is already owned by another account.";
        
        public const string AlreadyOwnedByPrincipalCode = "IDENTITY.WALLET.ALREADY_OWNED_BY_PRINCIPAL";
        public const string AlreadyOwnedByPrincipalMessage = "Wallet is already owned by this account.";
        
        public const string NotOwnedCode = "IDENTITY.WALLET.NOT_OWNED";
        public const string NotOwnedMessage = "Wallet is not owned by this account.";
        
        public const string NotOwnedByPrincipalCode = "IDENTITY.WALLET.NOT_OWNED_BY_PRINCIPAL";
        public const string NotOwnedByPrincipalMessage = "Wallet is not owned by this account.";
        
        public const string MaxExceededCode = "IDENTITY.WALLET.MAX_EXCEEDED";
        public const string MaxExceededMessage = "Account cannot have more than 10 linked wallets.";
        
        public const string IdInvalidCode = "IDENTITY.WALLET.ID.INVALID";
        public const string IdInvalidMessage = "Wallet ID must be positive.";
        
        public const string ProofRequiredCode = "IDENTITY.WALLET.PROOF.REQUIRED";
        public const string ProofRequiredMessage = "Proof type is required.";
        
        public const string ProofInvalidCode = "IDENTITY.WALLET.PROOF.INVALID";
        public const string ProofInvalidMessage = "Invalid proof type.";
        
        public const string AccessModeInvalidCode = "IDENTITY.WALLET.ACCESS_MODE.INVALID";
        public const string AccessModeInvalidMessage = "Invalid access mode.";
        
        public const string StateInvalidCode = "IDENTITY.WALLET.STATE.INVALID";
        public const string StateInvalidMessage = "Invalid ownership state.";
        
        public const string OwnershipDeletedCode = "IDENTITY.WALLET.OWNERSHIP.DELETED";
        public const string OwnershipDeletedMessage = "Wallet ownership has been deleted.";
        
        public const string OwnershipRevokedCode = "IDENTITY.WALLET.OWNERSHIP.REVOKED";
        public const string OwnershipRevokedMessage = "Wallet ownership has been revoked.";
        
        public const string LabelTooLongCode = "IDENTITY.WALLET.LABEL.TOO_LONG";
        public const string LabelTooLongMessage = "Wallet label cannot exceed 100 characters.";
        
        public const string DefaultNotOwnedCode = "IDENTITY.WALLET.DEFAULT_NOT_OWNED";
        public const string DefaultNotOwnedMessage = "Cannot set default wallet that is not owned by account.";
        
        public const string ChainMismatchCode = "IDENTITY.WALLET.CHAIN.MISMATCH";
        public const string ChainMismatchMessage = "Wallet chain does not match requested chain.";
        
        public const string WatchOnlyNotAllowedAsDefaultCode = "IDENTITY.WALLET.WATCH_ONLY_VIOLATION";
        public const string WatchOnlyNotAllowedAsDefaultMessage = "Only verified signing wallets can be set as default.";

        // Error factory methods
        public static Error AlreadyOwned() => Error.Conflict(AlreadyOwnedMessage, AlreadyOwnedCode);
        public static Error AlreadyOwnedByPrincipal() => Error.BusinessRule(AlreadyOwnedByPrincipalMessage, AlreadyOwnedByPrincipalCode);
        public static Error NotOwned() => Error.NotFound(NotOwnedMessage, NotOwnedCode);
        public static Error NotOwnedByPrincipal() => Error.NotFound(NotOwnedByPrincipalMessage, NotOwnedByPrincipalCode);
        public static Error MaxExceeded() => Error.Validation(MaxExceededMessage, MaxExceededCode);
        public static Error IdInvalid() => Error.Validation(IdInvalidMessage, IdInvalidCode);
        public static Error ProofRequired() => Error.Validation(ProofRequiredMessage, ProofRequiredCode);
        public static Error ProofInvalid() => Error.Validation(ProofInvalidMessage, ProofInvalidCode);
        public static Error AccessModeInvalid() => Error.Validation(AccessModeInvalidMessage, AccessModeInvalidCode);
        public static Error StateInvalid() => Error.Validation(StateInvalidMessage, StateInvalidCode);
        public static Error OwnershipDeleted() => Error.BusinessRule(OwnershipDeletedMessage, OwnershipDeletedCode);
        public static Error OwnershipRevoked() => Error.BusinessRule(OwnershipRevokedMessage, OwnershipRevokedCode);
        public static Error LabelTooLong() => Error.Validation(LabelTooLongMessage, LabelTooLongCode);
        public static Error DefaultNotOwned() => Error.BusinessRule(DefaultNotOwnedMessage, DefaultNotOwnedCode);
        public static Error ChainMismatch() => Error.BusinessRule(ChainMismatchMessage, ChainMismatchCode);
        public static Error WatchOnlyNotAllowedAsDefault() => Error.Validation(WatchOnlyNotAllowedAsDefaultMessage, WatchOnlyNotAllowedAsDefaultCode);
        
        // Wallet ownership conflict for exchange operations
        public static Error WalletOwnershipConflict(string chainId, string address) => Error.Conflict(
            $"Wallet {address} on chain {chainId} is already owned by another account.",
            "IDENTITY.WALLET.OWNERSHIP_CONFLICT",
            new Dictionary<string, object> { ["chainId"] = chainId, ["address"] = address });
    }

    /// <summary>
    /// Profile related errors.
    /// </summary>
    public static class Profile
    {
        public const string InvalidLanguageCode = "IDENTITY.PROFILE.LANGUAGE.INVALID";
        public const string InvalidLanguageMessage = "Invalid language code.";
        
        public const string InvalidRiskTierCode = "IDENTITY.PROFILE.RISK_TIER.INVALID";
        public const string InvalidRiskTierMessage = "Invalid risk tier.";
        
        public const string InvalidForPrincipalCode = "IDENTITY.PROFILE.RISK_TIER.INVALID_FOR_PRINCIPAL_TYPE";
        public const string InvalidForPrincipalMessage = "Risk tier is not valid for this account type.";
        
        public const string ServicePrincipalRiskConstraintCode = "IDENTITY.SERVICE_PRINCIPAL_RISK_CONSTRAINT";
        public const string ServicePrincipalRiskConstraintMessage = "Risk tier not available for this account type.";
        
        public const string ChainEmptyCode = "IDENTITY.PROFILE.CHAIN.EMPTY";
        public const string ChainEmptyMessage = "Chain cannot be empty.";
        
        public const string WalletIdInvalidCode = "IDENTITY.PROFILE.WALLET_ID.INVALID";
        public const string WalletIdInvalidMessage = "Wallet ID must be positive.";
        
        public const string InvalidCode = "IDENTITY.PROFILE.INVALID";
        public const string InvalidMessage = "Profile is invalid.";

        // Error factory methods
        public static Error InvalidLanguage() => Error.Validation(InvalidLanguageMessage, InvalidLanguageCode);
        public static Error InvalidRiskTier() => Error.Validation(InvalidRiskTierMessage, InvalidRiskTierCode);
        public static Error RiskTierInvalidForPrincipal() => Error.BusinessRule(InvalidForPrincipalMessage, InvalidForPrincipalCode);
        public static Error ServicePrincipalRiskConstraint() => Error.Validation(ServicePrincipalRiskConstraintMessage, ServicePrincipalRiskConstraintCode);
        public static Error ChainEmpty() => Error.Validation(ChainEmptyMessage, ChainEmptyCode);
        public static Error WalletIdInvalid() => Error.Validation(WalletIdInvalidMessage, WalletIdInvalidCode);
        public static Error Invalid() => Error.Validation(InvalidMessage, InvalidCode);
    }

    /// <summary>
    /// Email related errors.
    /// </summary>
    public static class Email
    {
        public const string RequiredCode = "IDENTITY.EMAIL.REQUIRED";
        public const string RequiredMessage = "Email is required.";
        
        public const string InvalidCode = "IDENTITY.EMAIL.INVALID";
        public const string InvalidMessage = "Email is invalid.";
        
        public const string HashRequiredCode = "IDENTITY.EMAIL.HASH.REQUIRED";
        public const string HashRequiredMessage = "Email hash is required.";
        
        public const string HashInvalidCode = "IDENTITY.EMAIL.HASH.INVALID";
        public const string HashInvalidMessage = "Email hash is invalid.";
    }

    /// <summary>
    /// General validation errors.
    /// </summary>
    public static class Validation
    {
        public const string RequiredFieldCode = "IDENTITY.VALIDATION.REQUIRED_FIELD";
        public const string RequiredFieldMessage = "Required field is missing.";
        
        public const string InvalidFormatCode = "IDENTITY.VALIDATION.INVALID_FORMAT";
        public const string InvalidFormatMessage = "Invalid format.";
        
        public const string OutOfRangeCode = "IDENTITY.VALIDATION.OUT_OF_RANGE";
        public const string OutOfRangeMessage = "Value is out of range.";

        public const string AtLeastOneFieldRequiredCode = "IDENTITY.VALIDATION.AT_LEAST_ONE_FIELD_REQUIRED";
        public const string AtLeastOneFieldRequiredMessage = "At least one field (PreferredLanguage or RiskTier) must be provided.";

        public const string InvalidWalletIdentificationCode = "IDENTITY.VALIDATION.INVALID_WALLET_IDENTIFICATION";
        public const string InvalidWalletIdentificationMessage = "Invalid wallet identification provided.";

        public const string EitherWalletIdOrCoordinatesRequiredCode = "IDENTITY.VALIDATION.EITHER_WALLET_ID_OR_COORDINATES_REQUIRED";
        public const string EitherWalletIdOrCoordinatesRequiredMessage = "Either WalletId or (string + RawAddress) must be provided.";

        public const string EitherCredentialIdOrIdentifiersRequiredCode = "IDENTITY.VALIDATION.EITHER_CREDENTIAL_ID_OR_IDENTIFIERS_REQUIRED";
        public const string EitherCredentialIdOrIdentifiersRequiredMessage = "Either CredentialId or (ProviderType + Issuer + Subject) must be provided.";

        public const string InvalidCredentialIdentifierCode = "IDENTITY.VALIDATION.INVALID_CREDENTIAL_IDENTIFIER";
        public const string InvalidCredentialIdentifierMessage = "Invalid credential identifier provided.";

        // Error factory methods
        public static Error RequiredField(string fieldName) => Error.Validation($"{fieldName} is required.", RequiredFieldCode);
        public static Error InvalidFormat(string fieldName) => Error.Validation($"{fieldName} has invalid format.", InvalidFormatCode);
        public static Error OutOfRange(string fieldName) => Error.Validation($"{fieldName} is out of range.", OutOfRangeCode);
        public static Error AtLeastOneFieldRequired() => Error.Validation(AtLeastOneFieldRequiredMessage, AtLeastOneFieldRequiredCode);
        public static Error InvalidWalletIdentification() => Error.Validation(InvalidWalletIdentificationMessage, InvalidWalletIdentificationCode);
        public static Error EitherWalletIdOrCoordinatesRequired() => Error.Validation(EitherWalletIdOrCoordinatesRequiredMessage, EitherWalletIdOrCoordinatesRequiredCode);
        public static Error EitherCredentialIdOrIdentifiersRequired() => Error.Validation(EitherCredentialIdOrIdentifiersRequiredMessage, EitherCredentialIdOrIdentifiersRequiredCode);
        public static Error InvalidCredentialIdentifier() => Error.Validation(InvalidCredentialIdentifierMessage, InvalidCredentialIdentifierCode);
    }

    /// <summary>
    /// Authentication and challenge related errors.
    /// </summary>
    public static class Authentication
    {
        public const string ChallengeInvalidCode = "IDENTITY.CHALLENGE.INVALID";
        public const string ChallengeInvalidMessage = "Challenge is invalid or expired.";

        public const string ChallengeBindingMismatchCode = "IDENTITY.CHALLENGE.BINDING_MISMATCH";
        public const string ChallengeBindingMismatchMessage = "Challenge does not match the requested chain and address.";

        public const string SignatureInvalidCode = "IDENTITY.SIGNATURE.INVALID";
        public const string SignatureInvalidMessage = "Invalid signature.";

        // Error factory methods
        public static Error ChallengeInvalid() => Error.Validation(ChallengeInvalidMessage, ChallengeInvalidCode);
        public static Error ChallengeBindingMismatch() => Error.Validation(ChallengeBindingMismatchMessage, ChallengeBindingMismatchCode);
        public static Error SignatureInvalid() => Error.Validation(SignatureInvalidMessage, SignatureInvalidCode);
    }
}