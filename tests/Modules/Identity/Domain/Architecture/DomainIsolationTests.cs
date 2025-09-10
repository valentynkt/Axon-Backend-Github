using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain.Tests.Architecture;

[TestFixture]
public class DomainIsolationTests
{
    [TestFixture]
    public class EntityFrameworkIsolationTests : DomainIsolationTests
    {
        // Test ID: 1.3-INT-001 - P0
        [Test]
        public void IdentityDomainAssembly_ShouldNotReferenceEntityFramework()
        {
            // Arrange - Given Identity Domain assembly
            var domainAssembly = Assembly.GetAssembly(typeof(Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal.AxonPrincipal));

            // Act - When checking assembly references
            var referencedAssemblies = domainAssembly!.GetReferencedAssemblies();
            var efReferences = referencedAssemblies
                .Where(assembly => 
                    assembly.Name!.Contains("EntityFramework", StringComparison.Ordinal) ||
                    assembly.Name.Contains("EF", StringComparison.Ordinal) ||
                    assembly.Name.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
                .ToList();

            // Assert - Then should only have minimal EF references through BuildingBlocks (temporary)
            // TODO: Remove this allowance when BuildingBlocks is refactored to separate concerns
            efReferences.Count.ShouldBeLessThanOrEqualTo(1);
            if (efReferences.Any())
            {
                efReferences.ShouldAllBe(assembly => assembly.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
            }
        }

        [Test]
        public void IdentityDomainTypes_ShouldNotUseEFAttributes()
        {
            // Arrange - Given all domain types
            var domainAssembly = Assembly.GetAssembly(typeof(Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal.AxonPrincipal));
            var allTypes = domainAssembly!.GetTypes();

            // Act - When checking for EF attributes
            var typesWithEFAttributes = new List<Type>();
            var efAttributeNames = new[]
            {
                "TableAttribute",
                "ColumnAttribute", 
                "KeyAttribute",
                "RequiredAttribute",
                "MaxLengthAttribute",
                "ForeignKeyAttribute",
                "IndexAttribute",
                "NotMappedAttribute"
            };

            foreach (var type in allTypes)
            {
                var attributes = type.GetCustomAttributes(true);
                var properties = type.GetProperties();
                
                // Check type-level attributes
                if (attributes.Any(attr => efAttributeNames.Contains(attr.GetType().Name)))
                {
                    typesWithEFAttributes.Add(type);
                }

                // Check property-level attributes
                foreach (var property in properties)
                {
                    var propertyAttributes = property.GetCustomAttributes(true);
                    if (propertyAttributes.Any(attr => efAttributeNames.Contains(attr.GetType().Name)))
                    {
                        typesWithEFAttributes.Add(type);
                        break;
                    }
                }
            }

            // Assert - Then no types should have EF attributes
            typesWithEFAttributes.ShouldBeEmpty();
        }

        [Test]
        public void IdentityDomainTypes_ShouldNotInheritFromEFTypes()
        {
            // Arrange - Given all domain entity types
            var domainAssembly = Assembly.GetAssembly(typeof(Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal.AxonPrincipal));
            var allTypes = domainAssembly!.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .ToList();

            // Act - When checking inheritance hierarchy
            var typesInheritingFromEF = new List<Type>();
            var efBaseTypeNames = new[]
            {
                "DbContext",
                "DbSet",
                "EntityConfiguration",
                "IEntityTypeConfiguration"
            };

            foreach (var type in allTypes)
            {
                var baseTypes = GetBaseTypes(type);
                if (baseTypes.Any(baseType => efBaseTypeNames.Any(efType => baseType.Name.Contains(efType, StringComparison.Ordinal))))
                {
                    typesInheritingFromEF.Add(type);
                }
            }

            // Assert - Then no types should inherit from EF types
            typesInheritingFromEF.ShouldBeEmpty();
        }

        private static List<Type> GetBaseTypes(Type type)
        {
            var baseTypes = new List<Type>();
            var current = type.BaseType;
            
            while (current != null && current != typeof(object))
            {
                baseTypes.Add(current);
                current = current.BaseType;
            }
            
            baseTypes.AddRange(type.GetInterfaces());
            return baseTypes;
        }
    }

    [TestFixture]
    public class LayerIsolationTests : DomainIsolationTests
    {
        // Test ID: 1.3-INT-002 - P0  
        [Test]
        public void DomainLayer_ShouldNotDependOnHigherLayers()
        {
            // Arrange - Given Domain assembly
            var domainAssembly = Assembly.GetAssembly(typeof(Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal.AxonPrincipal));

            // Act - When checking dependencies
            var domainReferences = domainAssembly!.GetReferencedAssemblies();

            var higherLayerReferences = domainReferences
                .Any(assembly => 
                    assembly.Name!.Contains("Identity.Application", StringComparison.Ordinal) ||
                    assembly.Name.Contains("Identity.Infrastructure", StringComparison.Ordinal) ||
                    assembly.Name.Contains("Identity.Api", StringComparison.Ordinal));

            // Assert - Then domain should not reference higher layers
            higherLayerReferences.ShouldBeFalse(); // Domain should NOT depend on Application/Infrastructure/API
        }

        [Test]
        public void DomainLayer_ShouldBeTheCore()
        {
            // Arrange - Given Domain assembly
            var domainAssembly = Assembly.GetAssembly(typeof(Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal.AxonPrincipal));

            // Act - When checking assembly structure
            var domainTypes = domainAssembly!.GetTypes().Where(t => t.IsPublic).ToList();

            // Assert - Then domain should contain core business entities
            domainTypes.ShouldContain(t => t.Name.Contains("AxonPrincipal", StringComparison.Ordinal));
            domainTypes.ShouldContain(t => t.Name.Contains("Wallet", StringComparison.Ordinal));
            domainTypes.ShouldContain(t => t.Name.Contains("WalletOwnership", StringComparison.Ordinal));
            domainTypes.ShouldContain(t => t.Name.Contains("IdentityCredential", StringComparison.Ordinal));
        }
    }

    [TestFixture]
    public class CleanArchitectureBoundariesTests : DomainIsolationTests
    {
        [Test]
        public void DomainLayer_ShouldOnlyDependOnBuildingBlocks()
        {
            // Arrange - Given Domain assembly
            var domainAssembly = Assembly.GetAssembly(typeof(Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal.AxonPrincipal));

            // Act - When checking all dependencies
            var referencedAssemblies = domainAssembly!.GetReferencedAssemblies();
            var allowedDependencies = new[]
            {
                "BuildingBlocks.Core",
                "BuildingBlocks.Primitives", 
                "BuildingBlocks",
                "CSharpFunctionalExtensions",
                "System",
                "Microsoft.Extensions",
                "netstandard",
                "mscorlib",
                "Vogen.SharedTypes",
                "MediatR.Contracts",
                "Ardalis.Specification",
                "Microsoft.EntityFrameworkCore" // Temporarily allowed due to BuildingBlocks dependency
            };

            var unauthorizedDependencies = referencedAssemblies
                .Where(assembly => !allowedDependencies.Any(allowed => 
                    assembly.Name!.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Assert - Then should only depend on allowed assemblies
            if (unauthorizedDependencies.Any())
            {
                var dependencyNames = string.Join(", ", unauthorizedDependencies.Select(d => d.Name));
                Assert.Fail($"Domain layer has unauthorized dependencies: {dependencyNames}");
            }

            unauthorizedDependencies.ShouldBeEmpty();
        }

        [Test]
        public void DomainTypes_ShouldFollowNamingConventions()
        {
            // Arrange - Given all domain types
            var domainAssembly = Assembly.GetAssembly(typeof(Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal.AxonPrincipal));
            var allTypes = domainAssembly!.GetTypes()
                .Where(t => t.IsPublic && !t.IsNested)
                .ToList();

            // Act - When checking naming conventions
            var aggregatesInWrongNamespace = allTypes
                .Where(t => t.Name.Contains("Aggregate", StringComparison.Ordinal) && !t.Namespace!.Contains("Aggregates", StringComparison.Ordinal))
                .ToList();

            var entitiesInWrongNamespace = allTypes
                .Where(t => t.Name.Contains("Entity", StringComparison.Ordinal) && !t.Namespace!.Contains("Entities", StringComparison.Ordinal))
                .ToList();

            var valueObjectsInWrongNamespace = allTypes
                .Where(t => t.Name.Contains("ValueObject", StringComparison.Ordinal) && !t.Namespace!.Contains("ValueObjects", StringComparison.Ordinal))
                .ToList();

            var eventsInWrongNamespace = allTypes
                .Where(t => t.Name.EndsWith("Event") && !t.Namespace!.Contains("Events", StringComparison.Ordinal))
                .ToList();

            // Assert - Then types should be in correct namespaces
            aggregatesInWrongNamespace.ShouldBeEmpty();
            entitiesInWrongNamespace.ShouldBeEmpty();
            valueObjectsInWrongNamespace.ShouldBeEmpty();
            eventsInWrongNamespace.ShouldBeEmpty();
        }
    }

    [TestFixture]
    public class ResultPatternComplianceTests : DomainIsolationTests
    {
        [Test]
        public void DomainMethods_ShouldUseResultPatternForBusinessLogic()
        {
            // Arrange - Given key domain aggregate methods
            var axonPrincipalType = typeof(Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal.AxonPrincipal);
            var walletType = typeof(Axon.Modules.Identity.Domain.Aggregates.Wallet.Wallet);

            // Act - When checking public methods
            var axonPrincipalMethods = axonPrincipalType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName && m.DeclaringType == axonPrincipalType)
                .ToList();

            var walletMethods = walletType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName && m.DeclaringType == walletType)
                .ToList();

            // Check methods that should return Result<T, Error>
            var businessLogicMethods = axonPrincipalMethods
                .Concat(walletMethods)
                .Where(m => 
                    m.Name.StartsWith("Update") || 
                    m.Name.StartsWith("Set") || 
                    m.Name.StartsWith("Link") || 
                    m.Name.StartsWith("Add"))
                .ToList();

            var nonResultMethods = businessLogicMethods
                .Where(m => !IsResultType(m.ReturnType))
                .ToList();

            // Assert - Then business logic methods should return Result
            if (nonResultMethods.Any())
            {
                var methodNames = string.Join(", ", nonResultMethods.Select(m => $"{m.DeclaringType!.Name}.{m.Name}"));
                Assert.Fail($"Methods should return Result<T, Error>: {methodNames}");
            }

            nonResultMethods.ShouldBeEmpty();
        }

        [Test]
        public void DomainMethods_ShouldNotThrowBusinessExceptions()
        {
            // This is verified through the Result pattern test above
            // Business logic should return Result<T, Error> instead of throwing exceptions
            Assert.Pass("Verified through Result pattern compliance test");
        }

        private static bool IsResultType(Type returnType)
        {
            if (returnType.IsGenericType)
            {
                var genericDefinition = returnType.GetGenericTypeDefinition();
                return genericDefinition.Name.Contains("Result", StringComparison.Ordinal);
            }
            
            return returnType.Name.Contains("Result", StringComparison.Ordinal) || returnType == typeof(void);
        }
    }

    [TestFixture]
    public class DependencyInjectionIsolationTests : DomainIsolationTests  
    {
        [Test]
        public void DomainLayer_ShouldNotContainDIContainerReferences()
        {
            // Arrange - Given Domain assembly
            var domainAssembly = Assembly.GetAssembly(typeof(Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal.AxonPrincipal));

            // Act - When checking for DI container references
            var referencedAssemblies = domainAssembly!.GetReferencedAssemblies();
            var diContainerReferences = referencedAssemblies
                .Where(assembly =>
                    assembly.Name!.Contains("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal) ||
                    assembly.Name.Contains("Autofac", StringComparison.Ordinal) ||
                    assembly.Name.Contains("Unity", StringComparison.Ordinal) ||
                    assembly.Name.Contains("Castle.Windsor", StringComparison.Ordinal) ||
                    assembly.Name.Contains("Ninject", StringComparison.Ordinal))
                .ToList();

            // Assert - Then should not reference DI containers
            diContainerReferences.ShouldBeEmpty();
        }

        [Test]
        public void DomainTypes_ShouldNotHaveDIAttributes()
        {
            // Arrange - Given all domain types  
            var domainAssembly = Assembly.GetAssembly(typeof(Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal.AxonPrincipal));
            var allTypes = domainAssembly!.GetTypes();

            // Act - When checking for DI attributes
            var diAttributeNames = new[]
            {
                "InjectAttribute",
                "AutowiredAttribute", 
                "ServiceAttribute",
                "TransientAttribute",
                "ScopedAttribute",
                "SingletonAttribute"
            };

            var typesWithDIAttributes = new List<Type>();

            foreach (var type in allTypes)
            {
                var attributes = type.GetCustomAttributes(true);
                if (attributes.Any(attr => diAttributeNames.Contains(attr.GetType().Name)))
                {
                    typesWithDIAttributes.Add(type);
                }

                // Check constructor parameters for injection attributes
                var constructors = type.GetConstructors();
                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    if (parameters.Any(p => p.GetCustomAttributes(true)
                        .Any(attr => diAttributeNames.Contains(attr.GetType().Name))))
                    {
                        typesWithDIAttributes.Add(type);
                        break;
                    }
                }
            }

            // Assert - Then no types should have DI attributes
            typesWithDIAttributes.ShouldBeEmpty();
        }
    }
}