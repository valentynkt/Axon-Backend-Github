using System.Reflection;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.Infrastructure;

/// <summary>
/// Rule for validating repository pattern implementation.
/// </summary>
public sealed class RepositoryImplementationRule : PatternComplianceRule
{
    public override string RuleId => "INFRA001";
    public override string Name => "Repository Implementation Rule";
    public override string Description => "Repository implementations must follow established patterns for data access";
    public override string Category => "Infrastructure";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var repositoryTypes = context.Types
                .Where(t => IsRepositoryType(t))
                .ToList();

            foreach (var repositoryType in repositoryTypes)
            {
                ValidateRepositoryNaming(repositoryType, violations);
                ValidateRepositoryStructure(repositoryType, violations);
                ValidateRepositoryLocation(repositoryType, violations);
                ValidateRepositoryInterface(repositoryType, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsRepositoryType(Type type) =>
        type.Name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase) ||
        IsInRepositoryNamespace(type);

    private static bool IsInRepositoryNamespace(Type type) =>
        type.Namespace?.Contains(".Repositories", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.Contains(".Repository", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.Contains(".Infrastructure", StringComparison.OrdinalIgnoreCase) == true;

    private void ValidateRepositoryNaming(Type repositoryType, List<RuleViolation> violations)
    {
        if (!repositoryType.Name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                repositoryType,
                "Repository types must end with 'Repository' suffix",
                $"Rename '{repositoryType.Name}' to include 'Repository' suffix"));
        }
    }

    private void ValidateRepositoryStructure(Type repositoryType, List<RuleViolation> violations)
    {
        if (repositoryType.IsInterface)
            return;

        // Repository implementations should be sealed
        if (!repositoryType.IsSealed)
        {
            violations.Add(CreateViolation(
                repositoryType,
                "Repository implementations should be sealed",
                $"Make '{repositoryType.Name}' sealed"));
        }
    }

    private void ValidateRepositoryLocation(Type repositoryType, List<RuleViolation> violations)
    {
        if (!IsInRepositoryNamespace(repositoryType))
        {
            violations.Add(CreateViolation(
                repositoryType,
                "Repository types must be placed in appropriate namespace",
                $"Move '{repositoryType.Name}' to .Repositories or .Infrastructure namespace"));
        }
    }

    private void ValidateRepositoryInterface(Type repositoryType, List<RuleViolation> violations)
    {
        if (repositoryType.IsInterface)
            return;

        // Repository implementations should implement an interface
        var repositoryInterfaces = repositoryType.GetInterfaces()
            .Where(i => i.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!repositoryInterfaces.Any())
        {
            violations.Add(CreateViolation(
                repositoryType,
                "Repository implementations should implement a repository interface",
                $"Create and implement an interface for '{repositoryType.Name}'"));
        }
    }
}