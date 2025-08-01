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

    private async Task ValidateWeakCryptography(List<Type> types, List<RuleViolation> violations)
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
                        violations.Add(new RuleViolation(
                            $"Method '{method.Name}' in '{type.Name}' uses weak cryptographic algorithm '{weakAlgorithm}'",
                            type.FullName!,
                            method.Name));
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
                            violations.Add(new RuleViolation(
                                $"Method '{method.Name}' in '{type.Name}' accepts weak cryptographic type '{weakAlgorithm}'",
                                type.FullName!,
                                method.Name));
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
                        violations.Add(new RuleViolation(
                            $"Field '{field.Name}' in '{type.Name}' uses weak cryptographic type '{weakAlgorithm}'",
                            type.FullName!,
                            field.Name));
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private async Task ValidateKeyManagement(List<Type> types, List<RuleViolation> violations)
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
                        violations.Add(new RuleViolation(
                            $"Field '{field.Name}' in '{type.Name}' appears to contain hardcoded cryptographic material",
                            type.FullName!,
                            field.Name));
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
                        violations.Add(new RuleViolation(
                            $"Property '{property.Name}' in '{type.Name}' stores cryptographic material without protection attributes",
                            type.FullName!,
                            property.Name));
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private async Task ValidateRandomGeneration(List<Type> types, List<RuleViolation> violations)
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
                        violations.Add(new RuleViolation(
                            $"Method '{method.Name}' in '{type.Name}' uses weak random number generation (System.Random)",
                            type.FullName!,
                            method.Name));
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private async Task ValidateCertificateHandling(List<Type> types, List<RuleViolation> violations)
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
                            violations.Add(new RuleViolation(
                                $"Method '{method.Name}' in '{type.Name}' appears to bypass certificate validation",
                                type.FullName!,
                                method.Name));
                        }
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }
}