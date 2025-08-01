using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Security;

/// <summary>
/// Rule to validate that proper security headers and middleware are configured.
/// </summary>
public class SecurityHeadersRule : ArchitectureRuleBase
{
    public override string RuleId => "SEC_HEADERS_001";
    public override string Name => "Security Headers Rule";
    public override string Description => "Ensures proper security headers are implemented";
    public override string Category => "Security";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(IArchitectureContext context, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();

        var assemblies = context.Assemblies;
        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => IsControllerOrEndpoint(t))
                .ToList();

            foreach (var type in types)
            {
                await CheckSecurityHeaders(type, violations);
            }
        }

        return violations;
    }

    private static bool IsControllerOrEndpoint(Type type)
    {
        return type.Name.EndsWith("Controller") ||
               type.Name.EndsWith("Endpoint") ||
               type.GetCustomAttributes().Any(attr =>
                   attr.GetType().Name.Contains("Controller") ||
                   attr.GetType().Name.Contains("ApiController") ||
                   attr.GetType().Name.Contains("Route"));
    }

    private async Task CheckSecurityHeaders(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => IsActionMethod(m))
            .ToList();

        foreach (var method in methods)
        {
            await CheckMethodSecurityHeaders(type, method, violations);
        }
    }

    private static bool IsActionMethod(MethodInfo method)
    {
        return method.GetCustomAttributes().Any(attr =>
            attr.GetType().Name.Contains("Http") ||
            attr.GetType().Name.Contains("Route") ||
            attr.GetType().Name.Contains("Get") ||
            attr.GetType().Name.Contains("Post") ||
            attr.GetType().Name.Contains("Put") ||
            attr.GetType().Name.Contains("Delete"));
    }

    private async Task CheckMethodSecurityHeaders(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        // Check for CORS configuration
        var hasCorsPolicy = method.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("EnableCors") ||
                        attr.GetType().Name.Contains("DisableCors"));

        if (!hasCorsPolicy && RequiresCorsPolicy(method))
        {
            violations.Add(CreateViolation(type, 
                $"Action method '{method.Name}' may require CORS policy configuration",
                "Add [EnableCors] attribute with appropriate policy or [DisableCors] if not needed"));
        }

        // Check for authentication requirements
        var hasAuthAttribute = method.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("Authorize") ||
                        attr.GetType().Name.Contains("AllowAnonymous"));

        if (!hasAuthAttribute)
        {
            violations.Add(CreateViolation(type,
                $"Action method '{method.Name}' should explicitly define authentication requirements",
                "Add [Authorize] or [AllowAnonymous] attribute to define authentication requirements"));
        }

        // Check for HTTPS requirement
        var hasHttpsAttribute = method.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("RequireHttps"));

        if (!hasHttpsAttribute && RequiresHttps(method))
        {
            violations.Add(CreateViolation(type,
                $"Action method '{method.Name}' should require HTTPS for security",
                "Add [RequireHttps] attribute for secure operations"));
        }

        // Check for anti-forgery token
        var hasAntiForgeryToken = method.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("ValidateAntiForgeryToken"));

        if (!hasAntiForgeryToken && RequiresAntiForgeryToken(method))
        {
            violations.Add(CreateViolation(type,
                $"Action method '{method.Name}' should validate anti-forgery tokens",
                "Add [ValidateAntiForgeryToken] attribute for state-changing operations"));
        }

        // Check for endpoints returning sensitive data
        if (ReturnsSensitiveData(method))
        {
            var hasCacheControl = method.GetCustomAttributes()
                .Any(attr => attr.GetType().Name.Contains("ResponseCache"));

            // Sensitive endpoints should not be cached
            if (hasCacheControl)
            {
                violations.Add(CreateViolation(type,
                    $"Endpoint '{method.Name}' returns sensitive data but allows caching",
                    "Remove [ResponseCache] attribute or set NoStore=true for sensitive data endpoints"));
            }
        }

        await Task.CompletedTask;
    }

    private static bool RequiresCorsPolicy(MethodInfo method)
    {
        return method.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("Http"));
    }

    private static bool RequiresHttps(MethodInfo method)
    {
        return method.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("Post") ||
                        attr.GetType().Name.Contains("Put") ||
                        attr.GetType().Name.Contains("Delete"));
    }

    private static bool RequiresAntiForgeryToken(MethodInfo method)
    {
        return method.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("Post") ||
                        attr.GetType().Name.Contains("Put") ||
                        attr.GetType().Name.Contains("Delete"));
    }

    private static bool ReturnsSensitiveData(MethodInfo method)
    {
        var returnType = method.ReturnType;
        var typeName = returnType.Name.ToLower();

        return typeName.Contains("user") ||
               typeName.Contains("password") ||
               typeName.Contains("token") ||
               typeName.Contains("key") ||
               typeName.Contains("secret") ||
               typeName.Contains("credential");
    }
}