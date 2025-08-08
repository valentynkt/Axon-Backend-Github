using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Interface for masking sensitive data in logs for Epic 05 Story 06.
/// Provides configurable PII protection strategies.
/// </summary>
public interface ISensitiveDataMasker
{
    object MaskSensitiveData(object data);
    bool ContainsSensitiveData(PropertyInfo property);
}

/// <summary>
/// Attribute to mark properties containing sensitive data.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class SensitiveDataAttribute : Attribute
{
    public MaskingStrategy Strategy { get; init; } = MaskingStrategy.Full;
}

/// <summary>
/// Different strategies for masking sensitive data.
/// </summary>
public enum MaskingStrategy
{
    Full,        // ********
    Partial,     // Jo***oe
    Email,       // u***@example.com
    Phone,       // ***-***-1234
    CreditCard,  // ****-****-****-1234
    Hash         // SHA256 hash for correlation
}

/// <summary>
/// Implementation of sensitive data masker with comprehensive PII protection.
/// </summary>
public sealed partial class SensitiveDataMasker : ISensitiveDataMasker
{
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pwd", "secret", "token", "key", "apikey", "auth",
        "ssn", "social", "socialsecurity", "socialSecurityNumber",
        "credit", "creditcard", "card", "cvv", "cvc", "cardNumber",
        "email", "emailaddress", "phone", "phonenumber", "mobile",
        "address", "streetaddress", "homeaddress", "workaddress",
        "dob", "dateofbirth", "birthdate", "birthday",
        "license", "passport", "driverslicense", "id", "personalid"
    };

    private static readonly Dictionary<MaskingStrategy, Func<string, string>> MaskingFunctions = new()
    {
        [MaskingStrategy.Full] = value => new string('*', Math.Min(value.Length, 8)),
        [MaskingStrategy.Partial] = MaskPartial,
        [MaskingStrategy.Email] = MaskEmail,
        [MaskingStrategy.Phone] = MaskPhone,
        [MaskingStrategy.CreditCard] = MaskCreditCard,
        [MaskingStrategy.Hash] = HashValue
    };

    public object MaskSensitiveData(object data)
    {
        if (data == null)
            return data;

        // Handle primitive types and strings
        if (data is string stringValue)
        {
            return MaskStringValue(stringValue);
        }

        if (data.GetType().IsPrimitive)
        {
            return data; // Don't mask primitive types unless they're strings
        }

        // Handle collections
        if (data is System.Collections.IEnumerable enumerable and not string)
        {
            return MaskCollection(enumerable);
        }

        // Handle complex objects
        return MaskObject(data);
    }

    public bool ContainsSensitiveData(PropertyInfo property)
    {
        // Check for SensitiveDataAttribute
        if (property.GetCustomAttribute<SensitiveDataAttribute>() != null)
        {
            return true;
        }

        // Check property name against known sensitive patterns
        return SensitivePropertyNames.Contains(property.Name) ||
               SensitivePropertyNames.Any(pattern => 
                   property.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static string MaskStringValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Detect and mask common patterns
        if (IsEmail(value))
            return MaskEmail(value);
        
        if (IsPhone(value))
            return MaskPhone(value);
        
        if (IsCreditCard(value))
            return MaskCreditCard(value);

        // Default to partial masking for unidentified strings
        return MaskPartial(value);
    }

    private object MaskObject(object obj)
    {
        var type = obj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        var maskedObject = new Dictionary<string, object?>();
        
        foreach (var property in properties)
        {
            try
            {
                var value = property.GetValue(obj);
                if (value == null)
                {
                    maskedObject[property.Name] = null;
                    continue;
                }

                if (ContainsSensitiveData(property))
                {
                    var attribute = property.GetCustomAttribute<SensitiveDataAttribute>();
                    var strategy = attribute?.Strategy ?? MaskingStrategy.Full;
                    
                    if (value is string stringValue)
                    {
                        maskedObject[property.Name] = MaskingFunctions[strategy](stringValue);
                    }
                    else
                    {
                        maskedObject[property.Name] = new string('*', 8);
                    }
                }
                else
                {
                    maskedObject[property.Name] = MaskSensitiveData(value);
                }
            }
            catch (Exception)
            {
                // If we can't read the property, don't include it
                maskedObject[property.Name] = "[UNABLE_TO_READ]";
            }
        }

        return maskedObject;
    }

    private object MaskCollection(System.Collections.IEnumerable enumerable)
    {
        var maskedItems = new List<object>();
        foreach (var item in enumerable)
        {
            maskedItems.Add(MaskSensitiveData(item));
        }
        return maskedItems;
    }

    // Masking strategy implementations
    private static string MaskPartial(string value)
    {
        if (value.Length <= 2)
            return new string('*', value.Length);
        
        if (value.Length <= 4)
            return value[0] + new string('*', value.Length - 2) + value[^1];
            
        return value[..2] + new string('*', value.Length - 4) + value[^2..];
    }

    private static string MaskEmail(string email)
    {
        if (!IsEmail(email))
            return MaskPartial(email);
            
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return MaskPartial(email);
            
        var localPart = email[..atIndex];
        var domainPart = email[atIndex..];
        
        var maskedLocal = localPart.Length <= 2 
            ? new string('*', localPart.Length)
            : localPart[0] + new string('*', localPart.Length - 2) + localPart[^1];
            
        return maskedLocal + domainPart;
    }

    private static string MaskPhone(string phone)
    {
        // Remove common phone formatting
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        
        if (digits.Length < 4)
            return new string('*', phone.Length);
            
        var lastFour = digits[^4..];
        var maskedDigits = new string('*', digits.Length - 4) + lastFour;
        
        // Try to preserve original formatting
        var result = phone;
        for (int i = 0, j = 0; i < result.Length && j < maskedDigits.Length; i++)
        {
            if (char.IsDigit(result[i]))
            {
                result = result.Remove(i, 1).Insert(i, maskedDigits[j].ToString());
                j++;
            }
        }
        
        return result;
    }

    private static string MaskCreditCard(string cardNumber)
    {
        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        
        if (digits.Length < 4)
            return new string('*', cardNumber.Length);
            
        var lastFour = digits[^4..];
        var masked = new string('*', digits.Length - 4) + lastFour;
        
        // Common credit card formatting: XXXX-XXXX-XXXX-1234
        if (digits.Length == 16)
        {
            return $"****-****-****-{lastFour}";
        }
        
        return masked;
    }

    private static string HashValue(string value)
    {
        var hashedBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hashedBytes)[..16]; // First 16 chars for correlation
    }

    // Pattern detection helpers
    private static bool IsEmail(string value) =>
        EmailRegex().IsMatch(value);

    private static bool IsPhone(string value) =>
        PhoneRegex().IsMatch(value);

    private static bool IsCreditCard(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length >= 13 && digits.Length <= 19 &&
               CreditCardRegex().IsMatch(digits);
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex EmailRegex();
    [GeneratedRegex(@"^[\+]?[\d\s\-\(\)\.]{7,15}$")]
    private static partial Regex PhoneRegex();
    [GeneratedRegex(@"^\d{13,19}$")]
    private static partial Regex CreditCardRegex();
}