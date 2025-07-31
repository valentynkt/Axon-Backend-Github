using System.Reflection;
using NetArchTest.Rules;

namespace Axon.ArchitectureTests;

/// <summary>
/// Architecture tests to enforce Clean Architecture layer boundaries and dependency rules.
/// Validates that layers only depend on allowed layers and no circular dependencies exist.
/// </summary>
[TestFixture]
public class CleanArchitectureRules
{
    private const string ApiLayer = "Axon.Api";
    private const string ApplicationLayer = "Axon.Modules.*.Application";
    private const string DomainLayer = "Axon.Modules.*.Domain";
    private const string InfrastructureLayer = "Axon.Modules.*.Infrastructure";
    private const string SharedCommon = "Axon.Shared.Common";
    private const string SharedAbstractions = "Axon.Shared.Common.Abstractions";

    [Test]
    public void Api_ShouldNotDependOn_Domain()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace(ApiLayer)
            .ShouldNot()
            .HaveDependencyOn(DomainLayer)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            ArchitectureTestHelpers.FormatViolations(result.FailingTypeNames, 
                "API layer should never depend on Domain layer directly. Use Application layer as mediator"));
    }

    [Test]
    public void Api_ShouldOnlyDependOn_ApplicationAndShared()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace(ApiLayer)
            .Should()
            .OnlyHaveDependenciesOn(ApplicationLayer, SharedCommon, SharedAbstractions, "System", "Microsoft", "MediatR")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            ArchitectureTestHelpers.FormatViolations(result.FailingTypeNames,
                "API layer should only depend on Application layer and Shared components"));
    }

    [Test]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace(ApplicationLayer)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureLayer)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            ArchitectureTestHelpers.FormatViolations(result.FailingTypeNames,
                "Application layer should not depend on Infrastructure layer. Use dependency inversion with interfaces"));
    }

    [Test]
    public void Application_ShouldOnlyDependOn_DomainAndShared()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace(ApplicationLayer)
            .Should()
            .OnlyHaveDependenciesOn(DomainLayer, SharedCommon, SharedAbstractions, "System", "Microsoft", "MediatR", "FluentValidation")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            ArchitectureTestHelpers.FormatViolations(result.FailingTypeNames,
                "Application layer should only depend on Domain layer and Shared components"));
    }

    [Test]
    public void Domain_ShouldHaveNoDependencyOnOtherLayers()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace(DomainLayer)
            .ShouldNot()
            .HaveDependencyOnAny(ApiLayer, ApplicationLayer, InfrastructureLayer)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            ArchitectureTestHelpers.FormatViolations(result.FailingTypeNames,
                "Domain layer should be dependency-free from other application layers"));
    }

    [Test]
    public void Domain_ShouldOnlyDependOn_SharedCommon()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace(DomainLayer)
            .Should()
            .OnlyHaveDependenciesOn(SharedCommon, SharedAbstractions, "System")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            ArchitectureTestHelpers.FormatViolations(result.FailingTypeNames,
                "Domain layer should only depend on Shared.Common and system libraries"));
    }

    [Test]
    public void Infrastructure_ShouldDependOn_ApplicationAndDomain()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace(InfrastructureLayer)
            .Should()
            .HaveDependencyOnAny(ApplicationLayer, DomainLayer)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            ArchitectureTestHelpers.FormatViolations(result.FailingTypeNames,
                "Infrastructure layer should depend on Application and/or Domain layers"));
    }

    [Test]
    public void Modules_ShouldNotHaveCrossModuleDependencies()
    {
        var chatTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon.Modules.Chat")
            .GetTypes();

        var violations = new List<string>();

        foreach (var type in chatTypes)
        {
            var dependencies = ArchitectureTestHelpers.GetReferencedTypes(type)
                .Where(t => t.Namespace?.StartsWith("Axon.Modules.") == true)
                .Where(t => !t.Namespace?.StartsWith("Axon.Modules.Chat") == true)
                .ToList();

            if (dependencies.Any())
            {
                violations.Add($"{type.FullName} depends on: {string.Join(", ", dependencies.Select(d => d.FullName))}");
            }
        }

        violations.ShouldBeEmpty(
            $"Modules should not have cross-module dependencies. Use Integration Events instead. " +
            $"Violations: {string.Join("; ", violations)}");
    }

    [Test]
    public void Shared_ShouldNotDependOn_Modules()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon.Shared")
            .ShouldNot()
            .HaveDependencyOn("Axon.Modules")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            ArchitectureTestHelpers.FormatViolations(result.FailingTypeNames,
                "Shared components should not depend on any modules"));
    }

    [Test]
    public void Shared_ShouldNotContainBusinessLogic()
    {
        var businessTerms = new[] { "Chat", "Message", "Conversation", "Process", "User", "Customer", "Order", "Product" };
        
        var sharedTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("Axon.Shared")
            .GetTypes();

        var violations = sharedTypes
            .Where(type => businessTerms.Any(term => 
                type.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                type.Namespace?.Contains(term, StringComparison.OrdinalIgnoreCase) == true))
            .Select(type => type.FullName)
            .ToList();

        violations.ShouldBeEmpty(
            $"Shared components should not contain business logic or domain terms. " +
            $"Use technical names only. Violations: {string.Join(", ", violations)}");
    }

    #region Repository Pattern Tests

    [Test]
    public void Repositories_ShouldBeIn_InfrastructureLayer()
    {
        var repositoryTypes = RepositoryArchitectureHelpers.GetRepositoryImplementations();
        var violations = repositoryTypes
            .Where(type => type.Namespace?.StartsWith("Axon.Modules.") != true ||
                          !type.Namespace?.Contains(".Infrastructure") == true)
            .Select(type => type.FullName)
            .Where(name => name != null)
            .Cast<string>()
            .ToList();

        violations.ShouldBeEmpty(
            ArchitectureTestHelpers.FormatViolations(violations,
                "Repository implementations should be in Infrastructure layer"));
    }

    [Test]
    public void Repositories_ShouldImplement_IRepository()
    {
        var repositoryTypes = RepositoryArchitectureHelpers.GetRepositoryImplementations();
        var violations = repositoryTypes
            .Where(type => !type.GetInterfaces()
                .Any(i => i.Name.Contains("Repository") || i.Name.StartsWith("IRepository")))
            .Select(type => type.FullName)
            .Where(name => name != null)
            .Cast<string>()
            .ToList();

        violations.ShouldBeEmpty(
            ArchitectureTestHelpers.FormatViolations(violations,
                "Repository implementations should implement IRepository interface"));
    }

    [Test]
    public void Repositories_ShouldNotExpose_EntityFrameworkTypes()
    {
        var repositoryTypes = RepositoryArchitectureHelpers.GetRepositoryImplementations();
        var efTypes = RepositoryArchitectureHelpers.GetEntityFrameworkTypes();
        var violations = new List<string>();

        foreach (var repoType in repositoryTypes)
        {
            var publicMethods = repoType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName && m.DeclaringType == repoType);

            foreach (var method in publicMethods)
            {
                if (RepositoryArchitectureHelpers.ExposesEntityFrameworkTypes(method, efTypes))
                {
                    violations.Add($"{repoType.FullName}.{method.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            ArchitectureTestHelpers.FormatViolations(violations,
                "Repository methods should not expose Entity Framework types in public API"));
    }

    [Test]
    public void RepositoryInterfaces_ShouldBeIn_DomainLayer()
    {
        var repositoryInterfaces = RepositoryArchitectureHelpers.GetRepositoryInterfaces();
        var violations = repositoryInterfaces
            .Where(type => type.Namespace?.StartsWith("Axon.Modules.") != true ||
                          !type.Namespace?.Contains(".Domain") == true)
            .Select(type => type.FullName)
            .Where(name => name != null)
            .Cast<string>()
            .ToList();

        violations.ShouldBeEmpty(
            ArchitectureTestHelpers.FormatViolations(violations,
                "Repository interfaces should be in Domain layer"));
    }

    [Test]
    public void Repositories_ShouldWork_WithAggregateRootsOnly()
    {
        var repositoryInterfaces = RepositoryArchitectureHelpers.GetRepositoryInterfaces();
        var aggregateRootTypes = RepositoryArchitectureHelpers.GetAggregateRootTypes();
        var violations = new List<string>();

        foreach (var repoInterface in repositoryInterfaces)
        {
            var genericArgs = repoInterface.GetGenericArguments();
            if (genericArgs.Length > 0)
            {
                var entityType = genericArgs[0];
                if (!RepositoryArchitectureHelpers.IsAggregateRoot(entityType, aggregateRootTypes))
                {
                    violations.Add($"{repoInterface.FullName} works with {entityType.FullName}");
                }
            }
        }

        violations.ShouldBeEmpty(
            ArchitectureTestHelpers.FormatViolations(violations,
                "Repositories should only work with Aggregate Root entities"));
    }

    #endregion

}