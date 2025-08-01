using NUnit.Framework;
using Shouldly;
using Axon.ArchitectureTests.Framework.Configuration;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Engine;
using Axon.ArchitectureTests.Framework.Utilities;
using Axon.ArchitectureTests.Core.Rules.Enhanced;
using Axon.Tests.Shared.TestBase;

namespace Axon.ArchitectureTests.Core.Tests.Enhanced;

/// <summary>
/// API contract validation tests with endpoint consistency checks.
/// Ensures API endpoints follow consistent patterns and contract standards.
/// </summary>
[TestFixture]
public sealed class ApiContractValidationTests : ArchitectureTestBase
{
    [Test]
    public async Task ApiEndpoints_ShouldFollowRestfulConventions()
    {
        // Arrange
        var rule = new RestfulEndpointRule();
        ValidateRuleConfiguration(rule);
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API001");
        
        // Assert
        AssertNoSpecificViolations(ruleResult, 
            new[] { "non-restful", "inconsistent-naming", "poor-resource-design" }, 
            "Critical RESTful Convention Violations");
    }

    [Test]
    public async Task RequestResponseModels_ShouldBeConsistent()
    {
        // Arrange
        var rule = new RequestResponseConsistencyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API002");
        
        // Assert
        var inconsistencyViolations = FilterViolations(ruleResult, 
            "inconsistent-naming", "model-mismatch", "contract-break");
        
        LogViolations(inconsistencyViolations, "Request/Response Model Inconsistencies");
        
        AssertNoSpecificViolations(ruleResult, 
            new[] { "breaking-change", "contract-violation" }, 
            "Critical Contract Consistency Issues");
    }

    [Test]
    public async Task ApiVersioning_ShouldBeProperlyImplemented()
    {
        // Arrange
        var rule = new ApiVersioningRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API003");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "versioning", "backward-compatibility", "deprecation" }, 
            "API Versioning Patterns");
        
        var versioningViolations = FilterViolations(ruleResult, 
            "no-versioning", "inconsistent-versioning", "breaking-change");
        
        LogViolations(versioningViolations, "API Versioning Issues");
    }

    [Test]
    public async Task InputValidation_ShouldBeComprehensive()
    {
        // Arrange
        var rule = new ApiInputValidationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API004");
        
        // Assert
        var validationGaps = FilterViolations(ruleResult, 
            "missing-validation", "weak-validation", "unprotected-input");
        
        AssertNoSpecificViolations(ruleResult, 
            new[] { "critical-unvalidated", "security-risk" }, 
            "Critical Input Validation Gaps");
        
        LogViolations(validationGaps, "Input Validation Opportunities");
    }

    [Test]
    public async Task ErrorHandling_ShouldBeStandardized()
    {
        // Arrange
        var rule = new StandardizedErrorHandlingRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API005");
        
        // Assert
        var errorHandlingViolations = FilterViolations(ruleResult, 
            "inconsistent-errors", "poor-error-format", "missing-error-codes");
        
        LogViolations(errorHandlingViolations, "Error Handling Standardization Issues");
        
        // Error responses should follow RFC 7807 Problem Details format
        LogInformationalViolations(ruleResult, 
            new[] { "problem-details", "error-standardization", "client-friendly" }, 
            "Error Handling Best Practices");
    }

    [Test]
    public async Task ResponseFormats_ShouldBeConsistent()
    {
        // Arrange
        var rule = new ResponseFormatConsistencyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API006");
        
        // Assert
        var formatViolations = FilterViolations(ruleResult, 
            "inconsistent-format", "mixed-casing", "format-mismatch");
        
        LogViolations(formatViolations, "Response Format Consistency Issues");
        
        // JSON responses should follow consistent naming conventions
        AssertNoSpecificViolations(ruleResult, 
            new[] { "critical-format-break", "client-breaking" }, 
            "Critical Response Format Issues");
    }

    [Test]
    public async Task HttpStatusCodes_ShouldBeSemanticallCorrect()
    {
        // Arrange
        var rule = new HttpStatusCodeRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API007");
        
        // Assert
        var statusCodeViolations = FilterViolations(ruleResult, 
            "wrong-status-code", "misused-status", "inappropriate-response");
        
        LogViolations(statusCodeViolations, "HTTP Status Code Issues");
        
        // Critical operations should use appropriate status codes
        AssertNoSpecificViolations(ruleResult, 
            new[] { "wrong-error-status", "misleading-status" }, 
            "Critical Status Code Misuse");
    }

    [Test]
    public async Task ContentNegotiation_ShouldBeSupported()
    {
        // Arrange
        var rule = new ContentNegotiationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API008");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "content-negotiation", "media-types", "accept-headers" }, 
            "Content Negotiation Support");
    }

    [Test]
    public async Task ApiDocumentation_ShouldBeComplete()
    {
        // Arrange
        var rule = new ApiDocumentationRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API009");
        
        // Assert
        var documentationGaps = FilterViolations(ruleResult, 
            "missing-documentation", "incomplete-swagger", "undocumented-endpoint");
        
        LogViolations(documentationGaps, "API Documentation Gaps");
        
        // Public endpoints should be documented
        LogInformationalViolations(ruleResult, 
            new[] { "swagger", "openapi", "documentation" }, 
            "API Documentation Quality");
    }

    [Test]
    public async Task RateLimiting_ShouldBeImplemented()
    {
        // Arrange
        var rule = new RateLimitingRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API010");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "rate-limiting", "throttling", "dos-protection" }, 
            "Rate Limiting Implementation");
        
        var rateLimitingGaps = FilterViolations(ruleResult, 
            "no-rate-limiting", "unprotected-endpoint", "dos-vulnerable");
        
        if (rateLimitingGaps.Any())
        {
            LogViolations(rateLimitingGaps, "Rate Limiting Gaps");
        }
    }

    [Test]
    public async Task CorsConfiguration_ShouldBeSecure()
    {
        // Arrange
        var rule = new CorsSecurityRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API011");
        
        // Assert
        var corsViolations = FilterViolations(ruleResult, 
            "permissive-cors", "wildcard-origins", "insecure-cors");
        
        AssertNoSpecificViolations(ruleResult, 
            new[] { "critical-cors", "security-risk" }, 
            "Critical CORS Security Issues");
        
        LogViolations(corsViolations, "CORS Configuration Issues");
    }

    [Test]
    public async Task ApiSecurity_ShouldBeRobust()
    {
        // Arrange
        var rule = new ApiSecurityRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API012");
        
        // Assert
        AssertRuleSuccess(ruleResult, "API Security Implementation");
        
        var securityViolations = FilterViolations(ruleResult, 
            "missing-auth", "weak-security", "unprotected-endpoint");
        
        securityViolations.Count.ShouldBe(0, 
            "All API endpoints should have appropriate security measures");
    }

    [Test]
    public async Task AllApiContractRules_ShouldPass()
    {
        // Arrange
        var rules = new IArchitectureRule[]
        {
            new RestfulEndpointRule(),
            new RequestResponseConsistencyRule(),
            new ApiVersioningRule(),
            new ApiInputValidationRule(),
            new StandardizedErrorHandlingRule(),
            new ResponseFormatConsistencyRule(),
            new HttpStatusCodeRule(),
            new ContentNegotiationRule(),
            new ApiDocumentationRule(),
            new RateLimitingRule(),
            new CorsSecurityRule(),
            new ApiSecurityRule()
        };
        
        // Act
        var result = await ExecuteRulesAndValidateAsync(rules, "API Contract Validation");
        
        // Assert
        ValidateFrameworkContext();
        AssertExecutionPerformance(result, TimeSpan.FromSeconds(30));
        
        // Critical API rules must pass
        var criticalFailures = result.RuleResults
            .Where(r => !r.IsSuccess && 
                       (r.RuleId.EndsWith("001") || r.RuleId.EndsWith("002") || 
                        r.RuleId.EndsWith("004") || r.RuleId.EndsWith("012")) && 
                       r.RuleId.StartsWith("API"))
            .ToList();

        criticalFailures.Count.ShouldBe(0, 
            $"Critical API contract rules failed: {string.Join(", ", criticalFailures.Select(f => f.RuleId))}");

        // Generate API contract health report
        await GenerateApiContractHealthReport(result);
    }

    [Test]
    public async Task PaginationPatterns_ShouldBeConsistent()
    {
        // Arrange
        var rule = new PaginationConsistencyRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API013");
        
        // Assert
        var paginationViolations = FilterViolations(ruleResult, 
            "inconsistent-pagination", "missing-pagination", "poor-paging");
        
        LogViolations(paginationViolations, "Pagination Pattern Issues");
        
        LogInformationalViolations(ruleResult, 
            new[] { "pagination", "page-size", "total-count" }, 
            "Pagination Best Practices");
    }

    [Test]
    public async Task FilteringAndSorting_ShouldFollowConventions()
    {
        // Arrange
        var rule = new FilteringSortingRule();
        
        // Act
        var ruleResult = await ExecuteRuleAndValidateAsync(rule, "API014");
        
        // Assert
        LogInformationalViolations(ruleResult, 
            new[] { "filtering", "sorting", "query-parameters" }, 
            "Filtering and Sorting Patterns");
    }

    private async Task GenerateApiContractHealthReport(EngineResult result)
    {
        await TestContext.Out.WriteLineAsync("\n=== API CONTRACT HEALTH REPORT ===");
        
        var apiRules = result.RuleResults.Where(r => r.RuleId.StartsWith("API")).ToList();
        var passedRules = apiRules.Count(r => r.IsSuccess);
        var totalRules = apiRules.Count;
        
        await TestContext.Out.WriteLineAsync($"API Contract Compliance Score: {passedRules}/{totalRules} ({(passedRules * 100.0 / totalRules):F1}%)");
        
        // RESTful Design health
        var restfulRules = apiRules.Where(r => 
            r.RuleId.EndsWith("001") || r.RuleId.EndsWith("002") || r.RuleId.EndsWith("006"));
        var restfulHealth = restfulRules.Count(r => r.IsSuccess) * 100.0 / restfulRules.Count();
        
        await TestContext.Out.WriteLineAsync($"RESTful Design Health: {restfulHealth:F1}%");
        
        // Security health
        var securityRules = apiRules.Where(r => 
            r.RuleId.EndsWith("004") || r.RuleId.EndsWith("011") || r.RuleId.EndsWith("012"));
        var securityHealth = securityRules.Count(r => r.IsSuccess) * 100.0 / securityRules.Count();
        
        await TestContext.Out.WriteLineAsync($"API Security Health: {securityHealth:F1}%");
        
        // Documentation and Standards health
        var standardsRules = apiRules.Where(r => 
            r.RuleId.EndsWith("003") || r.RuleId.EndsWith("005") || r.RuleId.EndsWith("009"));
        var standardsHealth = standardsRules.Count(r => r.IsSuccess) * 100.0 / standardsRules.Count();
        
        await TestContext.Out.WriteLineAsync($"Standards & Documentation Health: {standardsHealth:F1}%");
        
        await TestContext.Out.WriteLineAsync("====================================\n");
    }
}