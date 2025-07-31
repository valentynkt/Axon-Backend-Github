using System.Reflection;
using Axon.ArchitectureTests.Framework.Contracts;

namespace Axon.ArchitectureTests.Framework.Rules;

/// <summary>
/// Base class for rules that validate architectural pattern compliance.
/// </summary>
public abstract class PatternComplianceRule : ArchitectureRuleBase
{
    /// <inheritdoc />
    public override string Category => "PatternCompliance";

    /// <summary>
    /// Checks if a type follows a specific naming pattern.
    /// </summary>
    protected bool FollowsNamingPattern(Type type, string pattern) =>
        type.Name.EndsWith(pattern, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if a type implements a specific interface.
    /// </summary>
    protected bool ImplementsInterface(Type type, Type interfaceType) =>
        interfaceType.IsAssignableFrom(type);

    /// <summary>
    /// Checks if a type implements a generic interface.
    /// </summary>
    protected bool ImplementsGenericInterface(Type type, Type genericInterfaceDefinition) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterfaceDefinition);

    /// <summary>
    /// Gets all methods of a type with specific attributes.
    /// </summary>
    protected IEnumerable<MethodInfo> GetMethodsWithAttribute<TAttribute>(Type type) 
        where TAttribute : Attribute =>
        type.GetMethods().Where(m => m.GetCustomAttribute<TAttribute>() != null);

    /// <summary>
    /// Checks if a type is in a specific namespace pattern.
    /// </summary>
    protected bool IsInNamespace(Type type, string namespacePattern) =>
        type.Namespace?.Contains(namespacePattern, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Checks if a type is abstract.
    /// </summary>
    protected bool IsAbstract(Type type) => type.IsAbstract;

    /// <summary>
    /// Checks if a type is sealed.
    /// </summary>
    protected bool IsSealed(Type type) => type.IsSealed;

    /// <summary>
    /// Gets all public properties of a type.
    /// </summary>
    protected IEnumerable<PropertyInfo> GetPublicProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

    /// <summary>
    /// Gets all constructors of a type.
    /// </summary>
    protected IEnumerable<ConstructorInfo> GetConstructors(Type type) =>
        type.GetConstructors();

    /// <summary>
    /// Checks if a method is async (returns Task or Task&lt;T&gt;).
    /// </summary>
    protected bool IsAsyncMethod(MethodInfo method) =>
        method.ReturnType == typeof(Task) || 
        (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
}