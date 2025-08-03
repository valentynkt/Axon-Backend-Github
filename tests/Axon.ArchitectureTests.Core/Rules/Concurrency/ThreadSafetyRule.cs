using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Concurrency;

/// <summary>
/// Validates thread safety patterns and concurrent programming practices.
/// </summary>
public sealed class ThreadSafetyRule : ArchitectureRuleBase
{
    public override string RuleId => "CONC-THREAD-001";
    public override string Name => "Thread Safety";
    public override string Description => "Validates thread safety patterns and concurrent programming practices";
    public override string Category => "Concurrency";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {   
        var violations = new List<RuleViolation>();
        var types = context.Types.ToList();

        // Check for thread-safe singleton patterns
        await ValidateThreadSafeSingletons(types, violations);
        
        // Check for proper async/await usage
        await ValidateAsyncAwaitPatterns(types, violations);
        
        // Check for concurrent collection usage
        await ValidateConcurrentCollections(types, violations);
        
        // Check for lock-free programming patterns
        await ValidateLockFreePatterns(types, violations);
        
        return violations;
    }

    private static async Task ValidateThreadSafeSingletons(List<Type> types, List<RuleViolation> violations)
    {
        var singletonTypes = types.Where(t => 
            t.Name.Contains("Singleton") ||
            t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(m => m.Name == "Instance" || m.Name == "GetInstance"))
            .ToList();

        foreach (var singletonType in singletonTypes)
        {
            var instanceMethods = singletonType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Instance" || m.Name == "GetInstance")
                .ToList();

            foreach (var method in instanceMethods)
            {
                // Check for thread-safe implementation indicators
                var hasLockingMechanism = method.GetCustomAttributes()
                    .Any(a => a.GetType().Name.Contains("MethodImpl") ||
                            a.GetType().Name.Contains("Synchronized"));

                // Check for lazy initialization
                var usesLazyInitialization = method.ReturnType.Name.Contains("Lazy<") ||
                                            singletonType.GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                                                .Any(f => f.FieldType.Name.Contains("Lazy<"));

                if (!hasLockingMechanism && !usesLazyInitialization)
                {
                    violations.Add(new RuleViolation
                    {
                        Message = $"Singleton method '{method.Name}' in '{singletonType.Name}' may not be thread-safe",
                        TypeName = singletonType.FullName!,
                        AssemblyName = singletonType.Assembly.GetName().Name ?? "Unknown",
                        Severity = RuleSeverity.Error
                    });
                }
            }

            // Check for mutable static fields
            var staticFields = singletonType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => !f.IsInitOnly && !f.IsLiteral)
                .ToList();

            foreach (var field in staticFields)
            {
                if (!field.Name.ToLower().Contains("lock") && !field.Name.ToLower().Contains("sync"))
                {
                    violations.Add(new RuleViolation
                    {
                        Message = $"Mutable static field '{field.Name}' in singleton '{singletonType.Name}' may cause thread safety issues",
                        TypeName = singletonType.FullName!,
                        AssemblyName = singletonType.Assembly.GetName().Name ?? "Unknown",
                        Severity = RuleSeverity.Error
                    });
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateAsyncAwaitPatterns(List<Type> types, List<RuleViolation> violations)
    {
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            
            foreach (var method in methods)
            {
                var returnType = method.ReturnType;
                var isAsyncMethod = returnType.Name.StartsWith("Task") || returnType.Name.StartsWith("ValueTask");
                
                if (isAsyncMethod)
                {
                    // Check for proper async suffix
                    if (!method.Name.EndsWith("Async"))
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Async method '{method.Name}' in '{type.Name}' should have 'Async' suffix",
                            Severity = RuleSeverity.Error
                        });
                    }

                    // Check for ConfigureAwait usage
                    var hasConfigureAwaitGuidance = method.GetCustomAttributes()
                        .Any(a => a.GetType().Name.Contains("ConfigureAwait"));

                    // For library code, ConfigureAwait(false) should be used
                    if (type.Namespace?.Contains("Infrastructure") == true && !hasConfigureAwaitGuidance)
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Infrastructure async method '{method.Name}' in '{type.Name}' should use ConfigureAwait(false)",
                            Severity = RuleSeverity.Error
                        });
                    }
                }

                // Check for async void methods (should be avoided except for event handlers)
                if (returnType == typeof(void) && method.GetCustomAttributes().Any(a => a.GetType().Name.Contains("Async")))
                {
                    var isEventHandler = method.GetParameters().Length == 2 &&
                                       method.GetParameters()[0].ParameterType.Name.Contains("object") &&
                                       method.GetParameters()[1].ParameterType.Name.Contains("EventArgs");

                    if (!isEventHandler)
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Method '{method.Name}' in '{type.Name}' uses async void, should return Task instead",
                            Severity = RuleSeverity.Error
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateConcurrentCollections(List<Type> types, List<RuleViolation> violations)
    {
        foreach (var type in types)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var properties = type.GetProperties();

            // Check fields for thread-unsafe collections
            foreach (var field in fields)
            {
                if (IsThreadUnsafeCollection(field.FieldType) && IsSharedState(field))
                {
                    var hasSynchronization = HasSynchronizationMechanism(type);
                    
                    if (!hasSynchronization)
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Field '{field.Name}' in '{type.Name}' uses thread-unsafe collection '{field.FieldType.Name}' in shared state",
                            Severity = RuleSeverity.Error
                        });
                    }
                }
            }

            // Check properties for thread-unsafe collections
            foreach (var property in properties)
            {
                if (IsThreadUnsafeCollection(property.PropertyType) && property.CanWrite)
                {
                    var hasSynchronization = HasSynchronizationMechanism(type);
                    
                    if (!hasSynchronization)
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Property '{property.Name}' in '{type.Name}' uses thread-unsafe collection '{property.PropertyType.Name}'",
                            Severity = RuleSeverity.Error
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateLockFreePatterns(List<Type> types, List<RuleViolation> violations)
    {
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            
            foreach (var method in methods)
            {
                // Check for lock statements in high-performance contexts
                if (type.Name.Contains("Performance") || type.Name.Contains("HighThroughput"))
                {
                    // This is a simplified check - in practice, would analyze method body
                    var methodName = method.Name.ToLower();
                    
                    if (methodName.Contains("lock") || methodName.Contains("synchronize"))
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"High-performance method '{method.Name}' in '{type.Name}' may use blocking synchronization",
                            Severity = RuleSeverity.Error
                        });
                    }
                }

                // Check for atomic operations usage
                var parameters = method.GetParameters();
                var hasAtomicOperations = parameters.Any(p => 
                    p.ParameterType.Name.Contains("Interlocked") ||
                    p.ParameterType.Name.Contains("Volatile"));

                if (hasAtomicOperations)
                {
                    var hasProperDocumentation = method.GetCustomAttributes()
                        .Any(a => a.GetType().Name.Contains("Documentation") ||
                                a.GetType().Name.Contains("Summary"));

                    if (!hasProperDocumentation)
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Method '{method.Name}' in '{type.Name}' uses atomic operations but lacks proper documentation",
                            Severity = RuleSeverity.Error
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static bool IsThreadUnsafeCollection(Type type)
    {
        var unsafeCollections = new[] 
        {
            "List`1", "Dictionary`2", "HashSet`1", "Stack`1", "Queue`1", "ArrayList", "Hashtable"
        };

        return unsafeCollections.Any(unsafeCollection => type.Name.Contains(unsafeCollection)) ||
               (type.IsGenericType && unsafeCollections.Any(unsafeCollection => type.GetGenericTypeDefinition().Name.Contains(unsafeCollection)));
    }

    private static bool IsSharedState(FieldInfo field)
    {
        return field.IsStatic || field.IsPublic || 
               (field.IsPrivate && field.DeclaringType?.GetMethods().Any(m => m.IsPublic && m.Name.Contains(field.Name)) == true);
    }

    private static bool HasSynchronizationMechanism(Type type)
    {
        var lockFields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(f => f.Name.ToLower().Contains("lock") || f.Name.ToLower().Contains("sync"));

        var synchronizedMethods = type.GetMethods()
            .Any(m => m.GetCustomAttributes().Any(a => a.GetType().Name.Contains("MethodImpl") ||
                                                       a.GetType().Name.Contains("Synchronized")));

        return lockFields || synchronizedMethods;
    }
}