using System.Text.RegularExpressions;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Options;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.ValueObjects;

/// <summary>
/// Email value object with comprehensive validation following Epic 2 specifications.
/// Demonstrates Result-based factory pattern and functional validation approaches.
/// </summary>
public sealed record Email : SingleValueObject<string>
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
    public const int MaxLength = 254; // RFC 5321 maximum
    
    private Email(string value) : base(value.ToLowerInvariant())
    {
    }
    
    /// <summary>
    /// Create an email with validation using Result pattern
    /// </summary>
    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<Email>.Failure(Error.Validation("Email cannot be empty", "EMAIL_EMPTY"));
            
        value = value.Trim().ToLowerInvariant();
        
        if (value.Length > MaxLength)
            return Result<Email>.Failure(Error.Validation($"Email cannot exceed {MaxLength} characters", "EMAIL_TOO_LONG"));
            
        if (!EmailRegex.IsMatch(value))
            return Result<Email>.Failure(Error.Validation("Invalid email format", "EMAIL_INVALID_FORMAT"));
            
        return Result<Email>.Success(new Email(value));
    }
    
    /// <summary>
    /// Try to create an email, returning None for invalid emails
    /// </summary>
    public static Option<Email> TryCreate(string value)
    {
        var result = Create(value);
        return result.IsSuccess ? Option<Email>.Some(result.Value) : Option<Email>.None();
    }
    
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        if (string.IsNullOrWhiteSpace(Value))
            errors.Add(Error.Validation("Email cannot be empty", "EMAIL_EMPTY"));
        else
        {
            if (Value.Length > MaxLength)
                errors.Add(Error.Validation($"Email cannot exceed {MaxLength} characters", "EMAIL_TOO_LONG"));
                
            if (!EmailRegex.IsMatch(Value))
                errors.Add(Error.Validation("Invalid email format", "EMAIL_INVALID_FORMAT"));
        }
        
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
    
    /// <summary>
    /// Get the domain part of the email
    /// </summary>
    public string Domain => Value.Split('@')[1];
    
    /// <summary>
    /// Get the local part of the email
    /// </summary>
    public string LocalPart => Value.Split('@')[0];
    
    /// <summary>
    /// Check if email is from a specific domain
    /// </summary>
    public bool IsFromDomain(string domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return Domain.Equals(domain, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Check if email is a common free email provider
    /// </summary>
    public bool IsFreeEmailProvider()
    {
        var freeProviders = new[]
        {
            "gmail.com", "yahoo.com", "outlook.com", "hotmail.com", 
            "aol.com", "icloud.com", "protonmail.com"
        };
        
        return freeProviders.Any(provider => 
            Domain.Equals(provider, StringComparison.OrdinalIgnoreCase));
    }
}