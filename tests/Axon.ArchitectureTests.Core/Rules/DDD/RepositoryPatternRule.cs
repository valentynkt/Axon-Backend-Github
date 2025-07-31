using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.DDD;

/// <summary>
/// Validates that Repository pattern is properly implemented following DDD practices.
/// </summary>
public sealed class RepositoryPatternRule : PatternComplianceRule
{
    public override string RuleId => "DDD004";
    public override string Name => "Repository Pattern Rule";
    public override string Description => "Repository interfaces must be in Domain layer, implementations in Infrastructure layer";
    public override string Category => "DDD";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var repositoryTypes = context.Types
                .Where(t => IsRepository(t))
                .ToList();

            foreach (var repositoryType in repositoryTypes)
            {
                ValidateRepositoryLocation(repositoryType, violations);
                ValidateRepositoryStructure(repositoryType, violations);
                ValidateRepositoryNaming(repositoryType, violations);
                ValidateRepositoryMethods(repositoryType, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsRepository(Type type) =>
        type.Name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase) ||
        type.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase);

    private void ValidateRepositoryLocation(Type repositoryType, List<RuleViolation> violations)
    {
        var isInterface = repositoryType.IsInterface;
        var isInDomain = IsInDomainNamespace(repositoryType);
        var isInInfrastructure = IsInInfrastructureNamespace(repositoryType);

        if (isInterface && !isInDomain)
        {
            violations.Add(CreateViolation(
                repositoryType,
                "Repository interfaces must be placed in the Domain layer",
                $"Move interface '{repositoryType.Name}' to Domain layer"));
        }

        if (!isInterface && !isInInfrastructure)
        {
            violations.Add(CreateViolation(
                repositoryType,
                "Repository implementations must be placed in the Infrastructure layer",
                $"Move implementation '{repositoryType.Name}' to Infrastructure layer"));
        }
    }

    private void ValidateRepositoryStructure(Type repositoryType, List<RuleViolation> violations)
    {
        if (repositoryType.IsInterface) return;

        // Implementation should be sealed
        if (!IsSealed(repositoryType))
        {
            violations.Add(CreateViolation(
                repositoryType,
                "Repository implementations should be sealed",
                $"Make '{repositoryType.Name}' sealed"));
        }

        // Should implement a corresponding interface
        var expectedInterfaceName = $"I{repositoryType.Name}";
        var implementsExpectedInterface = repositoryType.GetInterfaces()
            .Any(i => i.Name.Equals(expectedInterfaceName, StringComparison.OrdinalIgnoreCase));

        if (!implementsExpectedInterface)
        {
            violations.Add(CreateViolation(
                repositoryType,
                $"Repository implementation should implement interface '{expectedInterfaceName}'",
                $"Create and implement interface '{expectedInterfaceName}' in Domain layer"));
        }
    }

    private void ValidateRepositoryNaming(Type repositoryType, List<RuleViolation> violations)
    {
        var typeName = repositoryType.Name;
        
        if (!typeName.EndsWith("Repository", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                repositoryType,
                "Repository types should have 'Repository' suffix",
                $"Rename '{typeName}' to '{typeName}Repository'"));
        }

        // Interface naming convention
        if (repositoryType.IsInterface && !typeName.StartsWith("I", StringComparison.Ordinal))
        {
            violations.Add(CreateViolation(
                repositoryType,
                "Repository interfaces should start with 'I' prefix",
                $"Rename '{typeName}' to 'I{typeName}'"));
        }
    }

    private void ValidateRepositoryMethods(Type repositoryType, List<RuleViolation> violations)
    {
        if (!repositoryType.IsInterface) return;

        var methods = repositoryType.GetMethods().Where(m => !m.IsSpecialName).ToList();
        
        foreach (var method in methods)
        {
            // Repository methods should be async
            if (!IsAsyncMethod(method) && !IsCollectionMethod(method))
            {
                violations.Add(CreateViolation(
                    repositoryType,
                    $"Repository method '{method.Name}' should be async for better performance",
                    $"Make '{method.Name}' async and return Task<T>"));
            }

            // Check for inappropriate business logic in repository
            if (HasBusinessLogicNaming(method.Name))
            {
                violations.Add(CreateViolation(
                    repositoryType,
                    $"Repository method '{method.Name}' appears to contain business logic - keep repositories focused on data access",
                    $"Move business logic from '{method.Name}' to Domain Services or Application layer"));
            }
        }
    }

    private static bool IsInDomainNamespace(Type type) =>
        type.Namespace?.Contains(".Domain.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsInInfrastructureNamespace(Type type) =>
        type.Namespace?.Contains(".Infrastructure.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsCollectionMethod(System.Reflection.MethodInfo method) =>
        method.ReturnType.IsAssignableTo(typeof(System.Collections.IEnumerable)) ||
        method.Name.StartsWith("Count", StringComparison.OrdinalIgnoreCase);

    private static bool HasBusinessLogicNaming(string methodName) =>
        methodName.Contains("Calculate", StringComparison.OrdinalIgnoreCase) ||
        methodName.Contains("Process", StringComparison.OrdinalIgnoreCase) ||
        methodName.Contains("Validate", StringComparison.OrdinalIgnoreCase) ||
        methodName.Contains("Transform", StringComparison.OrdinalIgnoreCase);
}