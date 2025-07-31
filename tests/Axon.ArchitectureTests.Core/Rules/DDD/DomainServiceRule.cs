using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.DDD;

/// <summary>
/// Validates that Domain Services follow DDD patterns and best practices.
/// </summary>
public sealed class DomainServiceRule : PatternComplianceRule
{
    public override string RuleId => "DDD003";
    public override string Name => "Domain Service Rule";
    public override string Description => "Domain services must be interfaces in Domain layer with stateless implementations";
    public override string Category => "DDD";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var domainServiceTypes = context.Types
                .Where(t => IsDomainService(t))
                .ToList();

            foreach (var serviceType in domainServiceTypes)
            {
                ValidateDomainServiceLocation(serviceType, violations);
                ValidateDomainServiceStructure(serviceType, violations);
                ValidateDomainServiceNaming(serviceType, violations);
                ValidateDomainServiceMethods(serviceType, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsDomainService(Type type) =>
        IsInDomainNamespace(type) &&
        (type.Name.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
         type.Name.EndsWith("DomainService", StringComparison.OrdinalIgnoreCase) ||
         HasDomainServiceCharacteristics(type));

    private static bool IsInDomainNamespace(Type type) =>
        type.Namespace?.Contains(".Domain.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase) == true;

    private static bool HasDomainServiceCharacteristics(Type type) =>
        type.IsInterface && 
        IsInDomainNamespace(type) &&
        type.GetMethods().Any(m => !m.IsSpecialName);

    private void ValidateDomainServiceLocation(Type serviceType, List<RuleViolation> violations)
    {
        if (!IsInDomainNamespace(serviceType))
        {
            violations.Add(CreateViolation(
                serviceType,
                "Domain services must be placed in the Domain layer",
                $"Move '{serviceType.Name}' to a Domain namespace"));
        }
    }

    private void ValidateDomainServiceStructure(Type serviceType, List<RuleViolation> violations)
    {
        if (!serviceType.IsInterface)
        {
            violations.Add(CreateViolation(
                serviceType,
                "Domain services should be defined as interfaces in the Domain layer",
                $"Convert '{serviceType.Name}' to an interface and implement in Infrastructure layer"));
        }

        // If it's a concrete class in Domain, it should be stateless
        if (serviceType.IsClass && IsInDomainNamespace(serviceType))
        {
            var instanceFields = serviceType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .Where(f => !f.IsInitOnly && !f.IsLiteral)
                .ToList();

            if (instanceFields.Any())
            {
                violations.Add(CreateViolation(
                    serviceType,
                    "Domain service implementations should be stateless",
                    $"Remove mutable state from '{serviceType.Name}' or move to Infrastructure layer"));
            }
        }
    }

    private void ValidateDomainServiceNaming(Type serviceType, List<RuleViolation> violations)
    {
        var typeName = serviceType.Name;
        
        if (!typeName.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                serviceType,
                "Domain services should have 'Service' suffix",
                $"Rename '{typeName}' to '{typeName}Service'"));
        }

        // Interface naming convention
        if (serviceType.IsInterface && !typeName.StartsWith("I", StringComparison.Ordinal))
        {
            violations.Add(CreateViolation(
                serviceType,
                "Domain service interfaces should start with 'I' prefix",
                $"Rename '{typeName}' to 'I{typeName}'"));
        }
    }

    private void ValidateDomainServiceMethods(Type serviceType, List<RuleViolation> violations)
    {
        if (!serviceType.IsInterface) return;

        var methods = serviceType.GetMethods().Where(m => !m.IsSpecialName).ToList();
        
        foreach (var method in methods)
        {
            // Domain service methods should be focused on domain operations
            if (method.Name.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                method.Name.StartsWith("Find", StringComparison.OrdinalIgnoreCase) ||
                method.Name.StartsWith("List", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(CreateViolation(
                    serviceType,
                    $"Domain service method '{method.Name}' appears to be a query operation - consider using Repository pattern instead",
                    $"Move query operations to Repository or create a separate Query service"));
            }

            // Should represent domain operations
            if (method.Name.StartsWith("Save", StringComparison.OrdinalIgnoreCase) ||
                method.Name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ||
                method.Name.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(CreateViolation(
                    serviceType,
                    $"Domain service method '{method.Name}' appears to be a persistence operation - use Repository pattern instead",
                    $"Move persistence operations to Repository pattern"));
            }
        }
    }
}