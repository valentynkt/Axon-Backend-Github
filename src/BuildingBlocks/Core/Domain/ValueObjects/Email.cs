using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Options;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.ValueObjects;

/// <summary>
/// RFC 5322-compliant email value object with comprehensive validation and domain logic.
/// Immutable, thread-safe, and optimized for performance with string interning.
/// </summary>
public sealed partial record Email : SingleValueObject<string>
{
    #region Constants & Static Fields
    
    public const int MaxLength = 254; // RFC 5321 maximum
    public const int MaxLocalPartLength = 64; // RFC 5321 local part limit
    public const int MaxDomainLength = 253; // RFC 1035 domain limit
    
    // Lazy-initialized regex for performance (compiled for hot path optimization)
    private static readonly Lazy<Regex> EmailRegexLazy = new(() => EmailValidationRegex(), 
        LazyThreadSafetyMode.ExecutionAndPublication);
    
    // Common free email providers (cached for performance)
    private static readonly HashSet<string> FreeEmailProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "yahoo.com", "yahoo.co.uk", "outlook.com", "hotmail.com",
        "aol.com", "icloud.com", "protonmail.com", "protonmail.ch", 
        "mail.com", "yandex.com", "zoho.com", "gmx.com", "gmx.de"
    };
    
    // Disposable email domains (security consideration)
    private static readonly HashSet<string> DisposableEmailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "guerrillamail.com", "10minutemail.com", 
        "tempmail.com", "throwaway.email", "yopmail.com"
    };
    
    #endregion
    
    #region Constructors
    
    private Email(string normalizedValue) : base(normalizedValue)
    {
        // Pre-compute domain and local parts for performance
        var atIndex = normalizedValue.IndexOf('@');
        _localPart = string.Intern(normalizedValue[..atIndex]);
        _domain = string.Intern(normalizedValue[(atIndex + 1)..]);
    }
    
    #endregion
    
    #region Cached Properties
    
    private readonly string _localPart;
    private readonly string _domain;
    
    public string LocalPart => _localPart;
    public string Domain => _domain;
    
    public string DomainWithoutTld
    {
        get
        {
            var lastDot = _domain.LastIndexOf('.');
            return lastDot > 0 ? _domain[..lastDot] : _domain;
        }
    }
    
    public string TopLevelDomain
    {
        get
        {
            var lastDot = _domain.LastIndexOf('.');
            return lastDot > 0 ? _domain[(lastDot + 1)..] : string.Empty;
        }
    }
    
    #endregion
    
    #region Factory Methods
    
    /// <summary>
    /// Creates an Email with comprehensive validation.
    /// </summary>
    public static Result<Email> Create([NotNullWhen(true)] string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<Email>.Failure(Error.Validation("Email cannot be empty", "EMAIL_EMPTY"));
        
        var trimmed = value.Trim();
        
        // Length validations
        if (trimmed.Length > MaxLength)
            return Result<Email>.Failure(Error.Validation(
                $"Email cannot exceed {MaxLength} characters", 
                "EMAIL_TOO_LONG",
                new Dictionary<string, object> { ["Length"] = trimmed.Length, ["MaxLength"] = MaxLength }));
        
        // Format validation
        if (!EmailRegexLazy.Value.IsMatch(trimmed))
            return Result<Email>.Failure(Error.Validation("Invalid email format", "EMAIL_INVALID_FORMAT"));
        
        var normalized = NormalizeEmail(trimmed);
        var atIndex = normalized.IndexOf('@');
        
        // Additional RFC validations
        if (atIndex <= 0 || atIndex >= normalized.Length - 1)
            return Result<Email>.Failure(Error.Validation("Invalid email structure", "EMAIL_INVALID_STRUCTURE"));
        
        var localPart = normalized[..atIndex];
        var domain = normalized[(atIndex + 1)..];
        
        if (localPart.Length > MaxLocalPartLength)
            return Result<Email>.Failure(Error.Validation(
                $"Email local part cannot exceed {MaxLocalPartLength} characters", 
                "EMAIL_LOCAL_PART_TOO_LONG"));
        
        if (domain.Length > MaxDomainLength)
            return Result<Email>.Failure(Error.Validation(
                $"Email domain cannot exceed {MaxDomainLength} characters", 
                "EMAIL_DOMAIN_TOO_LONG"));
        
        // Check for consecutive dots (RFC violation)
        if (normalized.Contains("..", StringComparison.Ordinal))
            return Result<Email>.Failure(Error.Validation("Email cannot contain consecutive dots", "EMAIL_CONSECUTIVE_DOTS"));
        
        return Result<Email>.Success(new Email(normalized));
    }
    
    /// <summary>
    /// Try pattern for scenarios where Result allocation should be avoided.
    /// </summary>
    public static bool TryCreate([NotNullWhen(true)] string? value, [NotNullWhen(true)] out Email? email)
    {
        email = null;
        
        if (string.IsNullOrWhiteSpace(value))
            return false;
        
        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength || !EmailRegexLazy.Value.IsMatch(trimmed))
            return false;
        
        var normalized = NormalizeEmail(trimmed);
        var atIndex = normalized.IndexOf('@');
        
        if (atIndex <= 0 || atIndex >= normalized.Length - 1)
            return false;
        
        var localPart = normalized[..atIndex];
        var domain = normalized[(atIndex + 1)..];
        
        if (localPart.Length > MaxLocalPartLength || domain.Length > MaxDomainLength)
            return false;
        
        if (normalized.Contains("..", StringComparison.Ordinal))
            return false;
        
        email = new Email(normalized);
        return true;
    }
    
    /// <summary>
    /// Creates an Option<Email> for functional composition.
    /// </summary>
    public static Option<Email> TryCreate(string? value)
    {
        var result = Create(value);
        return result.IsSuccess ? Option<Email>.Some(result.Value) : Option<Email>.None();
    }
    
    /// <summary>
    /// Creates an email from already validated/trusted source (use with caution).
    /// </summary>
    internal static Email CreateUnsafe(string normalizedValue) => new(normalizedValue);
    
    #endregion
    
    #region Validation
    
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        if (string.IsNullOrWhiteSpace(Value))
        {
            errors.Add(Error.Validation("Email cannot be empty", "EMAIL_EMPTY"));
        }
        else
        {
            if (Value.Length > MaxLength)
                errors.Add(Error.Validation($"Email cannot exceed {MaxLength} characters", "EMAIL_TOO_LONG"));
            
            if (!EmailRegexLazy.Value.IsMatch(Value))
                errors.Add(Error.Validation("Invalid email format", "EMAIL_INVALID_FORMAT"));
            
            if (_localPart.Length > MaxLocalPartLength)
                errors.Add(Error.Validation($"Email local part cannot exceed {MaxLocalPartLength} characters", "EMAIL_LOCAL_PART_TOO_LONG"));
            
            if (_domain.Length > MaxDomainLength)
                errors.Add(Error.Validation($"Email domain cannot exceed {MaxDomainLength} characters", "EMAIL_DOMAIN_TOO_LONG"));
        }
        
        return errors.Count == 0 
            ? Validation.Valid(Unit.Value) 
            : Validation.Invalid<Unit>(errors);
    }
    
    #endregion
    
    #region Domain Logic
    
    /// <summary>
    /// Checks if email belongs to a specific domain (case-insensitive).
    /// </summary>
    public bool IsFromDomain(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _domain.Equals(domain.Trim(), StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Checks if email is from any of the specified domains.
    /// </summary>
    public bool IsFromAnyDomain(params string[] domains)
    {
        ArgumentNullException.ThrowIfNull(domains);
        return domains.Any(d => !string.IsNullOrWhiteSpace(d) && IsFromDomain(d));
    }
    
    /// <summary>
    /// Checks if email is from a subdomain of the specified domain.
    /// </summary>
    public bool IsFromSubdomainOf(string parentDomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDomain);
        var normalized = parentDomain.Trim().ToLowerInvariant();
        return _domain.EndsWith($".{normalized}", StringComparison.Ordinal);
    }
    
    /// <summary>
    /// Determines if this is a free email provider (Gmail, Yahoo, etc.).
    /// </summary>
    public bool IsFreeEmailProvider() => FreeEmailProviders.Contains(_domain);
    
    /// <summary>
    /// Determines if this is a known disposable/temporary email address.
    /// </summary>
    public bool IsDisposableEmail() => DisposableEmailDomains.Contains(_domain);
    
    /// <summary>
    /// Determines if this appears to be a corporate email (not free/disposable).
    /// </summary>
    public bool IsCorporateEmail() => !IsFreeEmailProvider() && !IsDisposableEmail();
    
    /// <summary>
    /// Checks if email has a plus alias (e.g., user+alias@domain.com).
    /// </summary>
    public bool HasPlusAlias() => _localPart.Contains('+', StringComparison.Ordinal);
    
    /// <summary>
    /// Gets the base email without plus alias if present.
    /// </summary>
    public Email GetBaseEmail()
    {
        if (!HasPlusAlias())
            return this;
        
        var plusIndex = _localPart.IndexOf('+');
        var baseLocalPart = _localPart[..plusIndex];
        var baseEmail = $"{baseLocalPart}@{_domain}";
        
        return CreateUnsafe(baseEmail);
    }
    
    /// <summary>
    /// Creates a new email with a plus alias.
    /// </summary>
    public Result<Email> WithPlusAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return Result<Email>.Failure(Error.Validation("Alias cannot be empty", "EMAIL_ALIAS_EMPTY"));
        
        if (alias.Contains('@', StringComparison.Ordinal) || alias.Contains('+', StringComparison.Ordinal))
            return Result<Email>.Failure(Error.Validation("Invalid alias format", "EMAIL_ALIAS_INVALID"));
        
        var baseEmail = HasPlusAlias() ? GetBaseEmail() : this;
        var newEmail = $"{baseEmail._localPart}+{alias}@{baseEmail._domain}";
        
        return Create(newEmail);
    }
    
    #endregion
    
    #region Helpers
    
    private static string NormalizeEmail(string email)
    {
        // Normalize to lowercase and apply punycode for international domains
        var parts = email.Split('@');
        if (parts.Length != 2) 
            return email.ToLowerInvariant();
        
        var localPart = parts[0].ToLowerInvariant();
        var domain = parts[1].ToLowerInvariant();
        
        // Handle internationalized domain names (IDN)
        try
        {
            var idn = new IdnMapping();
            domain = idn.GetAscii(domain);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch
        {
            // If IDN conversion fails, use original domain
        }
#pragma warning restore CA1031
        
        return $"{localPart}@{domain}";
    }
    
    [GeneratedRegex(
        @"^[a-zA-Z0-9!#$%&'*+\-/=?^_`{|}~]+(?:\.[a-zA-Z0-9!#$%&'*+\-/=?^_`{|}~]+)*@(?:[a-zA-Z0-9](?:[a-zA-Z0-9\-]*[a-zA-Z0-9])?\.)+[a-zA-Z0-9](?:[a-zA-Z0-9\-]*[a-zA-Z0-9])?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailValidationRegex();
    
    #endregion
    
    #region Formatting
    
    /// <summary>
    /// Returns the email in its normalized form.
    /// </summary>
    public override string ToString() => Value;
    
    /// <summary>
    /// Returns a masked version of the email for display purposes.
    /// </summary>
    public string ToMaskedString()
    {
        if (_localPart.Length <= 2)
            return $"**@{_domain}";
        
        var visibleChars = Math.Min(2, _localPart.Length / 3);
        var masked = _localPart[..visibleChars] + new string('*', Math.Min(4, _localPart.Length - visibleChars));
        return $"{masked}@{_domain}";
    }
    
    #endregion
}