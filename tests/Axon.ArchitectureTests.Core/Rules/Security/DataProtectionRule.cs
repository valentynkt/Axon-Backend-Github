using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Security;

/// <summary>
/// Rule to validate data protection patterns and sensitive data handling.
/// </summary>
public sealed class DataProtectionRule : ArchitectureRuleBase
{
    public override string RuleId => "SEC005";
    public override string Name => "Data Protection";
    public override string Description => "Validates that sensitive data is properly protected and not exposed";
    public override string Category => "Security";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(IArchitectureContext context, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            foreach (var assembly in context.Assemblies)
            {
                var types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface);

                foreach (var type in types)
                {
                    // Check for sensitive data in response models
                    if (IsResponseModel(type))
                    {
                        ValidateResponseDataProtection(type, violations);
                    }

                    // Check for logging of sensitive data
                    ValidateLoggingDataProtection(type, violations);

                    // Check for encryption of sensitive fields
                    ValidateDataEncryption(type, violations);

                    // Check for PII handling
                    ValidatePiiHandling(type, violations);

                    // Check for secure serialization
                    ValidateSerializationSecurity(type, violations);
                }
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsResponseModel(Type type) =>
        type.Name.EndsWith("Response", StringComparison.OrdinalIgnoreCase) ||
        type.Name.EndsWith("Result", StringComparison.OrdinalIgnoreCase) ||
        type.Name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase) ||
        type.Name.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase);

    private void ValidateResponseDataProtection(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (IsSensitiveProperty(property.Name))
            {
                // Sensitive data should not be in response models
                violations.Add(CreateViolation(type,
                    $"Sensitive property '{property.Name}' should not be included in response model",
                    "Remove sensitive data from responses or use [JsonIgnore] attribute"));
            }

            // Check for properties that might contain PII without protection
            if (IsPiiProperty(property.Name))
            {
                var hasProtection = property.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.Contains("JsonIgnore") ||
                                attr.GetType().Name.Contains("NonSerialized") ||
                                attr.GetType().Name.Contains("Encrypt") ||
                                attr.GetType().Name.Contains("Mask"));

                if (!hasProtection)
                {
                    violations.Add(CreateViolation(type,
                        $"PII property '{property.Name}' lacks protection in response model",
                        "Add [JsonIgnore], masking, or encryption attributes for PII data"));
                }
            }
        }
    }

    private void ValidateLoggingDataProtection(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        foreach (var method in methods)
        {
            // Look for logging methods
            if (IsLoggingMethod(method.Name))
            {
                var parameters = method.GetParameters();
                
                foreach (var parameter in parameters)
                {
                    if (IsSensitiveParameterName(parameter.Name ?? ""))
                    {
                        violations.Add(CreateViolation(type,
                            $"Logging method '{method.Name}' might log sensitive parameter '{parameter.Name}'",
                            "Ensure sensitive data is masked or excluded from logs"));
                    }
                }
            }

            // Check for methods that might log entire objects containing sensitive data
            if (HasLoggingWithObjectParameter(method))
            {
                violations.Add(CreateViolation(type,
                    $"Method '{method.Name}' might log objects containing sensitive data",
                    "Use structured logging with explicit property selection to avoid logging sensitive data"));
            }
        }
    }

    private void ValidateDataEncryption(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties();

        foreach (var property in properties)
        {
            if (RequiresEncryption(property.Name))
            {
                var hasEncryption = property.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.Contains("Encrypt") ||
                                attr.GetType().Name.Contains("Protected") ||
                                attr.GetType().Name.Contains("Secure"));

                if (!hasEncryption && IsDataModel(type))
                {
                    violations.Add(CreateViolation(type,
                        $"Sensitive property '{property.Name}' should be encrypted at rest",
                        "Add encryption attributes or use data protection APIs"));
                }
            }
        }
    }

    private void ValidatePiiHandling(Type type, List<RuleViolation> violations)
    {
        if (ContainsPii(type))
        {
            // Check for GDPR compliance indicators
            var hasGdprSupport = type.GetMethods()
                .Any(m => m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                         m.Name.Contains("Export", StringComparison.OrdinalIgnoreCase) ||
                         m.Name.Contains("Anonymize", StringComparison.OrdinalIgnoreCase));

            if (!hasGdprSupport && IsEntityModel(type))
            {
                violations.Add(CreateViolation(type,
                    $"Type '{type.Name}' contains PII but lacks GDPR compliance methods",
                    "Implement data deletion, export, and anonymization capabilities for PII"));
            }

            // Check for audit trail on PII modifications
            var hasAuditAttributes = type.GetCustomAttributes()
                .Any(attr => attr.GetType().Name.Contains("Audit") ||
                            attr.GetType().Name.Contains("Track") ||
                            attr.GetType().Name.Contains("Log"));

            if (!hasAuditAttributes && IsEntityModel(type))
            {
                violations.Add(CreateViolation(type,
                    $"PII entity '{type.Name}' should have audit trail capabilities",
                    "Add audit attributes or implement change tracking for PII modifications"));
            }
        }
    }

    private void ValidateSerializationSecurity(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties();
        var hasSensitiveData = properties.Any(p => IsSensitiveProperty(p.Name) || IsPiiProperty(p.Name));

        if (hasSensitiveData)
        {
            // Check for secure serialization patterns
            var hasSecureSerializationAttributes = type.GetCustomAttributes()
                .Any(attr => attr.GetType().Name.Contains("DataContract") ||
                            attr.GetType().Name.Contains("Serializable"));

            if (hasSecureSerializationAttributes)
            {
                // If type is serializable, ensure sensitive properties are excluded
                foreach (var property in properties)
                {
                    if (IsSensitiveProperty(property.Name))
                    {
                        var isExcluded = property.GetCustomAttributes()
                            .Any(attr => attr.GetType().Name.Contains("JsonIgnore") ||
                                        attr.GetType().Name.Contains("IgnoreDataMember") ||
                                        attr.GetType().Name.Contains("NonSerialized"));

                        if (!isExcluded)
                        {
                            violations.Add(CreateViolation(type,
                                $"Serializable type contains unprotected sensitive property '{property.Name}'",
                                "Add [JsonIgnore] or [IgnoreDataMember] to sensitive properties"));
                        }
                    }
                }
            }
        }
    }

    private static bool IsSensitiveProperty(string propertyName)
    {
        var sensitivePatterns = new[]
        {
            "password", "secret", "key", "token", "hash", "salt",
            "ssn", "socialsecurity", "creditcard", "cardnumber", "cvv"
        };

        var propertyNameLower = propertyName.ToLowerInvariant();
        return sensitivePatterns.Any(pattern => propertyNameLower.Contains(pattern));
    }

    private static bool IsPiiProperty(string propertyName)
    {
        var piiPatterns = new[]
        {
            "email", "phone", "address", "firstname", "lastname", "fullname",
            "birthdate", "dateofbirth", "age", "gender", "nationality"
        };

        var propertyNameLower = propertyName.ToLowerInvariant();
        return piiPatterns.Any(pattern => propertyNameLower.Contains(pattern));
    }

    private static bool RequiresEncryption(string propertyName)
    {
        var encryptionPatterns = new[]
        {
            "password", "secret", "key", "token", "ssn", "creditcard",
            "cardnumber", "bankaccount", "routingnumber"
        };

        var propertyNameLower = propertyName.ToLowerInvariant();
        return encryptionPatterns.Any(pattern => propertyNameLower.Contains(pattern));
    }

    private static bool IsSensitiveParameterName(string parameterName)
    {
        var sensitivePatterns = new[]
        {
            "password", "secret", "key", "token", "user", "email", "phone"
        };

        var parameterNameLower = parameterName.ToLowerInvariant();
        return sensitivePatterns.Any(pattern => parameterNameLower.Contains(pattern));
    }

    private static bool IsLoggingMethod(string methodName)
    {
        var loggingPatterns = new[]
        {
            "log", "trace", "debug", "info", "warn", "error", "fatal"
        };

        var methodNameLower = methodName.ToLowerInvariant();
        return loggingPatterns.Any(pattern => methodNameLower.Contains(pattern));
    }

    private static bool HasLoggingWithObjectParameter(MethodInfo method)
    {
        if (!IsLoggingMethod(method.Name))
            return false;

        return method.GetParameters()
            .Any(p => !p.ParameterType.IsPrimitive && 
                     p.ParameterType != typeof(string) && 
                     p.ParameterType != typeof(Exception));
    }

    private static bool ContainsPii(Type type) =>
        type.GetProperties().Any(p => IsPiiProperty(p.Name) || IsSensitiveProperty(p.Name));

    private static bool IsDataModel(Type type) =>
        type.Name.EndsWith("Entity", StringComparison.OrdinalIgnoreCase) ||
        type.Name.EndsWith("Model", StringComparison.OrdinalIgnoreCase) ||
        type.Name.EndsWith("Data", StringComparison.OrdinalIgnoreCase);

    private static bool IsEntityModel(Type type) =>
        type.Name.EndsWith("Entity", StringComparison.OrdinalIgnoreCase) ||
        type.GetCustomAttributes().Any(attr => 
            attr.GetType().Name.Contains("Entity") ||
            attr.GetType().Name.Contains("Table"));
}