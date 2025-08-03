using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Security;

/// <summary>
/// Validates proper cryptography patterns and secure implementations.
/// </summary>
public sealed class CryptographyPatternsRule : ArchitectureRuleBase
{
    public override string RuleId => "SEC-CRYPTO-001";
    public override string Name => "Cryptography Patterns";
    public override string Description => "Validates secure cryptographic implementations and patterns";
    public override string Category => "Security";
    public override RuleSeverity Severity => RuleSeverity.Critical;

    private static readonly string[] WeakCryptoTypes = 
    {
        "MD5", "SHA1", "DES", "RC2", "RC4"
    };

    private static readonly string[] SecureCryptoTypes = 
    {
        "SHA256", "SHA384", "SHA512", "AES", "RSA", "ECDSA"
    };

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {   
        var violations = new List<RuleViolation>();
        var types = context.Types.ToList();

        // Check for weak cryptographic algorithms
        await ValidateWeakCryptography(types, violations);
        
        // Check for proper key management
        await ValidateKeyManagement(types, violations);
        
        // Check for secure random number generation
        await ValidateRandomGeneration(types, violations);
        
        // Check for proper certificate validation
        await ValidateCertificateHandling(types, violations);
        
        return violations;
    }

    private static async Task ValidateWeakCryptography(List<Type> types, List<RuleViolation> violations)
    {
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            
            foreach (var method in methods)
            {
                var methodBody = method.GetMethodBody();
                if (methodBody == null) continue;

                // Check method names and return types for weak crypto indicators
                var methodName = method.Name.ToUpper();
                var returnTypeName = method.ReturnType.Name.ToUpper();
                
                foreach (var weakAlgorithm in WeakCryptoTypes)
                {
                    if (methodName.Contains(weakAlgorithm) || returnTypeName.Contains(weakAlgorithm))
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Method '{method.Name}' in '{type.Name}' uses weak cryptographic algorithm '{weakAlgorithm}'",
                            Severity = RuleSeverity.Critical
                        });
                    }
                }

                // Check parameters for weak crypto types
                foreach (var parameter in method.GetParameters())
                {
                    var parameterTypeName = parameter.ParameterType.Name.ToUpper();
                    foreach (var weakAlgorithm in WeakCryptoTypes)
                    {
                        if (parameterTypeName.Contains(weakAlgorithm))
                        {
                            violations.Add(new RuleViolation
                            {
                                TypeName = type.FullName!,
                                AssemblyName = type.Assembly.FullName!,
                                Message = $"Method '{method.Name}' in '{type.Name}' accepts weak cryptographic type '{weakAlgorithm}'",
                                Severity = RuleSeverity.Critical
                            });
                        }
                    }
                }
            }

            // Check fields and properties for weak crypto types
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var field in fields)
            {
                var fieldTypeName = field.FieldType.Name.ToUpper();
                foreach (var weakAlgorithm in WeakCryptoTypes)
                {
                    if (fieldTypeName.Contains(weakAlgorithm))
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Field '{field.Name}' in '{type.Name}' uses weak cryptographic type '{weakAlgorithm}'",
                            Severity = RuleSeverity.Critical
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateKeyManagement(List<Type> types, List<RuleViolation> violations)
    {
        foreach (var type in types)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var properties = type.GetProperties();
            
            // Check for hardcoded keys in fields
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(string) && field.IsLiteral)
                {
                    var fieldName = field.Name.ToLower();
                    if (fieldName.Contains("key") || fieldName.Contains("secret") || fieldName.Contains("password"))
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Field '{field.Name}' in '{type.Name}' appears to contain hardcoded cryptographic material",
                            Severity = RuleSeverity.Critical
                        });
                    }
                }
            }

            // Check for proper key storage patterns
            foreach (var property in properties)
            {
                var propertyName = property.Name.ToLower();
                if (propertyName.Contains("key") || propertyName.Contains("secret"))
                {
                    var hasSecureStorage = property.GetCustomAttributes()
                        .Any(a => 
                            a.GetType().Name.Contains("Protected") ||
                            a.GetType().Name.Contains("Encrypted") ||
                            a.GetType().Name.Contains("Secure"));

                    if (!hasSecureStorage)
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Property '{property.Name}' in '{type.Name}' stores cryptographic material without protection attributes",
                            Severity = RuleSeverity.Critical
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateRandomGeneration(List<Type> types, List<RuleViolation> violations)
    {
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            
            foreach (var method in methods)
            {
                // Check for usage of weak random number generators
                var methodName = method.Name.ToLower();
                
                if (methodName.Contains("random") || methodName.Contains("generate"))
                {
                    // Check if method uses System.Random instead of cryptographically secure alternatives
                    var parameters = method.GetParameters();
                    var usesWeakRandom = parameters.Any(p => p.ParameterType.Name == "Random");
                    
                    if (usesWeakRandom)
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Method '{method.Name}' in '{type.Name}' uses weak random number generation (System.Random)",
                            Severity = RuleSeverity.Critical
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateCertificateHandling(List<Type> types, List<RuleViolation> violations)
    {
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            
            foreach (var method in methods)
            {
                // Check for certificate validation bypass
                var methodName = method.Name.ToLower();
                
                if (methodName.Contains("certificate") || methodName.Contains("ssl") || methodName.Contains("tls"))
                {
                    // This is a simplified check - in practice, you'd analyze method bodies
                    var returnType = method.ReturnType.Name.ToLower();
                    
                    if (returnType.Contains("bool") && methodName.Contains("validate"))
                    {
                        // Check if there are any attributes indicating this bypasses validation
                        var hasBypassIndicator = method.GetCustomAttributes()
                            .Any(a => a.GetType().Name.ToLower().Contains("ignore") ||
                                    a.GetType().Name.ToLower().Contains("bypass"));
                        
                        if (hasBypassIndicator)
                        {
                            violations.Add(new RuleViolation
                            {
                                TypeName = type.FullName!,
                                AssemblyName = type.Assembly.FullName!,
                                Message = $"Method '{method.Name}' in '{type.Name}' appears to bypass certificate validation",
                                Severity = RuleSeverity.Critical
                            });
                        }
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }
}