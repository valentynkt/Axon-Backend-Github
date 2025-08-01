using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Configuration;

/// <summary>
/// Rule to validate secrets management and configuration security patterns.
/// Ensures secrets are properly managed and not exposed in configuration.
/// </summary>
public sealed class SecretsConfigRule : ArchitectureRuleBase
{
    public override string RuleId => "CFG004";
    public override string Name => "Secrets Configuration Rule";
    public override string Description => "Validates secrets management and configuration security";
    public override string Category => "Configuration";
    public override RuleSeverity Severity => RuleSeverity.Critical;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            foreach (var type in context.Types.Where(ShouldValidateType))
            {
                // Check for hardcoded secrets
                ValidateHardcodedSecrets(type, violations);

                // Check secrets handling patterns
                ValidateSecretsHandling(type, violations);

                // Check configuration classes for secret properties
                if (IsConfigurationClass(type))
                {
                    ValidateSecretProperties(type, violations);
                }

                // Check for proper secrets storage patterns
                ValidateSecretsStorage(type, violations);

                // Check for secret exposure in logs/responses
                ValidateSecretExposure(type, violations);

                // Validate encryption patterns
                ValidateEncryptionPatterns(type, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool ShouldValidateType(Type type)
    {
        // Exclude test assemblies and system types from validation
        if (type.IsAbstract || type.IsInterface)
            return false;

        // Exclude test assemblies completely 
        if (IsTestAssembly(type.Assembly))
            return false;

        // Exclude system and framework types
        if (IsSystemOrFrameworkType(type))
            return false;

        return true;
    }

    private static bool IsTestAssembly(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name ?? "";
        return assemblyName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Contains("Tests", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Contains("ArchitectureTests", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSystemOrFrameworkType(Type type)
    {
        var typeNamespace = type.Namespace ?? "";
        return typeNamespace.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
               typeNamespace.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
               typeNamespace.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase) ||
               typeNamespace.StartsWith("Shouldly", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfigurationClass(Type type)
    {
        return type.Name.EndsWith("Options", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Settings", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Config", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Configuration", StringComparison.OrdinalIgnoreCase);
    }

    private void ValidateHardcodedSecrets(Type type, List<RuleViolation> violations)
    {
        // Check constants and static fields for hardcoded secrets
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        
        foreach (var field in fields)
        {
            if (field.IsLiteral || (field.IsInitOnly && field.IsStatic))
            {
                var value = field.GetValue(null)?.ToString();
                if (IsHardcodedSecret(value))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"CRITICAL: Hardcoded secret found in field '{field.Name}'. This is a security vulnerability",
                        "Remove hardcoded secrets and use secure configuration management (Azure Key Vault, User Secrets, etc.)"));
                }

                if (IsSecretField(field.Name) && !string.IsNullOrEmpty(value))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"CRITICAL: Secret field '{field.Name}' has hardcoded value. This is a security risk",
                        "Use IConfiguration or secure secret management instead of hardcoded values"));
                }
            }
        }

        // Check properties for default secret values
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var property in properties)
        {
            if (IsSecretProperty(property.Name))
            {
                try
                {
                    var instance = Activator.CreateInstance(type);
                    var value = property.GetValue(instance)?.ToString();
                    
                    if (!string.IsNullOrEmpty(value) && IsHardcodedSecret(value))
                    {
                        violations.Add(CreateViolation(
                            type,
                            $"CRITICAL: Secret property '{property.Name}' has hardcoded default value",
                            "Remove default values from secret properties and use configuration binding"));
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or TargetParameterCountException or InvalidOperationException)
                {
                    // Ignore reflection errors for complex types
                }
            }
        }
    }

    private void ValidateSecretsHandling(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var method in methods)
        {
            // Check methods that handle secrets
            if (HandlesSecrets(method))
            {
                ValidateSecretHandlingMethod(type, method, violations);
            }

            // Check for secret parameters
            var parameters = method.GetParameters();
            foreach (var parameter in parameters)
            {
                if (IsSecretParameter(parameter.Name))
                {
                    ValidateSecretParameter(type, method, parameter, violations);
                }
            }

            // Check return types for secrets
            if (method.ReturnType != typeof(void) && ReturnsSecret(method))
            {
                ValidateSecretReturn(type, method, violations);
            }
        }
    }

    private void ValidateSecretProperties(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var property in properties)
        {
            if (IsSecretProperty(property.Name))
            {
                // Secret properties should not be exposed in DTOs/responses
                if (IsResponseType(type))
                {
                    var hasJsonIgnore = property.GetCustomAttributes()
                        .Any(attr => attr.GetType().Name.Contains("JsonIgnore"));

                    if (!hasJsonIgnore)
                    {
                        violations.Add(CreateViolation(
                            type,
                            $"CRITICAL: Secret property '{property.Name}' in response type '{type.Name}' should have [JsonIgnore] attribute",
                            "Add [JsonIgnore] attribute to prevent secret exposure in API responses"));
                    }
                }

                // Secret properties should have proper validation
                var hasValidation = property.GetCustomAttributes()
                    .Any(attr => IsValidationAttribute(attr.GetType()));

                if (!hasValidation)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Secret property '{property.Name}' should have validation attributes",
                        "Add [Required] and other appropriate validation attributes to secret properties"));
                }

                // Secret properties should not have public setters in immutable configurations
                if (IsImmutableConfiguration(type) && property.SetMethod?.IsPublic == true)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Secret property '{property.Name}' in immutable configuration should have private setter",
                        "Use private setter or init-only setter for secret properties in immutable configurations"));
                }
            }
        }
    }

    private void ValidateSecretsStorage(Type type, List<RuleViolation> violations)
    {
        // Check for proper secrets storage patterns
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var method in methods)
        {
            if (method.Name.Contains("Configure", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("AddOptions", StringComparison.OrdinalIgnoreCase))
            {
                // Configuration methods should use secure secret storage
                var parameters = method.GetParameters();
                
                var hasConfiguration = parameters.Any(p => 
                    p.ParameterType.Name.Contains("IConfiguration"));

                var hasKeyVault = parameters.Any(p => 
                    p.ParameterType.Name.Contains("KeyVault") ||
                    p.ParameterType.Name.Contains("AzureKeyVault"));

                var hasUserSecrets = CheckForUserSecretsUsage(method);

                if (hasConfiguration && !hasKeyVault && !hasUserSecrets && HandlesSecretsInConfiguration(type))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Configuration method '{method.Name}' should use secure secret storage (Key Vault, User Secrets) for sensitive data",
                        "Integrate Azure Key Vault or User Secrets for secure secret management"));
                }
            }
        }
    }

    private void ValidateSecretExposure(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var method in methods)
        {
            // Check for logging methods that might expose secrets (exclude test framework code)
            if (IsLoggingMethod(method) && !IsTestFrameworkType(type))
            {
                ValidateLoggingSecretExposure(type, method, violations);
            }

            // Check for ToString() methods that might expose secrets
            if (method.Name == "ToString" && method.GetParameters().Length == 0)
            {
                if (type.GetProperties().Any(p => IsSecretProperty(p.Name)))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"ToString() method in type '{type.Name}' with secret properties may expose sensitive data",
                        "Override ToString() to exclude secret properties or use custom formatting"));
                }
            }

            // Check for serialization methods
            if (IsSerializationMethod(method))
            {
                ValidateSerializationSecretExposure(type, method, violations);
            }
        }

        // Check for debug attributes that might expose secrets
        var debugAttributes = type.GetCustomAttributes()
            .Where(attr => attr.GetType().Name.Contains("DebuggerDisplay"))
            .ToList();

        if (debugAttributes.Any() && type.GetProperties().Any(p => IsSecretProperty(p.Name)))
        {
            violations.Add(CreateViolation(
                type,
                $"Type '{type.Name}' with secret properties should not use [DebuggerDisplay] attribute",
                "Remove [DebuggerDisplay] attribute or ensure it doesn't expose secret properties"));
        }
    }

    private static bool IsTestFrameworkType(Type type)
    {
        var typeName = type.FullName ?? "";
        var assemblyName = type.Assembly.GetName().Name ?? "";

        return assemblyName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Mock", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Fake", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("ArchitectureTest", StringComparison.OrdinalIgnoreCase);
    }

    private void ValidateEncryptionPatterns(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var method in methods)
        {
            // Check for encryption/decryption methods
            if (IsEncryptionMethod(method))
            {
                ValidateEncryptionMethod(type, method, violations);
            }

            // Check for hashing methods
            if (IsHashingMethod(method))
            {
                ValidateHashingMethod(type, method, violations);
            }
        }

        // Check for encryption configuration
        if (type.Name.Contains("Encryption", StringComparison.OrdinalIgnoreCase) ||
            type.Name.Contains("Crypto", StringComparison.OrdinalIgnoreCase))
        {
            ValidateEncryptionConfiguration(type, violations);
        }
    }

    private void ValidateSecretHandlingMethod(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        // Methods that handle secrets should be secure
        if (method.IsPublic && !IsControllerAction(method))
        {
            violations.Add(CreateViolation(
                type,
                $"Secret handling method '{method.Name}' should not be public unless it's a controller action",
                "Make secret handling methods private or internal"));
        }

        // Check for proper error handling
        if (!HasTryCatchBlock(method))
        {
            violations.Add(CreateViolation(
                type,
                $"Secret handling method '{method.Name}' should have proper error handling to prevent secret leaks",
                "Add try-catch blocks to prevent secrets from appearing in exception messages"));
        }
    }

    private void ValidateSecretParameter(Type type, MethodInfo method, ParameterInfo parameter, List<RuleViolation> violations)
    {
        // Secret parameters should use SecureString when possible
        if (parameter.ParameterType == typeof(string))
        {
            violations.Add(CreateViolation(
                type,
                $"Secret parameter '{parameter.Name}' in method '{method.Name}' should use SecureString instead of string",
                "Use SecureString for secret parameters to prevent memory dumps"));
        }

        // Secret parameters should have validation attributes
        var hasValidation = parameter.GetCustomAttributes()
            .Any(attr => IsValidationAttribute(attr.GetType()));

        if (!hasValidation)
        {
            violations.Add(CreateViolation(
                type,
                $"Secret parameter '{parameter.Name}' should have validation attributes",
                "Add [Required] and other validation attributes to secret parameters"));
        }
    }

    private void ValidateSecretReturn(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        violations.Add(CreateViolation(
            type,
            $"Method '{method.Name}' returns secret data. Consider returning a wrapper or DTO without sensitive information",
            "Avoid returning raw secret data. Use result wrappers or DTOs that exclude sensitive information"));
    }

    private void ValidateLoggingSecretExposure(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        var parameters = method.GetParameters();
        
        // Check if logging methods might log secrets
        foreach (var parameter in parameters)
        {
            if (parameter.ParameterType == typeof(object) || 
                parameter.ParameterType == typeof(string) ||
                parameter.ParameterType.Name.Contains("Exception"))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Logging method '{method.Name}' should be careful not to log sensitive data",
                    "Implement log sanitization and avoid logging objects that might contain secrets"));
            }
        }
    }

    private void ValidateSerializationSecretExposure(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        if (type.GetProperties().Any(p => IsSecretProperty(p.Name)))
        {
            violations.Add(CreateViolation(
                type,
                $"Serialization method '{method.Name}' in type with secret properties may expose sensitive data",
                "Use custom serialization or [JsonIgnore] attributes to exclude secret properties"));
        }
    }

    private void ValidateEncryptionMethod(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        var parameters = method.GetParameters();
        
        // Encryption methods should not use hardcoded keys
        var hasKeyParameter = parameters.Any(p => 
            p.Name?.Contains("key", StringComparison.OrdinalIgnoreCase) == true);

        if (!hasKeyParameter && method.Name.Contains("Encrypt", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                type,
                $"Encryption method '{method.Name}' should accept encryption key as parameter, not use hardcoded keys",
                "Add key parameter and use secure key management"));
        }

        // Check for weak encryption algorithms
        if (UsesWeakEncryption(method))
        {
            violations.Add(CreateViolation(
                type,
                $"Encryption method '{method.Name}' may use weak encryption algorithms",
                "Use strong encryption algorithms like AES with appropriate key sizes"));
        }
    }

    private void ValidateHashingMethod(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        // Password hashing should use strong algorithms
        if (method.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase) &&
            method.GetParameters().Any(p => IsPasswordParameter(p.Name)))
        {
            var parameters = method.GetParameters();
            var hasSaltParameter = parameters.Any(p => 
                p.Name?.Contains("salt", StringComparison.OrdinalIgnoreCase) == true);

            if (!hasSaltParameter)
            {
                violations.Add(CreateViolation(
                    type,
                    $"Password hashing method '{method.Name}' should use salt parameter",
                    "Add salt parameter for secure password hashing"));
            }

            if (UsesWeakHashing(method))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Password hashing method '{method.Name}' should use strong hashing algorithms like bcrypt or Argon2",
                    "Replace weak hashing algorithms with bcrypt, scrypt, or Argon2"));
            }
        }
    }

    private void ValidateEncryptionConfiguration(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var property in properties)
        {
            if (property.Name.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase))
            {
                // Encryption keys should not be in plain configuration
                violations.Add(CreateViolation(
                    type,
                    $"Encryption configuration property '{property.Name}' should use secure key storage",
                    "Use Azure Key Vault or other secure storage for encryption keys"));
            }
        }
    }

    // Helper methods for detection
    private static bool IsHardcodedSecret(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 8)
            return false;

        // Common secret patterns
        return (value.Contains("key", StringComparison.OrdinalIgnoreCase) && value.Length > 20) ||
               (value.Contains("secret", StringComparison.OrdinalIgnoreCase) && value.Length > 20) ||
               (value.Contains("token", StringComparison.OrdinalIgnoreCase) && value.Length > 20) ||
               value.StartsWith("sk-") || // OpenAI API key pattern
               value.StartsWith("pk_") || // Stripe key pattern
               value.All(c => char.IsLetterOrDigit(c)) && value.Length > 32; // Long alphanumeric strings
    }

    private static bool IsSecretField(string fieldName)
    {
        return fieldName.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
               fieldName.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
               fieldName.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
               fieldName.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
               fieldName.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) ||
               fieldName.Contains("ClientSecret", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSecretProperty(string propertyName)
    {
        return IsSecretField(propertyName);
    }

    private static bool IsSecretParameter(string? parameterName)
    {
        return parameterName != null && IsSecretField(parameterName);
    }

    private static bool IsPasswordParameter(string? parameterName)
    {
        return parameterName?.Contains("password", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool HandlesSecrets(MethodInfo method)
    {
        return method.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
               method.GetParameters().Any(p => IsSecretParameter(p.Name));
    }

    private static bool ReturnsSecret(MethodInfo method)
    {
        return method.Name.Contains("GetSecret", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("GetPassword", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("GetKey", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("GetToken", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsResponseType(Type type)
    {
        return type.Name.Contains("Response", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Result", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Dto", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("ViewModel", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImmutableConfiguration(Type type)
    {
        return type.GetProperties().All(p => p.SetMethod == null || !p.SetMethod.IsPublic);
    }

    private static bool IsValidationAttribute(Type attributeType)
    {
        return attributeType.Name.Contains("Required") ||
               attributeType.Name.Contains("StringLength") ||
               attributeType.Name.Contains("RegularExpression");
    }

    private static bool CheckForUserSecretsUsage(MethodInfo method)
    {
        // Simplified check - in practice you might analyze method body
        return method.GetParameters().Any(p => 
            p.ParameterType.Name.Contains("UserSecrets") ||
            p.Name?.Contains("userSecrets", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool HandlesSecretsInConfiguration(Type type)
    {
        return type.GetProperties().Any(p => IsSecretProperty(p.Name)) ||
               type.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Auth", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoggingMethod(MethodInfo method)
    {
        return method.Name.Contains("Log", StringComparison.OrdinalIgnoreCase) ||
               method.GetParameters().Any(p => p.ParameterType.Name.Contains("ILogger"));
    }

    private static bool IsSerializationMethod(MethodInfo method)
    {
        return method.Name.Contains("Serialize", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("ToJson", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("ToXml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsControllerAction(MethodInfo method)
    {
        return method.GetCustomAttributes().Any(attr => 
            attr.GetType().Name.Contains("Http") ||
            attr.GetType().Name.Contains("Route"));
    }

    private static bool HasTryCatchBlock(MethodInfo method)
    {
        // Simplified check - in practice you would analyze method body
        return method.GetMethodBody() != null;
    }

    private static bool IsEncryptionMethod(MethodInfo method)
    {
        return method.Name.Contains("Encrypt", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Decrypt", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Cipher", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHashingMethod(MethodInfo method)
    {
        return method.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Digest", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesWeakEncryption(MethodInfo method)
    {
        // Simplified check - in practice you would analyze method body or parameters
        return method.GetParameters().Any(p => 
            p.ParameterType.Name.Contains("DES") ||
            p.ParameterType.Name.Contains("RC4"));
    }

    private static bool UsesWeakHashing(MethodInfo method)
    {
        // Simplified check - in practice you would analyze method body
        return method.GetParameters().Any(p => 
            p.ParameterType.Name.Contains("MD5") ||
            p.ParameterType.Name.Contains("SHA1"));
    }
}