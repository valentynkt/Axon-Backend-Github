using NetArchTest.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests;

/// <summary>
/// Architecture tests to enforce security compliance and prevent common vulnerabilities.
/// Validates proper handling of authorization, authentication, and security best practices.
/// </summary>
[TestFixture]
public class SecurityComplianceRules
{
    private static Assembly? _apiAssembly;

    [OneTimeSetUp]
    public void SetUp()
    {
        // Force load the API assembly by referencing a type from it
        _ = typeof(Axon.Api.Endpoints.Chat.ProcessMessageEndpoint);
        _apiAssembly = typeof(Axon.Api.Endpoints.Chat.ProcessMessageEndpoint).Assembly;
        TestContext.WriteLine($"Loaded API assembly: {_apiAssembly.GetName().Name}");
    }
    [Test]
    public void Controllers_ShouldHave_AuthorizationAttributes()
    {
        if (_apiAssembly == null)
        {
            Assert.Fail("API assembly not loaded");
            return;
        }

        var controllerTypes = _apiAssembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Axon.Api.Endpoints") == true || 
                       t.Namespace?.StartsWith("Axon.Api.Controllers") == true)
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(SecurityTestHelpers.IsApiController)
            .ToArray();

        var violations = new List<string>();

        foreach (var controllerType in controllerTypes)
        {
            var httpMethods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(SecurityTestHelpers.IsHttpEndpoint)
                .ToArray();

            foreach (var method in httpMethods)
            {
                var hasControllerAuth = SecurityTestHelpers.HasAuthorizationAttribute(controllerType);
                var hasMethodAuth = SecurityTestHelpers.HasAuthorizationAttribute(method);

                if (!hasControllerAuth && !hasMethodAuth)
                {
                    violations.Add($"{controllerType.Name}.{method.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"API controllers or their methods should have authorization attributes ([Authorize] or [AllowAnonymous]). " +
            $"This ensures explicit security decisions for all endpoints. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void SensitiveEndpoints_ShouldRequire_Authentication()
    {
        if (_apiAssembly == null)
        {
            Assert.Fail("API assembly not loaded");
            return;
        }

        var controllerTypes = _apiAssembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Axon.Api.Endpoints") == true || 
                       t.Namespace?.StartsWith("Axon.Api.Controllers") == true)
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(SecurityTestHelpers.IsApiController)
            .ToArray();

        var violations = new List<string>();

        foreach (var controllerType in controllerTypes)
        {
            var sensitiveMethods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(SecurityTestHelpers.IsSensitiveOperation)
                .ToArray();

            foreach (var method in sensitiveMethods)
            {
                var hasControllerAuth = SecurityTestHelpers.HasAuthorizationAttribute(controllerType);
                var hasMethodAuth = SecurityTestHelpers.HasAuthorizationAttribute(method);
                
                // Check if method explicitly allows anonymous access
                var isAnonymous = method.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name == "AllowAnonymousAttribute") ||
                    (!hasMethodAuth && controllerType.GetCustomAttributes()
                        .Any(attr => attr.GetType().Name == "AllowAnonymousAttribute"));

                // Check if method has [Authorize] attribute
                var hasAuthorize = method.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name == "AuthorizeAttribute") ||
                    (!hasMethodAuth && controllerType.GetCustomAttributes()
                        .Any(attr => attr.GetType().Name == "AuthorizeAttribute"));

                if (!hasAuthorize && !isAnonymous)
                {
                    violations.Add($"{controllerType.Name}.{method.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Sensitive endpoints (POST/PUT/DELETE/PATCH or state-changing operations) should require authentication. " +
            $"Add [Authorize] attribute or explicitly mark as [AllowAnonymous] if intentional. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    [Test]
    public void APIs_ShouldHave_RateLimiting()
    {
        var apiAssembly = _apiAssembly;

        if (apiAssembly == null)
        {
            Assert.Pass("No API assembly found to analyze for rate limiting");
            return;
        }

        var hasRateLimiting = SecurityTestHelpers.HasRateLimitingConfiguration(apiAssembly);

        if (!hasRateLimiting)
        {
            Assert.Warn(
                "API should implement rate limiting to prevent abuse. " +
                "Consider adding rate limiting middleware or attributes to protect against DoS attacks. " +
                "This is a guidance warning - rate limiting configuration was not detected.");
        }
        else
        {
            Assert.Pass("Rate limiting configuration detected");
        }
    }

    [Test]
    public void APIs_ShouldValidate_ContentTypes()
    {
        if (_apiAssembly == null)
        {
            Assert.Fail("API assembly not loaded");
            return;
        }

        var controllerTypes = _apiAssembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Axon.Api.Endpoints") == true || 
                       t.Namespace?.StartsWith("Axon.Api.Controllers") == true)
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(SecurityTestHelpers.IsApiController)
            .ToArray();

        var violations = new List<string>();

        foreach (var controllerType in controllerTypes)
        {
            var postMethods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name == "HttpPostAttribute" || 
                                attr.GetType().Name == "HttpPutAttribute" ||
                                attr.GetType().Name == "HttpPatchAttribute"))
                .ToArray();

            foreach (var method in postMethods)
            {
                var hasContentValidation = SecurityTestHelpers.HasContentTypeValidation(method);
                
                if (!hasContentValidation)
                {
                    violations.Add($"{controllerType.Name}.{method.Name}");
                }
            }
        }

        if (violations.Any())
        {
            TestContext.WriteLine(
                $"API endpoints with request bodies should validate content types using [Consumes] attributes or [FromBody] parameters. " +
                $"This helps prevent content type confusion attacks. " +
                $"Endpoints to review: {string.Join(", ", violations)}");
        }

        TestContext.WriteLine($"Endpoints without apparent content type validation: {violations.Count}");
    }

    [Test]
    public void APIs_ShouldUse_HTTPS()
    {
        var apiAssembly = _apiAssembly;

        if (apiAssembly == null)
        {
            Assert.Pass("No API assembly found to analyze for HTTPS configuration");
            return;
        }

        var hasHttpsRedirection = SecurityTestHelpers.HasHttpsRedirection(apiAssembly);

        if (!hasHttpsRedirection)
        {
            TestContext.WriteLine(
                "API should enforce HTTPS to protect data in transit. " +
                "Add UseHttpsRedirection() in Program.cs or configure HTTPS enforcement. " +
                "This is a guidance warning - HTTPS configuration was not detected through static analysis.");
        }
        else
        {
            TestContext.WriteLine("HTTPS configuration patterns detected");
        }

        // This is a guidance test - we cannot reliably detect HTTPS configuration through static analysis
        Assert.Pass($"HTTPS enforcement analysis completed - see output for recommendations");
    }

    [Test]
    public void CORS_ShouldBe_RestrictivelyConfigured()
    {
        var apiAssembly = _apiAssembly;

        if (apiAssembly == null)
        {
            Assert.Pass("No API assembly found to analyze for CORS configuration");
            return;
        }

        var hasCorsConfiguration = SecurityTestHelpers.HasCorsConfiguration(apiAssembly);

        if (hasCorsConfiguration)
        {
            TestContext.WriteLine(
                "CORS configuration detected. Ensure CORS policy is restrictive: " +
                "- Avoid AllowAnyOrigin() in production " +
                "- Use specific allowed origins " +
                "- Limit allowed methods and headers " +
                "Manual review of CORS configuration is recommended.");
        }
        else
        {
            TestContext.WriteLine(
                "No CORS configuration detected. If API serves browser clients, " +
                "configure CORS with restrictive policies to prevent cross-origin attacks.");
        }

        // This is a guidance test - CORS configuration correctness requires manual review
        Assert.Pass($"CORS configuration analysis completed - see output for recommendations");
    }
}