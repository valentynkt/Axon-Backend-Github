using System.Reflection;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.Infrastructure;

/// <summary>
/// Rule for validating external service integration patterns.
/// </summary>
public sealed class ExternalServicePatternRule : PatternComplianceRule
{
    public override string RuleId => "INFRA002";
    public override string Name => "External Service Pattern Rule";
    public override string Description => "External service integrations must follow established patterns for reliability and maintainability";
    public override string Category => "Infrastructure";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var externalServiceTypes = context.Types
                .Where(t => IsExternalServiceType(t))
                .ToList();

            foreach (var serviceType in externalServiceTypes)
            {
                ValidateServiceNaming(serviceType, violations);
                ValidateServiceStructure(serviceType, violations);
                ValidateServiceInterface(serviceType, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsExternalServiceType(Type type) =>
        type.Name.EndsWith("Client", StringComparison.OrdinalIgnoreCase) ||
        type.Name.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
        type.Name.Contains("External", StringComparison.OrdinalIgnoreCase) ||
        type.Name.Contains("Api", StringComparison.OrdinalIgnoreCase) ||
        IsInExternalServiceNamespace(type);

    private static bool IsInExternalServiceNamespace(Type type) =>
        type.Namespace?.Contains(".External", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.Contains(".Clients", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.Contains(".Services", StringComparison.OrdinalIgnoreCase) == true;

    private void ValidateServiceNaming(Type serviceType, List<RuleViolation> violations)
    {
        var hasValidSuffix = serviceType.Name.EndsWith("Client", StringComparison.OrdinalIgnoreCase) ||
                           serviceType.Name.EndsWith("Service", StringComparison.OrdinalIgnoreCase);

        if (!hasValidSuffix)
        {
            violations.Add(CreateViolation(
                serviceType,
                "External service types should end with 'Client' or 'Service' suffix",
                $"Rename '{serviceType.Name}' to include appropriate suffix"));
        }
    }

    private void ValidateServiceStructure(Type serviceType, List<RuleViolation> violations)
    {
        if (serviceType.IsInterface)
            return;

        // External service implementations should be sealed
        if (!serviceType.IsSealed)
        {
            violations.Add(CreateViolation(
                serviceType,
                "External service implementations should be sealed",
                $"Make '{serviceType.Name}' sealed"));
        }
    }

    private void ValidateServiceInterface(Type serviceType, List<RuleViolation> violations)
    {
        if (serviceType.IsInterface)
            return;

        // External services should implement interfaces for testability
        var serviceInterfaces = serviceType.GetInterfaces()
            .Where(i => !i.Namespace?.StartsWith("System") == true)
            .ToList();

        if (!serviceInterfaces.Any())
        {
            violations.Add(CreateViolation(
                serviceType,
                "External service implementations should implement interfaces for testability",
                $"Create and implement interface for '{serviceType.Name}'"));
        }
    }
}