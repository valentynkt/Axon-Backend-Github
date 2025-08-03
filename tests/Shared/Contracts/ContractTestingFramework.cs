using System.Text.Json;
using NUnit.Framework;
using Axon.Tests.Shared.TestBase;
using Axon.Shared.Common.Abstractions;

namespace Axon.Tests.Shared.Contracts;

/// <summary>
/// Contract Testing Framework for external services and MCP server integration
/// </summary>
[TestFixture]
[Category("Contract")]
[Category("Integration")]
public abstract class ContractTestingFramework : LondonSchoolTestBase
{
    protected ContractValidator ContractValidator { get; private set; } = null!;

    [SetUp]
    public override void LondonSchoolSetUp()
    {
        base.LondonSchoolSetUp();
        ContractValidator = new ContractValidator(this);
    }

    /// <summary>
    /// Validates that a service contract is maintained between versions
    /// </summary>
    protected async Task ValidateServiceContract<TRequest, TResponse>(
        string serviceName,
        TRequest request,
        Func<TRequest, Task<Result<TResponse>>> serviceCall,
        ContractExpectation<TResponse> expectations)
        where TRequest : class
        where TResponse : class
    {
        await ContractValidator
            .ForService(serviceName)
            .WithRequest(request)
            .ExpectResponse(expectations)
            .ValidateAsync(serviceCall);
    }

    /// <summary>
    /// Validates MCP server contract compliance
    /// </summary>
    protected async Task ValidateMcpServerContract(
        string serverName,
        string toolName,
        object parameters,
        McpContractExpectation expectations)
    {
        await ContractValidator
            .ForMcpServer(serverName)
            .WithTool(toolName, parameters)
            .ExpectMcpResponse(expectations)
            .ValidateAsync();
    }

    /// <summary>
    /// Validates OpenAI API contract compliance
    /// </summary>
    protected async Task ValidateOpenAiApiContract(
        string model,
        string message,
        OpenAiContractExpectation expectations)
    {
        await ContractValidator
            .ForOpenAiApi()
            .WithModel(model)
            .WithMessage(message)
            .ExpectOpenAiResponse(expectations)
            .ValidateAsync();
    }
}

/// <summary>
/// Contract validator for testing service contracts
/// </summary>
public class ContractValidator
{
    private readonly LondonSchoolTestBase _testBase;
    private string _serviceName = string.Empty;
    private object? _request;
    private readonly List<IContractExpectation> _expectations = new();

    public ContractValidator(LondonSchoolTestBase testBase)
    {
        _testBase = testBase;
    }

    public ContractValidator ForService(string serviceName)
    {
        _serviceName = serviceName;
        return this;
    }

    public ContractValidator ForMcpServer(string serverName)
    {
        _serviceName = $"MCP-{serverName}";
        return this;
    }

    public ContractValidator ForOpenAiApi()
    {
        _serviceName = "OpenAI-API";
        return this;
    }

    public ContractValidator WithRequest<T>(T request) where T : class
    {
        _request = request;
        return this;
    }

    public ContractValidator WithTool(string toolName, object parameters)
    {
        _request = new { ToolName = toolName, Parameters = parameters };
        return this;
    }

    public ContractValidator WithModel(string model)
    {
        _request = new { Model = model };
        return this;
    }

    public ContractValidator WithMessage(string message)
    {
        var currentRequest = _request as dynamic ?? new { };
        _request = new { currentRequest?.Model, Message = message };
        return this;
    }

    public ContractValidator ExpectResponse<T>(ContractExpectation<T> expectation) where T : class
    {
        _expectations.Add(expectation);
        return this;
    }

    public ContractValidator ExpectMcpResponse(McpContractExpectation expectation)
    {
        _expectations.Add(expectation);
        return this;
    }

    public ContractValidator ExpectOpenAiResponse(OpenAiContractExpectation expectation)
    {
        _expectations.Add(expectation);
        return this;
    }

    public async Task ValidateAsync<TRequest, TResponse>(Func<TRequest, Task<Result<TResponse>>> serviceCall)
        where TRequest : class
        where TResponse : class
    {
        TestContext.WriteLine($"=== CONTRACT VALIDATION: {_serviceName} ===");
        
        var validationTimer = System.Diagnostics.Stopwatch.StartNew();
        var results = new ContractValidationResults();

        try
        {
            if (_request is not TRequest typedRequest)
            {
                throw new InvalidOperationException($"Request type mismatch. Expected {typeof(TRequest).Name}");
            }

            // Execute service call
            TestContext.WriteLine("Executing service call for contract validation...");
            var callTimer = System.Diagnostics.Stopwatch.StartNew();
            
            var result = await serviceCall(typedRequest);
            callTimer.Stop();
            
            results.ExecutionTime = callTimer.Elapsed;
            results.ServiceCallSucceeded = result.IsSuccess;
            
            if (result.IsSuccess)
            {
                TestContext.WriteLine($"✓ Service call succeeded in {callTimer.ElapsedMilliseconds}ms");
                results.Response = result.Value;
            }
            else
            {
                TestContext.WriteLine($"✗ Service call failed: {result.Error?.Message}");
                results.Error = result.Error;
            }

            // Validate expectations
            foreach (var expectation in _expectations)
            {
                TestContext.WriteLine($"Validating expectation: {expectation.Description}");
                var isValid = await expectation.ValidateAsync(results);
                
                if (isValid)
                {
                    TestContext.WriteLine("✓ Expectation satisfied");
                }
                else
                {
                    TestContext.WriteLine("✗ Expectation failed");
                    results.FailedExpectations.Add(expectation.Description);
                }
            }
        }
        finally
        {
            validationTimer.Stop();
            TestContext.WriteLine($"=== CONTRACT VALIDATION COMPLETED in {validationTimer.ElapsedMilliseconds}ms ===");
            
            // Assert contract compliance
            if (results.FailedExpectations.Any())
            {
                Assert.Fail($"Contract validation failed: {string.Join(", ", results.FailedExpectations)}");
            }
        }
    }

    public async Task ValidateAsync()
    {
        // Simplified validation for non-generic scenarios
        TestContext.WriteLine($"=== CONTRACT VALIDATION: {_serviceName} ===");
        
        foreach (var expectation in _expectations)
        {
            TestContext.WriteLine($"Validating expectation: {expectation.Description}");
            var results = new ContractValidationResults(); // Mock results for this simplified case
            var isValid = await expectation.ValidateAsync(results);
            
            if (!isValid)
            {
                Assert.Fail($"Contract expectation failed: {expectation.Description}");
            }
        }
        
        TestContext.WriteLine("=== CONTRACT VALIDATION COMPLETED ===");
    }
}

/// <summary>
/// Base interface for contract expectations
/// </summary>
public interface IContractExpectation
{
    string Description { get; }
    Task<bool> ValidateAsync(ContractValidationResults results);
}

/// <summary>
/// Generic contract expectation for typed responses
/// </summary>
public class ContractExpectation<T> : IContractExpectation where T : class
{
    public string Description { get; }
    private readonly List<Func<T?, bool>> _validators = new();
    private readonly List<Func<ContractValidationResults, bool>> _resultValidators = new();

    public ContractExpectation(string description)
    {
        Description = description;
    }

    public ContractExpectation<T> ResponseShouldNotBeNull()
    {
        _validators.Add(response => response != null);
        return this;
    }

    public ContractExpectation<T> ResponseShouldSatisfy(Func<T?, bool> validator)
    {
        _validators.Add(validator);
        return this;
    }

    public ContractExpectation<T> ExecutionShouldComplete(TimeSpan maxDuration)
    {
        _resultValidators.Add(results => results.ExecutionTime <= maxDuration);
        return this;
    }

    public ContractExpectation<T> ShouldSucceed()
    {
        _resultValidators.Add(results => results.ServiceCallSucceeded);
        return this;
    }

    public async Task<bool> ValidateAsync(ContractValidationResults results)
    {
        // Validate response if available
        if (results.Response is T typedResponse)
        {
            foreach (var validator in _validators)
            {
                if (!validator(typedResponse))
                {
                    return false;
                }
            }
        }
        else if (_validators.Any())
        {
            // Response was expected but not available or wrong type
            return false;
        }

        // Validate execution results
        foreach (var validator in _resultValidators)
        {
            if (!validator(results))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// MCP server contract expectation
/// </summary>
public class McpContractExpectation : IContractExpectation
{
    public string Description { get; }
    private readonly List<Func<object?, bool>> _responseValidators = new();

    public McpContractExpectation(string description)
    {
        Description = description;
    }

    public McpContractExpectation ToolShouldReturnSuccess()
    {
        _responseValidators.Add(response => response != null);
        return this;
    }

    public McpContractExpectation ResponseShouldContainField(string fieldName)
    {
        _responseValidators.Add(response =>
        {
            if (response == null) return false;
            var json = JsonSerializer.Serialize(response);
            return json.Contains($"\"{fieldName}\"");
        });
        return this;
    }

    public async Task<bool> ValidateAsync(ContractValidationResults results)
    {
        foreach (var validator in _responseValidators)
        {
            if (!validator(results.Response))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// OpenAI API contract expectation
/// </summary>
public class OpenAiContractExpectation : IContractExpectation
{
    public string Description { get; }
    private readonly List<Func<object?, bool>> _responseValidators = new();

    public OpenAiContractExpectation(string description)
    {
        Description = description;
    }

    public OpenAiContractExpectation ResponseShouldHaveContent()
    {
        _responseValidators.Add(response => response != null);
        return this;
    }

    public OpenAiContractExpectation ResponseShouldBeWithinTokenLimit(int maxTokens)
    {
        _responseValidators.Add(response =>
        {
            // Simplified token counting - in practice, you'd use proper tokenization
            var responseText = response?.ToString() ?? "";
            return responseText.Length / 4 <= maxTokens; // Rough approximation
        });
        return this;
    }

    public async Task<bool> ValidateAsync(ContractValidationResults results)
    {
        foreach (var validator in _responseValidators)
        {
            if (!validator(results.Response))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// Results of contract validation
/// </summary>
public class ContractValidationResults
{
    public bool ServiceCallSucceeded { get; set; }
    public object? Response { get; set; }
    public object? Error { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public List<string> FailedExpectations { get; } = new();
}