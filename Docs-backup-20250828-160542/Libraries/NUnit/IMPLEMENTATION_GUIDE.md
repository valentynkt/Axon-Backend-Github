# NUnit 4.3.2+ Implementation Guide for .NET 10

## Overview

This guide provides comprehensive documentation for implementing NUnit 4.3.2+ in the Axon Backend project targeting .NET 10. NUnit is a mature, feature-rich testing framework that offers excellent compatibility with Clean Architecture, CQRS, and MediatR patterns.

## Framework Compatibility

### .NET 10 Support
- **NUnit 4.3.2+**: ✅ Fully compatible with .NET 10 preview and release versions
- **Forward Compatibility**: NUnit 4.3.2 adapter explicitly supports future .NET versions "as long as there are no breaking changes"
- **Release Status**: NUnit 4.3.2 released December 28, 2024 (hotfix with version corrections)
- **Minimum Requirements**: .NET 8+ or .NET Framework 4.6.2+

### Licensing
- **License**: MIT (completely open source)
- **Commercial Use**: ✅ No restrictions for commercial applications
- **Enterprise Ready**: Part of .NET Foundation with 600+ million NuGet downloads

## NUnit vs xUnit Comparison for .NET 10

| Aspect | NUnit 4.3.2+ | xUnit 3.0.0 |
|--------|---------------|-------------|
| **.NET 10 Compatibility** | ✅ Explicit forward compatibility | ✅ Native support via Microsoft Testing Platform |
| **Performance** | Good (consumes more resources) | Better (optimized parallel execution) |
| **Features** | Rich attribute system, flexible assertions | Minimal, modern design |
| **Learning Curve** | Moderate (familiar to JUnit users) | Steeper (modern patterns) |
| **Async Support** | Good but not optimized | Excellent, built for async-first |
| **Parallel Execution** | Supported, configurable | Default and optimized |
| **Integration Testing** | Excellent WebApplicationFactory support | Excellent WebApplicationFactory support |
| **Enterprise Features** | Extensive test organization features | Focused on simplicity |
| **Update Frequency** | Slower release cycle | Faster, more frequent updates |

### Recommendation Summary
- **Choose NUnit** for: Complex enterprise applications, teams familiar with traditional testing patterns, extensive test parameterization needs
- **Choose xUnit** for: Modern .NET projects, async-heavy applications, performance-critical test suites, new greenfield projects

## Project Setup

### Required NuGet Packages

```xml
<PackageReference Include="NUnit" Version="4.3.2" />
<PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
<PackageReference Include="NUnit.Analyzers" Version="4.3.0" />

<!-- For Integration Testing -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />

<!-- For Mocking -->
<PackageReference Include="Moq" Version="4.20.72" />
<PackageReference Include="NSubstitute" Version="5.3.0" />

<!-- For Fluent Assertions (Optional but Recommended) -->
<PackageReference Include="FluentAssertions" Version="7.0.0" />
```

### Test Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="NUnit.Framework" />
    <Using Include="FluentAssertions" />
    <Using Include="Moq" />
  </ItemGroup>
</Project>
```

## Testing Patterns for Clean Architecture

### 1. Domain Entity Testing

```csharp
[TestFixture]
public class ConversationTests
{
    [Test]
    public void AddMessage_WithValidContent_ShouldSucceed()
    {
        // Arrange
        var conversation = new Conversation(ConversationId.New(), UserId.New());
        var content = "Hello, world!";
        var userId = UserId.New();

        // Act
        var result = conversation.AddMessage(content, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        conversation.Messages.Should().HaveCount(1);
        conversation.Messages.First().Content.Should().Be(content);
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void AddMessage_WithInvalidContent_ShouldFail(string invalidContent)
    {
        // Arrange
        var conversation = new Conversation(ConversationId.New(), UserId.New());
        var userId = UserId.New();

        // Act
        var result = conversation.AddMessage(invalidContent, userId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.Validation("Message content cannot be empty"));
    }
}
```

### 2. CQRS Command Handler Testing

```csharp
[TestFixture]
public class ProcessMessageHandlerTests
{
    private Mock<IAiClient> _aiClientMock;
    private Mock<IConversationRepository> _repositoryMock;
    private ProcessMessageHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _aiClientMock = new Mock<IAiClient>();
        _repositoryMock = new Mock<IConversationRepository>();
        _handler = new ProcessMessageHandler(_aiClientMock.Object, _repositoryMock.Object);
    }

    [Test]
    public async Task Handle_ValidCommand_ShouldProcessMessage()
    {
        // Arrange
        var command = new ProcessMessageCommand("Hello", Guid.NewGuid());
        var expectedResponse = "AI Response";
        
        _aiClientMock
            .Setup(x => x.ProcessAsync(command.Message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResponse);
        _aiClientMock.Verify(x => x.ProcessAsync(command.Message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_AiClientFailure_ShouldReturnFailure()
    {
        // Arrange
        var command = new ProcessMessageCommand("Hello", Guid.NewGuid());
        var error = Error.Failure("AI processing failed");
        
        _aiClientMock
            .Setup(x => x.ProcessAsync(command.Message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(error));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [TearDown]
    public void TearDown()
    {
        _aiClientMock.Reset();
        _repositoryMock.Reset();
    }
}
```

### 3. Query Handler Testing

```csharp
[TestFixture]
public class GetConversationHandlerTests
{
    private Mock<IConversationRepository> _repositoryMock;
    private GetConversationHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _repositoryMock = new Mock<IConversationRepository>();
        _handler = new GetConversationHandler(_repositoryMock.Object);
    }

    [Test]
    public async Task Handle_ExistingConversation_ShouldReturnDto()
    {
        // Arrange
        var conversationId = ConversationId.New();
        var query = new GetConversationQuery(conversationId.Value);
        var conversation = new Conversation(conversationId, UserId.New());
        
        _repositoryMock
            .Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(conversationId.Value);
    }

    [Test]
    public async Task Handle_NonExistentConversation_ShouldReturnNotFound()
    {
        // Arrange
        var conversationId = ConversationId.New();
        var query = new GetConversationQuery(conversationId.Value);
        
        _repositoryMock
            .Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NotFound("Conversation not found"));
    }
}
```

## Integration Testing with ASP.NET Core

### Custom WebApplicationFactory

```csharp
public class AxonWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // Replace real dependencies with test doubles
            services.RemoveAll<IAiClient>();
            services.AddScoped<IAiClient, MockAiClient>();
            
            // Configure test database
            services.RemoveAll<DbContextOptions<AxonDbContext>>();
            services.AddDbContext<AxonDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));
        });
    }
}
```

### Integration Test Base Class

```csharp
[TestFixture]
public abstract class IntegrationTestBase
{
    protected AxonWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Factory = new AxonWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }
}
```

### API Endpoint Testing

```csharp
[TestFixture]
public class ChatEndpointTests : IntegrationTestBase
{
    [Test]
    public async Task SendMessage_ValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            Message = "Hello, AI!",
            ConversationId = Guid.NewGuid()
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/chat/send", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<SendMessageResponse>(responseContent);
        result.Should().NotBeNull();
        result!.Response.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task SendMessage_EmptyMessage_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            Message = "",
            ConversationId = Guid.NewGuid()
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/chat/send", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

## Advanced NUnit Features

### Parameterized Tests

```csharp
[TestFixture]
public class ValidationTests
{
    [Test]
    [TestCase("valid@example.com", ExpectedResult = true)]
    [TestCase("invalid-email", ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    [TestCase(null, ExpectedResult = false)]
    public bool ValidateEmail_VariousInputs_ReturnsExpectedResult(string email)
    {
        return EmailValidator.IsValid(email);
    }

    [Test]
    [TestCaseSource(nameof(GetValidUserData))]
    public void CreateUser_ValidData_ShouldSucceed(string name, string email, int age)
    {
        // Arrange & Act
        var result = User.Create(name, email, age);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    private static IEnumerable<TestCaseData> GetValidUserData()
    {
        yield return new TestCaseData("John Doe", "john@example.com", 25);
        yield return new TestCaseData("Jane Smith", "jane@example.com", 30);
        yield return new TestCaseData("Bob Johnson", "bob@example.com", 35);
    }
}
```

### Test Categories and Organization

```csharp
[TestFixture]
[Category("Unit")]
public class DomainEntityTests
{
    [Test]
    [Category("FastTests")]
    public void QuickValidation_Test() { }

    [Test]
    [Category("SlowTests")]
    [Explicit("Long running test")]
    public void ComplexBusinessRule_Test() { }
}

[TestFixture]
[Category("Integration")]
public class DatabaseTests
{
    [Test]
    [Category("Database")]
    public void Repository_Test() { }
}
```

### Parallel Test Execution

Configure in `AssemblyInfo.cs` or via attributes:

```csharp
[assembly: Parallelizable(ParallelScope.Fixtures)]
[assembly: LevelOfParallelism(4)]

// Or per test fixture
[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class ParallelizableTests
{
    [Test]
    public void Test1() { }

    [Test]
    public void Test2() { }
}
```

## Dependency Injection in Tests

### Using Microsoft.Extensions.DependencyInjection

```csharp
[TestFixture]
public class ServiceIntegrationTests
{
    private ServiceProvider _serviceProvider = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var services = new ServiceCollection();
        
        // Register dependencies
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IConversationRepository, InMemoryConversationRepository>();
        services.AddScoped<IAiClient, MockAiClient>();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Test]
    public void ConversationService_WithDependencies_ShouldWork()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<IConversationService>();

        // Act & Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<ConversationService>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _serviceProvider?.Dispose();
    }
}
```

## Performance and Best Practices

### Test Performance Monitoring

```csharp
[TestFixture]
public class PerformanceTests
{
    [Test]
    [MaxTime(1000)] // Test must complete within 1 second
    public async Task FastOperation_ShouldCompleteQuickly()
    {
        var service = new FastService();
        await service.ProcessAsync();
    }

    [Test]
    public void MeasureExecutionTime()
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        ExpensiveOperation();
        
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
    }
}
```

### Memory Usage Testing

```csharp
[Test]
public void LargeDataProcessing_ShouldNotLeakMemory()
{
    var initialMemory = GC.GetTotalMemory(true);
    
    // Act
    ProcessLargeDataSet();
    
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    
    var finalMemory = GC.GetTotalMemory(false);
    var memoryIncrease = finalMemory - initialMemory;
    
    memoryIncrease.Should().BeLessThan(10_000_000); // Less than 10MB increase
}
```

## Visual Studio Integration

### Test Runner Configuration

NUnit integrates seamlessly with Visual Studio Test Explorer:

- **Discovery**: Tests appear automatically in Test Explorer
- **Categories**: Filter by test categories using Test Explorer filters
- **Live Unit Testing**: Supported in Visual Studio Enterprise
- **Code Coverage**: Integrates with Visual Studio Code Coverage tools

### Test Settings

Create `test.runsettings` for advanced configuration:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <NUnit>
    <NumberOfTestWorkers>4</NumberOfTestWorkers>
    <ShadowCopyFiles>false</ShadowCopyFiles>
    <Verbosity>1</Verbosity>
  </NUnit>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="Code Coverage" uri="datacollector://Microsoft/CodeCoverage/2.0" assemblyQualifiedName="Microsoft.VisualStudio.Coverage.DynamicCoverageDataCollector, Microsoft.VisualStudio.TraceCollector, Version=11.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a">
        <Configuration>
          <CodeCoverage>
            <ModulePaths>
              <Include>
                <ModulePath>.*\.dll$</ModulePath>
              </Include>
            </ModulePaths>
          </CodeCoverage>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

## Migration from xUnit (If Applicable)

### Attribute Mapping

| xUnit | NUnit | Notes |
|-------|-------|-------|
| `[Fact]` | `[Test]` | Basic test method |
| `[Theory]` + `[InlineData]` | `[Test]` + `[TestCase]` | Parameterized tests |
| `[Collection]` | `[TestFixture]` | Test organization |
| Constructor/Dispose | `[SetUp]`/`[TearDown]` | Test lifecycle |

### Code Migration Example

**xUnit:**
```csharp
public class UserServiceTests : IDisposable
{
    private readonly UserService _service;
    
    public UserServiceTests()
    {
        _service = new UserService();
    }
    
    [Fact]
    public void CreateUser_ValidData_Success()
    {
        Assert.True(_service.Create("John", "john@example.com"));
    }
    
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CreateUser_InvalidName_Fails(string name)
    {
        Assert.False(_service.Create(name, "john@example.com"));
    }
    
    public void Dispose() => _service?.Dispose();
}
```

**NUnit:**
```csharp
[TestFixture]
public class UserServiceTests
{
    private UserService _service = null!;
    
    [SetUp]
    public void SetUp()
    {
        _service = new UserService();
    }
    
    [Test]
    public void CreateUser_ValidData_Success()
    {
        _service.Create("John", "john@example.com").Should().BeTrue();
    }
    
    [Test]
    [TestCase("")]
    [TestCase(null)]
    public void CreateUser_InvalidName_Fails(string name)
    {
        _service.Create(name, "john@example.com").Should().BeFalse();
    }
    
    [TearDown]
    public void TearDown() => _service?.Dispose();
}
```

## Troubleshooting Common Issues

### 1. Test Discovery Issues

**Problem**: Tests not appearing in Test Explorer
**Solutions**:
- Ensure `Microsoft.NET.Test.Sdk` and `NUnit3TestAdapter` are installed
- Rebuild the solution
- Clear Visual Studio test cache

### 2. Parallel Execution Problems

**Problem**: Tests fail when run in parallel
**Solutions**:
- Use `[NonParallelizable]` attribute for problematic tests
- Ensure test isolation (no shared static state)
- Use proper setup/teardown for resources

### 3. Async Test Issues

**Problem**: Async tests not completing properly
**Solutions**:
```csharp
[Test]
public async Task AsyncTest_Proper_Pattern()
{
    // ✅ Correct - await the async operation
    var result = await SomeAsyncOperation();
    result.Should().NotBeNull();
}

[Test]
public void AsyncTest_Wrong_Pattern()
{
    // ❌ Wrong - missing await
    var task = SomeAsyncOperation();
    // Test completes before async operation
}
```

## Summary

NUnit 4.3.2+ provides a robust, enterprise-ready testing framework for .NET 10 applications. While xUnit may offer better performance for modern async-heavy applications, NUnit's rich feature set, excellent documentation, and mature ecosystem make it an excellent choice for complex Clean Architecture applications using CQRS and MediatR patterns.

### Key Advantages for Axon Backend:
- ✅ Explicit .NET 10 compatibility guarantee
- ✅ Rich assertion and parameterization features
- ✅ Excellent integration testing capabilities
- ✅ Strong Visual Studio integration
- ✅ MIT license with no commercial restrictions
- ✅ Extensive community and documentation

### Considerations:
- Higher resource usage compared to xUnit
- Slower update cycle
- Not optimized for async-first patterns

Choose NUnit if your team values feature richness, familiar patterns, and extensive test organization capabilities over raw performance and cutting-edge features.