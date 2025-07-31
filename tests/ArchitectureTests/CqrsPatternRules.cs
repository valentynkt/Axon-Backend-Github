using NetArchTest.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests;

/// <summary>
/// Architecture tests to enforce CQRS pattern compliance across the application.
/// Validates Commands, Queries, Handlers follow proper patterns and naming conventions.
/// </summary>
[TestFixture]
public class CqrsPatternRules
{
    [Test]
    public void Commands_ShouldEndWith_Command()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .HaveNameEndingWith("Command")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Command classes should end with 'Command'. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Test]
    public void Queries_ShouldEndWith_Query()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Queries.*")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .HaveNameEndingWith("Query")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Query classes should end with 'Query'. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Test]
    public void CommandHandlers_ShouldEndWith_Handler()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .DoNotHaveNameEndingWith("Command")
            .Should()
            .HaveNameEndingWith("Handler")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Command handlers should end with 'Handler'. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Test]
    public void QueryHandlers_ShouldEndWith_Handler()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Queries.*")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .DoNotHaveNameEndingWith("Query")
            .Should()
            .HaveNameEndingWith("Handler")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Query handlers should end with 'Handler'. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Test]
    public void Commands_ShouldImplement_IRequest()
    {
        var commandTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Command")
            .GetTypes();

        var violations = new List<string>();

        foreach (var commandType in commandTypes)
        {
            var implementsIRequest = commandType.GetInterfaces()
                .Any(i => i.Name == "IRequest" || 
                         (i.IsGenericType && i.GetGenericTypeDefinition().Name == "IRequest`1"));

            if (!implementsIRequest)
            {
                violations.Add(commandType.FullName ?? commandType.Name);
            }
        }

        violations.ShouldBeEmpty(
            $"Commands should implement IRequest or IRequest<T>. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void CommandHandlers_ShouldImplement_IRequestHandler()
    {
        var handlerTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Handler")
            .GetTypes();

        var violations = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            var implementsIRequestHandler = handlerType.GetInterfaces()
                .Any(i => i.Name.StartsWith("IRequestHandler"));

            if (!implementsIRequestHandler)
            {
                violations.Add(handlerType.FullName ?? handlerType.Name);
            }
        }

        violations.ShouldBeEmpty(
            $"Command handlers should implement IRequestHandler<TRequest> or IRequestHandler<TRequest, TResponse>. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void CommandHandlers_ShouldHave_HandleMethod()
    {
        var handlerTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Handler")
            .GetTypes();

        var violations = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            var hasHandleMethod = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(m => m.Name == "Handle");

            if (!hasHandleMethod)
            {
                violations.Add(handlerType.FullName ?? handlerType.Name);
            }
        }

        violations.ShouldBeEmpty(
            $"Command handlers should have a public Handle method. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void CommandHandlers_ShouldReturn_Task()
    {
        var handlerTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Handler")
            .GetTypes();

        var violations = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Handle");

            foreach (var method in handleMethods)
            {
                var returnsTask = method.ReturnType.Name.StartsWith("Task");
                
                if (!returnsTask)
                {
                    violations.Add($"{handlerType.FullName}.{method.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Command handler Handle methods should return Task or Task<T>. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void Commands_ShouldNotHave_PublicSetters()
    {
        var commandTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Command")
            .GetTypes();

        var violations = new List<string>();

        foreach (var commandType in commandTypes)
        {
            var publicSetters = commandType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true)
                .ToList();

            if (publicSetters.Any())
            {
                violations.Add($"{commandType.FullName}: {string.Join(", ", publicSetters.Select(p => p.Name))}");
            }
        }

        violations.ShouldBeEmpty(
            $"Commands should be immutable (no public setters). Use records or readonly properties. " +
            $"Violations: {string.Join("; ", violations)}");
    }

    [Test]
    public void CommandHandlers_ShouldHave_SingleResponsibility()
    {
        var handlerTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Handler")
            .GetTypes();

        var violations = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Handle")
                .Count();

            // Should handle only one command type
            if (handleMethods > 1)
            {
                violations.Add($"{handlerType.FullName} handles {handleMethods} commands");
            }
        }

        violations.ShouldBeEmpty(
            $"Command handlers should handle only one command type (Single Responsibility). " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void CommandHandlers_ShouldBeIn_ApplicationLayer()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Handler")
            .Should()
            .ResideInNamespace("*.Application.*")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Command handlers should be in Application layer. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Test]
    public void Commands_ShouldModify_State()
    {
        var handlerTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Handler")
            .GetTypes();

        var violations = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            if (!ArchitectureTestHelpers.LikelyModifiesState(handlerType))
            {
                violations.Add(handlerType.FullName ?? handlerType.Name);
            }
        }

        violations.ShouldBeEmpty(
            $"Command handlers should modify state (inject repositories, DbContext, etc.). " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void Queries_ShouldNever_ModifyState()
    {
        var handlerTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Queries.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Handler")
            .GetTypes();

        var violations = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            if (ArchitectureTestHelpers.LikelyModifiesState(handlerType))
            {
                violations.Add(handlerType.FullName ?? handlerType.Name);
            }
        }

        violations.ShouldBeEmpty(
            $"Query handlers should never modify state (avoid repositories, DbContext with write access). " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void Commands_ShouldHave_FluentValidationValidators()
    {
        var commandTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Command")
            .GetTypes();

        var violations = new List<string>();

        foreach (var commandType in commandTypes)
        {
            if (!ArchitectureTestHelpers.HasFluentValidator(commandType))
            {
                violations.Add(commandType.FullName ?? commandType.Name);
            }
        }

        violations.ShouldBeEmpty(
            $"Commands should have FluentValidation validators (TypeNameValidator pattern). " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void Validators_ShouldBeIn_ApplicationLayer()
    {
        var validatorTypes = Types.InCurrentDomain()
            .That()
            .HaveNameEndingWith("Validator")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => t.GetInterfaces().Any(i => i.Name.Contains("IValidator")) ||
                       t.BaseType?.Name.Contains("AbstractValidator") == true)
            .ToList();

        var violations = new List<string>();

        foreach (var validatorType in validatorTypes)
        {
            var namespaceName = validatorType.Namespace ?? "";
            if (!namespaceName.Contains(".Application."))
            {
                violations.Add(validatorType.FullName ?? validatorType.Name);
            }
        }

        violations.ShouldBeEmpty(
            $"FluentValidation validators should be in Application layer. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void Commands_ShouldReturn_ResultOrUnit()
    {
        var handlerTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Handler")
            .GetTypes();

        var violations = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Handle");

            foreach (var method in handleMethods)
            {
                var returnType = method.ReturnType;
                
                // Unwrap Task<T> to get T
                if (returnType.IsGenericType && returnType.Name.StartsWith("Task"))
                {
                    var taskArgs = returnType.GetGenericArguments();
                    if (taskArgs.Length == 1)
                    {
                        returnType = taskArgs[0];
                    }
                }

                var isValidReturn = returnType.Name == "Unit" ||
                                  returnType.Name.StartsWith("Result") ||
                                  (returnType.IsGenericType && returnType.GetGenericTypeDefinition().Name.StartsWith("Result"));

                if (!isValidReturn)
                {
                    violations.Add($"{handlerType.FullName}.{method.Name} returns {returnType.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Command handlers should return Result, Result<T>, or Unit. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void Queries_ShouldReturn_DTOsNotEntities()
    {
        var handlerTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Queries.*")
            .And()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Handler")
            .GetTypes();

        var violations = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Handle");

            foreach (var method in handleMethods)
            {
                var returnType = method.ReturnType;
                
                // Unwrap Task<T> to get T
                if (returnType.IsGenericType && returnType.Name.StartsWith("Task"))
                {
                    var taskArgs = returnType.GetGenericArguments();
                    if (taskArgs.Length == 1)
                    {
                        returnType = taskArgs[0];
                    }
                }

                if (!ArchitectureTestHelpers.IsDto(returnType))
                {
                    violations.Add($"{handlerType.FullName}.{method.Name} returns {returnType.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Query handlers should return DTOs, not domain entities (use *Dto, *Response, *Model, *View naming). " +
            $"Violations: {string.Join(", ", violations)}");
    }
}