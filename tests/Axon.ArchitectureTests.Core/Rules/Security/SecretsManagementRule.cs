using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Security;

/// <summary>
/// Rule to validate secrets management patterns and security practices.
/// </summary>
public sealed class SecretsManagementRule : ArchitectureRuleBase
{
    public override string RuleId => "SEC004";
    public override string Name => "Secrets Management";
    public override string Description => "Validates that secrets are properly managed and not hardcoded";
    public override string Category => "Security";
    public override RuleSeverity Severity => RuleSeverity.Critical;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(IArchitectureContext context, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            foreach (var assembly in context.Assemblies)
            {
                // Skip system assemblies if they somehow got through
                if (IsSystemAssembly(assembly))
                    continue;
                var types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface);

                foreach (var type in types)
                {
                    // Skip architecture test framework types - they legitimately need various patterns
                    if (type.Namespace?.Contains("ArchitectureTests", StringComparison.OrdinalIgnoreCase) == true)
                        continue;
                    // Check for hardcoded secrets in fields and properties
                    ValidateFieldsAndProperties(type, violations);

                    // Check for hardcoded secrets in method bodies (limited to what reflection can see)
                    ValidateMethodSignatures(type, violations);

                    // Check configuration patterns
                    if (IsConfigurationType(type))
                    {
                        ValidateConfigurationSecurity(type, violations);
                    }

                    // Check for proper secret injection patterns
                    ValidateSecretInjectionPatterns(type, violations);
                }
            }
        }, cancellationToken);

        return violations;
    }

    private void ValidateFieldsAndProperties(Type type, List<RuleViolation> violations)
    {
        // Check static fields for hardcoded secrets
        var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        
        foreach (var field in fields)
        {
            if (field.IsLiteral || (field.IsStatic && field.IsInitOnly))
            {
                try
                {
                    var value = field.GetValue(null)?.ToString();
                    if (IsHardcodedSecret(field.Name, value))
                    {
                        violations.Add(CreateViolation(type,
                            $"Field '{field.Name}' appears to contain a hardcoded secret",
                            "Move secrets to configuration, environment variables, or secure storage"));
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or TargetParameterCountException or InvalidOperationException)
                {
                    // Ignore reflection errors
                }
            }

            // Check field names for secret-like patterns
            if (IsSecretFieldName(field.Name))
            {
                violations.Add(CreateViolation(type,
                    $"Field '{field.Name}' appears to be a secret that should not be hardcoded",
                    "Use IConfiguration, IOptions<T>, or secret management services"));
            }
        }

        // Check properties
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var property in properties)
        {
            if (IsSecretPropertyName(property.Name))
            {
                // Check if property is properly configured for secrets
                var hasConfigurationBinding = property.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.Contains("Configuration") ||
                                attr.GetType().Name.Contains("FromServices") ||
                                attr.GetType().Name.Contains("Inject"));

                if (!hasConfigurationBinding && property.CanWrite && property.SetMethod?.IsPublic == true)
                {
                    violations.Add(CreateViolation(type,
                        $"Secret property '{property.Name}' should use configuration binding or dependency injection",
                        "Use [FromConfiguration] or inject via constructor with IOptions<T>"));
                }
            }
        }
    }

    private void ValidateMethodSignatures(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        foreach (var method in methods)
        {
            // Check method parameters for secret-like names
            var parameters = method.GetParameters();
            
            foreach (var parameter in parameters)
            {
                if (IsSecretParameterName(parameter.Name ?? ""))
                {
                    violations.Add(CreateViolation(type,
                        $"Method '{method.Name}' has parameter '{parameter.Name}' that appears to be a secret",
                        "Use configuration objects or secure parameter patterns instead of raw secret parameters"));
                }
            }

            // Check for methods that create or handle secrets
            if (IsSecretHandlingMethod(method.Name))
            {
                ValidateSecretHandlingMethod(type, method, violations);
            }
        }
    }

    private void ValidateConfigurationSecurity(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties();

        foreach (var property in properties)
        {
            if (IsSecretPropertyName(property.Name))
            {
                // Check for validation attributes on secret properties
                var hasValidation = property.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.Contains("Required") ||
                                attr.GetType().Name.Contains("NotNull") ||
                                attr.GetType().Name.Contains("NotEmpty"));

                if (!hasValidation)
                {
                    violations.Add(CreateViolation(type,
                        $"Configuration secret '{property.Name}' should have validation attributes",
                        "Add [Required] or validation attributes to ensure secrets are provided"));
                }

                // Check for proper data protection attributes
                var hasDataProtection = property.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.Contains("DataProtection") ||
                                attr.GetType().Name.Contains("Encrypt") ||
                                attr.GetType().Name.Contains("Secure"));

                if (property.Name.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase) && !hasDataProtection)
                {
                    violations.Add(CreateViolation(type,
                        $"Connection string '{property.Name}' should consider data protection",
                        "Consider using data protection APIs or encrypted configuration"));
                }
            }
        }
    }

    private void ValidateSecretInjectionPatterns(Type type, List<RuleViolation> violations)
    {
        // Check constructors for proper secret injection
        var constructors = type.GetConstructors();

        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            
            foreach (var parameter in parameters)
            {
                // Check for configuration or options pattern usage
                if (IsSecretParameterName(parameter.Name ?? ""))
                {
                    var usesOptionsPattern = parameter.ParameterType.Name.Contains("IOptions") ||
                                           parameter.ParameterType.Name.Contains("IConfiguration");

                    if (!usesOptionsPattern)
                    {
                        violations.Add(CreateViolation(type,
                            $"Constructor parameter '{parameter.Name}' should use IOptions<T> or IConfiguration pattern",
                            "Use IOptions<TOptions> or IConfiguration instead of direct secret injection"));
                    }
                }
            }
        }
    }

    private void ValidateSecretHandlingMethod(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        // Check if secret handling methods have proper security measures
        var hasSecurityAttribute = method.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("Authorize") ||
                        attr.GetType().Name.Contains("RequireHttps") ||
                        attr.GetType().Name.Contains("NonAction"));

        if (method.IsPublic && !hasSecurityAttribute)
        {
            violations.Add(CreateViolation(type,
                $"Secret handling method '{method.Name}' should have security attributes",
                "Add [Authorize], [RequireHttps], or [NonAction] attributes as appropriate"));
        }

        // Check return type - methods shouldn't return raw secrets
        if (method.ReturnType == typeof(string) && 
            (method.Name.Contains("Get", StringComparison.OrdinalIgnoreCase) ||
             method.Name.Contains("Retrieve", StringComparison.OrdinalIgnoreCase)))
        {
            violations.Add(CreateViolation(type,
                $"Secret retrieval method '{method.Name}' should not return raw strings",
                "Return wrapped secret objects or use secure string types"));
        }
    }

    private static bool IsConfigurationType(Type type) =>
        type.Name.Contains("Config", StringComparison.OrdinalIgnoreCase) ||
        type.Name.Contains("Options", StringComparison.OrdinalIgnoreCase) ||
        type.Name.Contains("Settings", StringComparison.OrdinalIgnoreCase);

    private static bool IsSecretFieldName(string fieldName)
    {
        var secretPatterns = new[]
        {
            "password", "secret", "key", "token", "connectionstring", 
            "apikey", "clientsecret", "privatekey", "certificate"
        };

        var fieldNameLower = fieldName.ToLowerInvariant();
        return secretPatterns.Any(pattern => fieldNameLower.Contains(pattern));
    }

    private static bool IsSecretPropertyName(string propertyName)
    {
        var secretPatterns = new[]
        {
            "password", "secret", "key", "token", "connectionstring",
            "apikey", "clientsecret", "privatekey", "certificate", "salt"
        };

        var propertyNameLower = propertyName.ToLowerInvariant();
        return secretPatterns.Any(pattern => propertyNameLower.Contains(pattern));
    }

    private static bool IsSecretParameterName(string parameterName)
    {
        var secretPatterns = new[]
        {
            "password", "secret", "key", "token", "connectionstring",
            "apikey", "clientsecret", "privatekey", "certificate"
        };

        var parameterNameLower = parameterName.ToLowerInvariant();
        return secretPatterns.Any(pattern => parameterNameLower.Contains(pattern));
    }

    private static bool IsSecretHandlingMethod(string methodName)
    {
        // Exclude common system methods that contain "hash" but aren't secret handling
        if (methodName.Equals("GetHashCode", StringComparison.OrdinalIgnoreCase) ||
            methodName.Equals("ComputeHash", StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith("get_", StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith("set_", StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith("ToString", StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith("Equals", StringComparison.OrdinalIgnoreCase))
            return false;

        var secretMethods = new[]
        {
            "encryptpassword", "decryptpassword", "hashpassword", "verifypassword", 
            "authenticateuser", "authorizeuser", "generateaccesstoken", 
            "validateaccesstoken", "createsecretkey", "storesecretkey"
        };

        var methodNameLower = methodName.ToLowerInvariant();
        return secretMethods.Any(pattern => methodNameLower.Contains(pattern));
    }

    private static bool IsHardcodedSecret(string fieldName, string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 8)
            return false;

        // Check for common secret patterns in field names
        if (!IsSecretFieldName(fieldName))
            return false;

        // Check for suspicious value patterns
        var suspiciousPatterns = new[]
        {
            // Common weak passwords or test secrets
            "password", "123456", "admin", "test", "demo",
            // Base64-like patterns
            @"^[A-Za-z0-9+/]+=*$",
            // JWT-like patterns
            @"^ey[A-Za-z0-9]",
            // API key patterns
            @"^[A-Za-z0-9]{32,}$"
        };

        return suspiciousPatterns.Any(pattern => 
            value.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(value, pattern));
    }

    private static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.FullName ?? "";
        return name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase);
    }
}