using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Api;

/// <summary>
/// Validates REST API design patterns and best practices.
/// </summary>
public sealed class RestApiDesignRule : ArchitectureRuleBase
{
    public override string RuleId => "API-REST-001";
    public override string Name => "REST API Design";
    public override string Description => "Validates REST API design patterns, HTTP methods, status codes, and resource naming";
    public override string Category => "API Design";
    public override RuleSeverity Severity => RuleSeverity.Warning;

    private static readonly Dictionary<string, string[]> HttpMethodPatterns = new()
    {
        { "GET", new[] { "Get", "Find", "List", "Retrieve", "Read" } },
        { "POST", new[] { "Create", "Add", "Insert", "Process" } },
        { "PUT", new[] { "Update", "Replace", "Modify" } },
        { "PATCH", new[] { "Patch", "PartialUpdate" } },
        { "DELETE", new[] { "Delete", "Remove", "Destroy" } }
    };

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {   
        var violations = new List<RuleViolation>();
        var types = context.Types.ToList();

        // Check for proper HTTP method usage
        await ValidateHttpMethodUsage(types, violations);
        
        // Check for consistent resource naming
        await ValidateResourceNaming(types, violations);
        
        // Check for proper status code usage
        await ValidateStatusCodeUsage(types, violations);
        
        // Check for API versioning
        await ValidateApiVersioning(types, violations);
        
        // Check for proper content negotiation
        await ValidateContentNegotiation(types, violations);
        
        return violations;
    }

    private async Task ValidateHttpMethodUsage(List<Type> types, List<RuleViolation> violations)
    {
        var controllerTypes = types.Where(t => 
            t.Name.EndsWith("Controller") ||
            t.Name.EndsWith("Endpoint") ||
            t.GetCustomAttributes().Any(a => a.GetType().Name.Contains("Controller")))
            .ToList();

        foreach (var controller in controllerTypes)
        {
            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == controller)
                .ToList();

            foreach (var method in methods)
            {
                var httpAttributes = method.GetCustomAttributes()
                    .Where(a => a.GetType().Name.StartsWith("Http"))
                    .ToList();

                foreach (var httpAttr in httpAttributes)
                {
                    var httpMethod = httpAttr.GetType().Name.Replace("Http", "").Replace("Attribute", "");
                    
                    if (HttpMethodPatterns.ContainsKey(httpMethod.ToUpper()))
                    {
                        var expectedPatterns = HttpMethodPatterns[httpMethod.ToUpper()];
                        var methodNameMatches = expectedPatterns.Any(pattern => 
                            method.Name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase));

                        if (!methodNameMatches)
                        {
                            violations.Add(new RuleViolation(
                                $"Method '{method.Name}' uses HTTP {httpMethod} but doesn't follow naming convention (expected: {string.Join(", ", expectedPatterns)})",
                                controller.FullName!,
                                method.Name));
                        }
                    }
                }

                // Check for methods without HTTP attributes that should have them
                if (!httpAttributes.Any() && method.IsPublic && !method.IsSpecialName)
                {
                    violations.Add(new RuleViolation(
                        $"Public method '{method.Name}' in controller '{controller.Name}' lacks HTTP method attribute",
                        controller.FullName!,
                        method.Name));
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private async Task ValidateResourceNaming(List<Type> types, List<RuleViolation> violations)
    {
        var controllerTypes = types.Where(t => 
            t.Name.EndsWith("Controller") ||
            t.Name.EndsWith("Endpoint"))
            .ToList();

        foreach (var controller in controllerTypes)
        {
            var routeAttributes = controller.GetCustomAttributes()
                .Where(a => a.GetType().Name.Contains("Route"))
                .ToList();

            foreach (var routeAttr in routeAttributes)
            {
                // Check route attribute for proper resource naming
                var routeAttributeType = routeAttr.GetType();
                var templateProperty = routeAttributeType.GetProperty("Template");
                
                if (templateProperty != null)
                {
                    var template = templateProperty.GetValue(routeAttr)?.ToString();
                    
                    if (!string.IsNullOrEmpty(template))
                    {
                        // Check for plural resource names in routes
                        var segments = template.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        
                        foreach (var segment in segments)
                        {
                            if (segment.StartsWith("{") && segment.EndsWith("}"))
                                continue; // Skip parameter segments
                            
                            if (segment.Contains("api") || segment.Contains("v"))
                                continue; // Skip API version segments
                            
                            // Simple check for singular resource names (should be plural)
                            if (!segment.EndsWith("s") && !segment.Contains("-"))
                            {
                                violations.Add(new RuleViolation(
                                    $"Route segment '{segment}' in controller '{controller.Name}' should use plural resource naming",
                                    controller.FullName!,
                                    "Route"));
                            }
                        }
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private async Task ValidateStatusCodeUsage(List<Type> types, List<RuleViolation> violations)
    {
        var controllerTypes = types.Where(t => 
            t.Name.EndsWith("Controller") ||
            t.Name.EndsWith("Endpoint"))
            .ToList();

        foreach (var controller in controllerTypes)
        {
            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == controller)
                .ToList();

            foreach (var method in methods)
            {
                var returnType = method.ReturnType;
                
                // Check if method returns proper API response types
                var returnsActionResult = returnType.Name.Contains("ActionResult") ||
                                        returnType.Name.Contains("IActionResult") ||
                                        returnType.Name.Contains("Result");

                if (!returnsActionResult && method.GetCustomAttributes().Any(a => a.GetType().Name.StartsWith("Http")))
                {
                    violations.Add(new RuleViolation(
                        $"API method '{method.Name}' in '{controller.Name}' should return ActionResult or Result<T> for proper status code handling",
                        controller.FullName!,
                        method.Name));
                }

                // Check for explicit status code attributes
                var hasStatusCodeAttributes = method.GetCustomAttributes()
                    .Any(a => a.GetType().Name.Contains("ProducesResponseType") ||
                            a.GetType().Name.Contains("SwaggerResponse"));

                if (!hasStatusCodeAttributes && method.GetCustomAttributes().Any(a => a.GetType().Name.StartsWith("Http")))
                {
                    violations.Add(new RuleViolation(
                        $"API method '{method.Name}' in '{controller.Name}' lacks explicit status code documentation",
                        controller.FullName!,
                        method.Name));
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private async Task ValidateApiVersioning(List<Type> types, List<RuleViolation> violations)
    {
        var controllerTypes = types.Where(t => 
            t.Name.EndsWith("Controller") ||
            t.Name.EndsWith("Endpoint"))
            .ToList();

        foreach (var controller in controllerTypes)
        {
            var hasVersionAttribute = controller.GetCustomAttributes()
                .Any(a => a.GetType().Name.Contains("ApiVersion") ||
                        a.GetType().Name.Contains("Version"));

            var hasRouteVersioning = controller.GetCustomAttributes()
                .Any(a => a.GetType().Name.Contains("Route") && 
                        a.ToString()?.Contains("v") == true);

            if (!hasVersionAttribute && !hasRouteVersioning)
            {
                violations.Add(new RuleViolation(
                    $"Controller '{controller.Name}' lacks API versioning strategy",
                    controller.FullName!,
                    "Class"));
            }
        }
        
        await Task.CompletedTask;
    }

    private async Task ValidateContentNegotiation(List<Type> types, List<RuleViolation> violations)
    {
        var controllerTypes = types.Where(t => 
            t.Name.EndsWith("Controller") ||
            t.Name.EndsWith("Endpoint"))
            .ToList();

        foreach (var controller in controllerTypes)
        {
            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == controller)
                .ToList();

            foreach (var method in methods)
            {
                var hasProducesAttribute = method.GetCustomAttributes()
                    .Any(a => a.GetType().Name.Contains("Produces"));

                var hasConsumesAttribute = method.GetCustomAttributes()
                    .Any(a => a.GetType().Name.Contains("Consumes"));

                var httpAttributes = method.GetCustomAttributes()
                    .Where(a => a.GetType().Name.StartsWith("Http"))
                    .ToList();

                if (httpAttributes.Any() && !hasProducesAttribute)
                {
                    violations.Add(new RuleViolation(
                        $"API method '{method.Name}' in '{controller.Name}' lacks Produces attribute for content negotiation",
                        controller.FullName!,
                        method.Name));
                }

                // Check for POST/PUT methods without Consumes attribute
                var hasPostOrPut = httpAttributes.Any(a => 
                    a.GetType().Name.Contains("Post") || 
                    a.GetType().Name.Contains("Put") ||
                    a.GetType().Name.Contains("Patch"));

                if (hasPostOrPut && !hasConsumesAttribute)
                {
                    violations.Add(new RuleViolation(
                        $"API method '{method.Name}' in '{controller.Name}' lacks Consumes attribute for content negotiation",
                        controller.FullName!,
                        method.Name));
                }
            }
        }
        
        await Task.CompletedTask;
    }
}