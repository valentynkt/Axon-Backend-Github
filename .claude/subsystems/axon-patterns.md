# 🎯 Axon Backend Patterns - Specialized Development Templates

**SUBSYSTEM PURPOSE**: Comprehensive development patterns, templates, and automation specifically tailored for Axon Backend's Clean Architecture, DDD, and CQRS implementation.

## 🏗️ CLEAN ARCHITECTURE AUTOMATION PATTERNS

### 🎯 Vertical Slice Feature Generation (Complete Automation)
```javascript
// Generate complete vertical slice with all layers using MCP tools
const generateVerticalSlice = async (module, feature, operation) => {
  console.log(`🏗️ Generating Vertical Slice: ${module}.${feature} ${operation}`);
  
  // Initialize Claude Flow for Axon Backend development via MCP
  await mcp__claude-flow__memory_usage({
    action: "store",
    key: "axon/current-slice",
    value: JSON.stringify({ module, feature, operation }),
    namespace: "axon-backend"
  });
  
  // Phase 1: Architecture Analysis (Claude Flow orchestrated via MCP)
  await mcp__claude-flow__task_orchestrate({
    task: `Analyze Clean Architecture requirements for ${module}.${feature}`,
    strategy: "adaptive",
    maxAgents: 2  // Auto-spawns architect agents
  });
  
  // Phase 2: Generate all layer files (Parallel execution via MCP)
  await mcp__claude-flow__parallel_execute({
    tasks: [
      {
        name: "api-layer",
        description: "Generate API endpoint and contracts",
        files: [
          `src/Api/Endpoints/${module}/${feature}Endpoint.cs`,
          `src/Api/Contracts/${module}/${feature}Requests.cs`,
          `src/Api/Contracts/${module}/${feature}Responses.cs`
        ],
        template: "axon-api-template",
        validation: "no-domain-types-on-wire"
      },
    "application-layer": {
      "description": "Generate CQRS command/query with handler",
      "files": [
        "src/Modules/'$module'/Application/'$operation's/'$feature'/'$feature'.cs",
        "src/Modules/'$module'/Application/'$operation's/'$feature'/'$feature'Handler.cs",
        "src/Modules/'$module'/Application/'$operation's/'$feature'/'$feature'Validator.cs"
      ],
      "template": "axon-cqrs-template",
      "validation": "mediatr-patterns,result-usage"
    },
    "domain-layer": {
      "description": "Generate domain entities and value objects if needed",
      "files": [
        "src/Modules/'$module'/Domain/Aggregates/'$feature'/...cs",
        "src/Modules/'$module'/Domain/ValueObjects/...cs"
      ],
      "template": "axon-domain-template",
      "validation": "ddd-patterns,domain-isolation",
      "conditional": "only-if-domain-logic-required"
    },
    "infrastructure-layer": {
      "description": "Generate infrastructure adapters",
      "files": [
        "src/Modules/'$module'/Infrastructure/.../*Client.cs"
      ],
      "template": "axon-infrastructure-template",
      "validation": "port-adapter-pattern"
    }
  }'
  
  # Phase 3: Generate comprehensive tests (Parallel execution)
  npx claude-flow@alpha parallel-execute --tasks '{
    "unit-tests": {
      "description": "Generate behavioral unit tests",
      "files": [
        "tests/Modules.'$module'.Application.Tests/'$operation's/'$feature'/'$feature'HandlerTests.cs",
        "tests/Modules.'$module'.Domain.Tests/Aggregates/'$feature'/...Tests.cs"
      ],
      "template": "axon-test-template",
      "framework": "nunit-shouldly-moq",
      "coverage_target": 95
    },
    "integration-tests": {
      "description": "Generate integration tests",
      "files": [
        "tests/Api.Tests/Endpoints/'$module'/'$feature'EndpointTests.cs"
      ],
      "template": "axon-integration-template",
      "framework": "webapplicationfactory"
    },
    "architecture-tests": {
      "description": "Update architecture tests for new slice",
      "files": [
        "tests/Axon.ArchitectureTests.Core/Tests/*Tests.cs"
      ],
      "validation": "clean-architecture-boundaries"
    }
  }'
  
  # Phase 4: Validation and Learning
  npx claude-flow@alpha quality-assess "$module.$feature" \
    --criteria "clean-architecture,ddd-patterns,cqrs-implementation,test-coverage" \
    --auto-fix-violations
  
  # Phase 5: Store patterns for learning
  npx claude-flow@alpha neural-train "axon-vertical-slice-patterns" \
    --training-data "{\"module\":\"$module\",\"feature\":\"$feature\",\"success\":true}" \
    --continuous
  
  echo "✅ Vertical Slice Generated Successfully"
}
```

### 🔧 CQRS Pattern Automation Templates

#### Command Pattern Template
```csharp
// Auto-generated Command template with Claude Flow integration
namespace Axon.Modules.{MODULE}.Application.Commands.{FEATURE};

// Command (Request)
public sealed record {FEATURE}Command(
    {COMMAND_PROPERTIES}
) : IRequest<Result<{FEATURE}Response>>;

// Command Handler with Claude Flow hooks
public sealed class {FEATURE}Handler : IRequestHandler<{FEATURE}Command, Result<{FEATURE}Response>>
{
    private readonly {REQUIRED_DEPENDENCIES} _dependencies;
    private readonly ILogger<{FEATURE}Handler> _logger;

    public {FEATURE}Handler({DEPENDENCY_INJECTION})
    {
        {DEPENDENCY_ASSIGNMENTS}
    }

    public async Task<Result<{FEATURE}Response>> Handle(
        {FEATURE}Command command, 
        CancellationToken cancellationToken)
    {
        // Claude Flow pre-execution hook
        await LogInformation("Processing {FEATURE} command", command);
        
        try 
        {
            // Core business logic implementation
            {BUSINESS_LOGIC_IMPLEMENTATION}
            
            // Claude Flow post-execution learning
            await LogInformation("Successfully processed {FEATURE} command");
            
            return Result<{FEATURE}Response>.Success(response);
        }
        catch (Exception ex)
        {
            await LogError(ex, "Failed to process {FEATURE} command");
            return Result<{FEATURE}Response>.Failure(Error.InternalError("Processing failed"));
        }
    }
}

// Response DTO
public sealed record {FEATURE}Response(
    {RESPONSE_PROPERTIES}
);
```

#### Query Pattern Template
```csharp
// Auto-generated Query template with Claude Flow integration
namespace Axon.Modules.{MODULE}.Application.Queries.{FEATURE};

public sealed record {FEATURE}Query(
    {QUERY_PARAMETERS}
) : IRequest<Result<{FEATURE}QueryResponse>>;

public sealed class {FEATURE}QueryHandler : IRequestHandler<{FEATURE}Query, Result<{FEATURE}QueryResponse>>
{
    private readonly {READ_DEPENDENCIES} _dependencies;
    private readonly ILogger<{FEATURE}QueryHandler> _logger;

    public {FEATURE}QueryHandler({DEPENDENCY_INJECTION})
    {
        {DEPENDENCY_ASSIGNMENTS}
    }

    public async Task<Result<{FEATURE}QueryResponse>> Handle(
        {FEATURE}Query query, 
        CancellationToken cancellationToken)
    {
        await LogInformation("Processing {FEATURE} query", query);
        
        // Query implementation with caching and optimization
        {QUERY_IMPLEMENTATION}
        
        return Result<{FEATURE}QueryResponse>.Success(response);
    }
}
```

## 🏛️ DOMAIN-DRIVEN DESIGN AUTOMATION

### 🎯 Aggregate Root Generation
```bash
// Generate complete DDD aggregate with all components using MCP
const generateDddAggregate = async (module, aggregate) => {
  // Claude Flow orchestrated aggregate generation via MCP
  await mcp__claude-flow__task_orchestrate({
    task: `Generate DDD aggregate ${module}.${aggregate} with full DDD patterns`,
    strategy: "adaptive",
    maxAgents: 2  // Auto-spawns DDD-focused agents
  });
  
  // Generate aggregate structure via MCP
  await mcp__claude-flow__parallel_execute({
    tasks: [
      {
        name: "aggregate-root",
        file: `src/Modules/${module}/Domain/Aggregates/${aggregate}/${aggregate}.cs`,
        template: "ddd-aggregate-root",
        patterns: ["entity-base", "domain-events", "invariants"]
      },
    "value-objects": {
      "files": [
        "src/Modules/'$module'/Domain/ValueObjects/'$aggregate'Id.cs",
        "src/Modules/'$module'/Domain/ValueObjects/...cs"
      ],
      "template": "ddd-value-object",
      "patterns": ["immutability", "equality", "validation"]
    },
    "domain-events": {
      "files": [
        "src/Modules/'$module'/Domain/Events/'$aggregate'Created.cs",
        "src/Modules/'$module'/Domain/Events/'$aggregate'Updated.cs"
      ],
      "template": "ddd-domain-event",
      "patterns": ["event-sourcing-ready"]
    },
    "domain-services": {
      "files": [
        "src/Modules/'$module'/Domain/Services/'$aggregate'DomainService.cs"
      ],
      "template": "ddd-domain-service",
      "conditional": "complex-business-logic-required"
    }
  }'
}
```

### 📋 DDD Aggregate Template
```csharp
// Auto-generated DDD Aggregate Root template
namespace Axon.Modules.{MODULE}.Domain.Aggregates.{AGGREGATE};

public sealed class {AGGREGATE} : AggregateRoot<{AGGREGATE}Id>
{
private readonly List<{DOMAIN_EVENT}> _domainEvents = new();

    private {AGGREGATE}({AGGREGATE}Id id, {CONSTRUCTOR_PARAMETERS}) : base(id)
{
        {INVARIANT_VALIDATIONS}
        {PROPERTY_ASSIGNMENTS}
    }

    // Factory method with business rules
    public static Result<{AGGREGATE}> Create({CREATION_PARAMETERS})
    {
        // Business rule validation
        var validationResult = ValidateCreationRules({VALIDATION_PARAMETERS});
        if (validationResult.IsFailure)
            return Result<{AGGREGATE}>.Failure(validationResult.Error);

        var id = {AGGREGATE}Id.CreateNew();
        var aggregate = new {AGGREGATE}(id, {CONSTRUCTOR_ARGUMENTS});
        
        // Raise domain event
        aggregate.RaiseDomainEvent(new {AGGREGATE}Created(id, {EVENT_DATA}));
        
        return Result<{AGGREGATE}>.Success(aggregate);
    }

    // Business operations
    public Result {BUSINESS_OPERATION}({OPERATION_PARAMETERS})
    {
        // Business rule validation
        {BUSINESS_RULE_VALIDATIONS}
        
        // Execute business logic
        {BUSINESS_LOGIC_IMPLEMENTATION}
        
        // Raise domain event
        RaiseDomainEvent(new {AGGREGATE}{OPERATION}({EVENT_PARAMETERS}));
        
        return Result.Success();
    }

    // Invariant protection
    private static Result ValidateCreationRules({VALIDATION_PARAMETERS})
    {
        {BUSINESS_RULE_IMPLEMENTATIONS}
        return Result.Success();
    }

    // Properties (encapsulated)
    {AGGREGATE_PROPERTIES}
}

// Value Object for strongly-typed ID
public sealed class {AGGREGATE}Id : ValueObject
{
    public Guid Value { get; }
    
    private {AGGREGATE}Id(Guid value) => Value = value;
    
    public static {AGGREGATE}Id CreateNew() => new(Guid.NewGuid());
    public static {AGGREGATE}Id From(Guid value) => new(value);
    
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
    
    public override string ToString() => Value.ToString();
}
```

## 🚀 FASTENDPOINTS INTEGRATION PATTERNS

### 🎯 Endpoint Generation with Claude Flow
```bash
// Generate FastEndpoints with full validation and mapping using MCP
const generateFastEndpoint = async (module, feature, httpMethod) => {
  await mcp__claude-flow__parallel_execute({
    tasks: [
      {
        name: "endpoint",
        file: `src/Api/Endpoints/${module}/${feature}Endpoint.cs`,
        template: "fastendpoints-template",
        method: httpMethod,
        validation: "fluent-validation",
        mapping: "auto-mapper"
      },
    "contracts": {
      "files": [
        "src/Api/Contracts/'$module'/'$feature'Request.cs",
        "src/Api/Contracts/'$module'/'$feature'Response.cs"
      ],
      "template": "api-contracts-template",
      "validation": "no-domain-leakage"
    }
  }'
}
```

### 📡 FastEndpoints Template
```csharp
// Auto-generated FastEndpoints template
namespace Axon.Api.Endpoints.{MODULE};

public sealed class {FEATURE}Endpoint : Endpoint<{FEATURE}Request, {FEATURE}Response>
{
    private readonly ISender _sender;
    private readonly ILogger<{FEATURE}Endpoint> _logger;

    public {FEATURE}Endpoint(ISender sender, ILogger<{FEATURE}Endpoint> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public override void Configure()
    {
        {HTTP_METHOD}("/api/v1/{MODULE_LOWER}/{FEATURE_LOWER}");
        AllowAnonymous(); // or Roles("Admin") or Policies("RequireAuth")
        Version(1);
        Summary(s => {
            s.Summary = "{FEATURE_DESCRIPTION}";
            s.Description = "{DETAILED_DESCRIPTION}";
            s.Responses[200] = "{SUCCESS_DESCRIPTION}";
            s.Responses[400] = "Validation errors";
            s.Responses[500] = "Internal server error";
        });
    }

    public override async Task HandleAsync({FEATURE}Request request, CancellationToken ct)
    {
        await _logger.LogInformationAsync("Processing {FEATURE} request", request);

        // Map to command/query
        var command = new {FEATURE}Command({MAPPING_PARAMETERS});
        
        // Execute through MediatR
        var result = await _sender.Send(command, ct);
        
        if (result.IsFailure)
        {
            await _logger.LogErrorAsync("Failed to process {FEATURE}: {Error}", result.Error);
            await SendResultAsync(Results.BadRequest(result.Error));
            return;
        }

        // Map to response
        var response = new {FEATURE}Response({RESPONSE_MAPPING});
        
        await SendOkAsync(response);
    }
}

// Request DTO with validation
public sealed class {FEATURE}Request
{
    {REQUEST_PROPERTIES}
}

public sealed class {FEATURE}RequestValidator : Validator<{FEATURE}Request>
{
    public {FEATURE}RequestValidator()
    {
        {VALIDATION_RULES}
    }
}

// Response DTO
public sealed class {FEATURE}Response
{
    {RESPONSE_PROPERTIES}
}
```

## 🧪 TESTING AUTOMATION PATTERNS

### 🎯 Comprehensive Test Generation
```bash
// Generate complete test suite for a feature using MCP
const generateTestSuite = async (module, feature) => {
  await mcp__claude-flow__parallel_execute({
    tasks: [
      {
        name: "unit-tests",
        description: "Generate behavioral unit tests",
        files: [
          `tests/Modules.${module}.Application.Tests/Commands/${feature}/${feature}HandlerTests.cs`
        ],
        template: "axon-unit-test-template",
        patterns: ["arrange-act-assert", "behavioral-naming", "shouldly-assertions"]
      },
    "integration-tests": {
      "description": "Generate integration tests",
      "files": [
        "tests/Api.Tests/Endpoints/'$module'/'$feature'EndpointTests.cs"
      ],
      "template": "axon-integration-test-template",
      "patterns": ["webapi-testing", "database-isolation"]
    },
    "test-builders": {
      "description": "Generate test data builders",
      "files": [
        "tests/Modules.'$module'.Application.Tests/Builders/'$feature'CommandBuilder.cs"
      ],
      "template": "test-builder-template",
      "patterns": ["fluent-builder", "test-data-generation"]
    }
  }'
}
```

### 🧪 Unit Test Template (NUnit + Shouldly + Moq)
```csharp
// Auto-generated Unit Test template
namespace Axon.Modules.{MODULE}.Application.Tests.Commands.{FEATURE};

[TestFixture]
[Category("Unit")]
[Category("Application")]
public sealed class {FEATURE}HandlerTests : ApplicationTestBase
{
    private {FEATURE}Handler _handler = null!;
    private Mock<{DEPENDENCIES}> _dependencyMocks = null!;

    [SetUp]
    public void SetUp()
    {
        // Setup mocks
        {MOCK_SETUPS}
        
        // Create handler
        _handler = new {FEATURE}Handler({MOCK_DEPENDENCIES});
    }

    [Test]
    public async Task Handle_Given{SCENARIO}_Should{EXPECTED_OUTCOME}()
    {
        // Arrange
        var command = {FEATURE}CommandBuilder
            .{BUILDER_METHODS}
            .Build();
        
        {MOCK_CONFIGURATIONS}

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccessAnd(response =>
        {
            {RESPONSE_ASSERTIONS}
        });

        // Verify interactions
        {MOCK_VERIFICATIONS}
    }

    [Test]
    public async Task Handle_Given{FAILURE_SCENARIO}_ShouldReturnFailure()
    {
        // Arrange
        var command = {FEATURE}CommandBuilder
            .{INVALID_BUILDER_METHODS}
            .Build();

        {FAILURE_MOCK_SETUP}

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Error.ShouldBe({EXPECTED_ERROR});
    }

    {ADDITIONAL_TEST_METHODS}
}

// Test Data Builder
public sealed class {FEATURE}CommandBuilder
{
    private {BUILDER_PROPERTIES}

    private {FEATURE}CommandBuilder() { }

    public static {FEATURE}CommandBuilder {FACTORY_METHOD}({REQUIRED_PARAMETERS})
    {
        return new {FEATURE}CommandBuilder
        {
            {INITIAL_PROPERTY_ASSIGNMENTS}
        };
    }

    public {FEATURE}CommandBuilder {FLUENT_METHODS}({PARAMETER_TYPE} value)
    {
        {PROPERTY_ASSIGNMENT}
        return this;
    }

    public {FEATURE}Command Build()
    {
        return new {FEATURE}Command({BUILD_PARAMETERS});
    }
}
```

## 🔧 INFRASTRUCTURE AUTOMATION PATTERNS

### 🎯 OpenAI Client Integration Template
```csharp
// Auto-generated OpenAI client with MCP integration
namespace Axon.Modules.{MODULE}.Infrastructure.Ai;

public sealed class OpenAiClient : IAiClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiClient> _logger;
    private readonly IMcpServerResolver _mcpResolver;

    public OpenAiClient(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiClient> logger,
        IMcpServerResolver mcpResolver)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _mcpResolver = mcpResolver;
    }

    public async Task<Result<AiResponse>> ProcessMessageAsync(
        AiRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Claude Flow pre-execution hook
            await _logger.LogInformationAsync("Processing AI request with message length: {Length}", 
                request.Message.Length);

            // Get MCP server configurations
            var mcpResult = await _mcpResolver.GetEnabledServerConfigurations();
            if (mcpResult.IsFailure)
            {
                await _logger.LogErrorAsync("Failed to load MCP configurations: {Error}", mcpResult.Error);
                return Result<AiResponse>.Failure(mcpResult.Error);
            }

            // Build OpenAI request with MCP tools
            var openAiRequest = BuildOpenAiRequest(request, mcpResult.Value);
            
            // Execute request
            var response = await ExecuteOpenAiRequest(openAiRequest, cancellationToken);
            
            // Map response
            var aiResponse = MapToAiResponse(response);
            
            // Claude Flow post-execution learning
            await _logger.LogInformationAsync("Successfully processed AI request. Response ID: {ResponseId}", 
                aiResponse.ResponseId);

            return Result<AiResponse>.Success(aiResponse);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync(ex, "Failed to process AI request");
            return Result<AiResponse>.Failure(Error.ExternalService("AI service unavailable"));
        }
    }

    private ChatCompletionRequest BuildOpenAiRequest(AiRequest request, IReadOnlyCollection<McpServerConfig> mcpConfigs)
    {
        {BUILD_REQUEST_IMPLEMENTATION}
    }

    private async Task<ChatCompletionResponse> ExecuteOpenAiRequest(
        ChatCompletionRequest request, 
        CancellationToken cancellationToken)
    {
        {EXECUTE_REQUEST_IMPLEMENTATION}
    }

    private AiResponse MapToAiResponse(ChatCompletionResponse response)
    {
        {MAP_RESPONSE_IMPLEMENTATION}
    }
}
```

## 🎯 AXON-SPECIFIC AUTOMATION WORKFLOWS

### 🚀 Complete Module Scaffolding
```bash
// Generate complete bounded context module using MCP
const scaffoldAxonModule = async (moduleName, initialAggregates) => {
  console.log(`🏗️ Scaffolding Axon Module: ${moduleName}`);
  
  // Initialize Claude Flow for module scaffolding via MCP (auto-spawning preferred)
  await mcp__claude-flow__task_orchestrate({
    task: `Scaffold ${moduleName} Module`,
    strategy: "adaptive",
    maxAgents: 6  // Auto-spawns needed agents
  });
  
  // Create module structure via MCP
  await mcp__claude-flow__parallel_execute({
    tasks: [
      {
        name: "module-structure",
        description: "Create module folder structure",
        directories: [
          `src/Modules/${moduleName}/Application/Commands`,
          `src/Modules/${moduleName}/Application/Queries`,
          `src/Modules/${moduleName}/Application/Abstractions`,
          `src/Modules/${moduleName}/Domain/Aggregates`,
          `src/Modules/${moduleName}/Domain/ValueObjects`,
          `src/Modules/${moduleName}/Domain/Events`,
          `src/Modules/${moduleName}/Infrastructure`,
          `tests/Modules.${moduleName}.Application.Tests`,
          `tests/Modules.${moduleName}.Domain.Tests`,
          `tests/Modules.${moduleName}.Infrastructure.Tests`
        ]
      },
    "aggregates": {
      "description": "Generate initial aggregates",
      "aggregates": "'$initial_aggregates'",
      "template": "ddd-aggregate-full"
    },
    "service-registration": {
      "description": "Create service registration",
      "file": "src/Modules/'$module_name'/Infrastructure/ServiceRegistration.cs",
      "template": "axon-service-registration"
    },
    "api-endpoints": {
      "description": "Create API endpoints structure",
      "directories": [
        "src/Api/Endpoints/'$module_name'",
        "src/Api/Contracts/'$module_name'"
      ]
    }
  }'
  
  ]);
  
  // Update architecture tests via MCP
  await mcp__claude-flow__task_orchestrate({
    task: `Update architecture tests for new module ${moduleName}`,
    strategy: "sequential",
    maxAgents: 1
  });
  
  console.log(`✅ Module ${moduleName} scaffolded successfully`);
};
```

---

**SUBSYSTEM ACTIVATION**: This pattern library loads automatically when Axon Backend development tasks are detected. All patterns follow Clean Architecture, DDD, and CQRS principles with comprehensive testing.