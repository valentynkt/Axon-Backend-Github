using System.Diagnostics;
using Axon.Modules.Chat.Domain.Types;

namespace Axon.Modules.Chat.Domain.Tests.Types;

/// <summary>
/// Comprehensive unit tests for ToolExecution domain type covering all scenarios and edge cases
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Types")]
public sealed class ToolExecutionTests
{
    #region Constructor Tests

    [Test]
    public void Constructor_WithValidParameters_ShouldCreateSuccessfully()
    {
        // Arrange
        const string toolName = "test-tool";
        const string arguments = """{"param1": "value1"}""";
        const string result = "execution result";
        var executionTime = TimeSpan.FromMilliseconds(150);
        const bool isSuccess = true;

        // Act
        var toolExecution = new ToolExecution(toolName, arguments, result, executionTime, isSuccess);

        // Assert
        toolExecution.ToolName.ShouldBe(toolName);
        toolExecution.Arguments.ShouldBe(arguments);
        toolExecution.Result.ShouldBe(result);
        toolExecution.ExecutionTime.ShouldBe(executionTime);
        toolExecution.IsSuccess.ShouldBe(isSuccess);
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidToolName_ShouldThrowArgumentException(string? invalidToolName)
    {
        // Arrange
        const string arguments = """{"param1": "value1"}""";
        const string result = "execution result";
        var executionTime = TimeSpan.FromMilliseconds(150);

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => 
            new ToolExecution(invalidToolName!, arguments, result, executionTime, true));
        
        exception.Message.ShouldContain("Tool name cannot be null or empty");
        exception.ParamName.ShouldBe("toolName");
    }

    [Test]
    public void Constructor_WithNullArguments_ShouldDefaultToEmptyString()
    {
        // Arrange
        const string toolName = "test-tool";
        const string result = "execution result";
        var executionTime = TimeSpan.FromMilliseconds(150);

        // Act
        var toolExecution = new ToolExecution(toolName, null!, result, executionTime, true);

        // Assert
        toolExecution.Arguments.ShouldBe(string.Empty);
    }

    [Test]
    public void Constructor_WithNullResult_ShouldDefaultToEmptyString()
    {
        // Arrange
        const string toolName = "test-tool";
        const string arguments = """{"param1": "value1"}""";
        var executionTime = TimeSpan.FromMilliseconds(150);

        // Act
        var toolExecution = new ToolExecution(toolName, arguments, null!, executionTime, true);

        // Assert
        toolExecution.Result.ShouldBe(string.Empty);
    }

    #endregion

    #region Factory Method Tests - Success

    [Test]
    public void Success_WithValidParameters_ShouldCreateSuccessfulExecution()
    {
        // Arrange
        const string toolName = "calculate-sum";
        const string arguments = """{"numbers": [1, 2, 3]}""";
        const string result = "6";
        var executionTime = TimeSpan.FromMilliseconds(250);

        // Act
        var toolExecution = ToolExecution.Success(toolName, arguments, result, executionTime);

        // Assert
        toolExecution.ToolName.ShouldBe(toolName);
        toolExecution.Arguments.ShouldBe(arguments);
        toolExecution.Result.ShouldBe(result);
        toolExecution.ExecutionTime.ShouldBe(executionTime);
        toolExecution.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public void Success_WithEmptyArguments_ShouldCreateSuccessfulExecution()
    {
        // Arrange
        const string toolName = "get-time";
        const string arguments = "";
        const string result = "2024-01-01T10:00:00Z";
        var executionTime = TimeSpan.FromMilliseconds(50);

        // Act
        var toolExecution = ToolExecution.Success(toolName, arguments, result, executionTime);

        // Assert
        toolExecution.ToolName.ShouldBe(toolName);
        toolExecution.Arguments.ShouldBe(arguments);
        toolExecution.Result.ShouldBe(result);
        toolExecution.ExecutionTime.ShouldBe(executionTime);
        toolExecution.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public void Success_WithZeroExecutionTime_ShouldCreateSuccessfulExecution()
    {
        // Arrange
        const string toolName = "instant-tool";
        const string arguments = """{"action": "instant"}""";
        const string result = "completed instantly";
        var executionTime = TimeSpan.Zero;

        // Act
        var toolExecution = ToolExecution.Success(toolName, arguments, result, executionTime);

        // Assert
        toolExecution.ExecutionTime.ShouldBe(TimeSpan.Zero);
        toolExecution.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region Factory Method Tests - Failure

    [Test]
    public void Failure_WithValidParameters_ShouldCreateFailedExecution()
    {
        // Arrange
        const string toolName = "broken-tool";
        const string arguments = """{"param": "invalid"}""";
        const string errorMessage = "Invalid parameter provided";
        var executionTime = TimeSpan.FromMilliseconds(100);

        // Act
        var toolExecution = ToolExecution.Failure(toolName, arguments, errorMessage, executionTime);

        // Assert
        toolExecution.ToolName.ShouldBe(toolName);
        toolExecution.Arguments.ShouldBe(arguments);
        toolExecution.Result.ShouldBe($"Error: {errorMessage}");
        toolExecution.ExecutionTime.ShouldBe(executionTime);
        toolExecution.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public void Failure_WithEmptyErrorMessage_ShouldCreateFailedExecutionWithErrorPrefix()
    {
        // Arrange
        const string toolName = "failing-tool";
        const string arguments = """{"test": true}""";
        const string errorMessage = "";
        var executionTime = TimeSpan.FromMilliseconds(75);

        // Act
        var toolExecution = ToolExecution.Failure(toolName, arguments, errorMessage, executionTime);

        // Assert
        toolExecution.Result.ShouldBe("Error: ");
        toolExecution.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public void Failure_WithLongExecutionTime_ShouldCreateFailedExecution()
    {
        // Arrange
        const string toolName = "timeout-tool";
        const string arguments = """{"timeout": 30000}""";
        const string errorMessage = "Operation timed out after 30 seconds";
        var executionTime = TimeSpan.FromSeconds(30);

        // Act
        var toolExecution = ToolExecution.Failure(toolName, arguments, errorMessage, executionTime);

        // Assert
        toolExecution.ExecutionTime.ShouldBe(TimeSpan.FromSeconds(30));
        toolExecution.Result.ShouldContain("Operation timed out");
        toolExecution.IsSuccess.ShouldBeFalse();
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Test]
    public void Constructor_WithVeryLongToolName_ShouldCreateSuccessfully()
    {
        // Arrange
        var longToolName = new string('a', 1000);
        const string arguments = """{"test": true}""";
        const string result = "success";
        var executionTime = TimeSpan.FromMilliseconds(100);

        // Act
        var toolExecution = new ToolExecution(longToolName, arguments, result, executionTime, true);

        // Assert
        toolExecution.ToolName.ShouldBe(longToolName);
    }

    [Test]
    public void Constructor_WithComplexJsonArguments_ShouldCreateSuccessfully()
    {
        // Arrange
        const string toolName = "complex-tool";
        const string complexArguments = """
        {
            "nested": {
                "array": [1, 2, {"key": "value"}],
                "boolean": true,
                "null_value": null,
                "unicode": "测试"
            },
            "special_chars": "!@#$%^&*()_+-=[]{}|;':\",./<>?"
        }
        """;
        const string result = "processed complex data";
        var executionTime = TimeSpan.FromMilliseconds(500);

        // Act
        var toolExecution = new ToolExecution(toolName, complexArguments, result, executionTime, true);

        // Assert
        toolExecution.Arguments.ShouldBe(complexArguments);
        toolExecution.ToolName.ShouldBe(toolName);
    }

    [Test]
    public void Constructor_WithNegativeExecutionTime_ShouldAcceptValue()
    {
        // Arrange
        const string toolName = "negative-time-tool";
        const string arguments = """{"test": true}""";
        const string result = "result";
        var negativeTime = TimeSpan.FromMilliseconds(-100);

        // Act
        var toolExecution = new ToolExecution(toolName, arguments, result, negativeTime, true);

        // Assert
        toolExecution.ExecutionTime.ShouldBe(negativeTime);
    }

    [Test]
    public void Constructor_WithMaxTimeSpanValue_ShouldCreateSuccessfully()
    {
        // Arrange
        const string toolName = "max-time-tool";
        const string arguments = """{"test": true}""";
        const string result = "result";
        var maxTime = TimeSpan.MaxValue;

        // Act
        var toolExecution = new ToolExecution(toolName, arguments, result, maxTime, true);

        // Assert
        toolExecution.ExecutionTime.ShouldBe(maxTime);
    }

    #endregion

    #region Record Equality and Hash Code Tests

    [Test]
    public void Equality_WithIdenticalValues_ShouldBeEqual()
    {
        // Arrange
        const string toolName = "test-tool";
        const string arguments = """{"param": "value"}""";
        const string result = "success";
        var executionTime = TimeSpan.FromMilliseconds(100);

        var execution1 = new ToolExecution(toolName, arguments, result, executionTime, true);
        var execution2 = new ToolExecution(toolName, arguments, result, executionTime, true);

        // Act & Assert
        execution1.ShouldBe(execution2);
        execution1.Equals(execution2).ShouldBeTrue();
        (execution1 == execution2).ShouldBeTrue();
        (execution1 != execution2).ShouldBeFalse();
    }

    [Test]
    public void Equality_WithDifferentToolNames_ShouldNotBeEqual()
    {
        // Arrange
        const string arguments = """{"param": "value"}""";
        const string result = "success";
        var executionTime = TimeSpan.FromMilliseconds(100);

        var execution1 = new ToolExecution("tool1", arguments, result, executionTime, true);
        var execution2 = new ToolExecution("tool2", arguments, result, executionTime, true);

        // Act & Assert
        execution1.ShouldNotBe(execution2);
        execution1.Equals(execution2).ShouldBeFalse();
        (execution1 == execution2).ShouldBeFalse();
        (execution1 != execution2).ShouldBeTrue();
    }

    [Test]
    public void Equality_WithDifferentIsSuccessValues_ShouldNotBeEqual()
    {
        // Arrange
        const string toolName = "test-tool";
        const string arguments = """{"param": "value"}""";
        const string result = "result";
        var executionTime = TimeSpan.FromMilliseconds(100);

        var execution1 = new ToolExecution(toolName, arguments, result, executionTime, true);
        var execution2 = new ToolExecution(toolName, arguments, result, executionTime, false);

        // Act & Assert
        execution1.ShouldNotBe(execution2);
    }

    [Test]
    public void GetHashCode_WithIdenticalValues_ShouldBeEqual()
    {
        // Arrange
        const string toolName = "test-tool";
        const string arguments = """{"param": "value"}""";
        const string result = "success";
        var executionTime = TimeSpan.FromMilliseconds(100);

        var execution1 = new ToolExecution(toolName, arguments, result, executionTime, true);
        var execution2 = new ToolExecution(toolName, arguments, result, executionTime, true);

        // Act
        var hash1 = execution1.GetHashCode();
        var hash2 = execution2.GetHashCode();

        // Assert
        hash1.ShouldBe(hash2);
    }

    [Test]
    public void GetHashCode_WithDifferentValues_ShouldGenerateDifferentHashes()
    {
        // Arrange
        var execution1 = ToolExecution.Success("tool1", """{"a": 1}""", "result1", TimeSpan.FromMilliseconds(100));
        var execution2 = ToolExecution.Success("tool2", """{"a": 2}""", "result2", TimeSpan.FromMilliseconds(200));

        // Act
        var hash1 = execution1.GetHashCode();
        var hash2 = execution2.GetHashCode();

        // Assert
        hash1.ShouldNotBe(hash2);
    }

    #endregion

    #region ToString and Serialization Tests

    [Test]
    public void ToString_ShouldReturnMeaningfulRepresentation()
    {
        // Arrange
        const string toolName = "test-tool";
        const string arguments = """{"param": "value"}""";
        const string result = "success";
        var executionTime = TimeSpan.FromMilliseconds(100);

        var toolExecution = new ToolExecution(toolName, arguments, result, executionTime, true);

        // Act
        var stringRepresentation = toolExecution.ToString();

        // Assert
        stringRepresentation.ShouldNotBeNullOrEmpty();
        stringRepresentation.ShouldContain(toolName);
    }

    #endregion

    #region Performance and Stress Tests

    [Test]
    public void Creation_WithLargeData_ShouldPerformEfficiently()
    {
        // Arrange
        const string toolName = "large-data-tool";
        var largeArguments = new string('x', 100_000);
        var largeResult = new string('y', 100_000);
        var executionTime = TimeSpan.FromSeconds(1);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var toolExecution = new ToolExecution(toolName, largeArguments, largeResult, executionTime, true);
        stopwatch.Stop();

        // Assert
        toolExecution.ShouldNotBeNull();
        toolExecution.ToolName.ShouldBe(toolName);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100); // Should be very fast
    }

    [Test]
    public void FactoryMethods_BulkCreation_ShouldPerformEfficiently()
    {
        // Arrange
        const int iterationCount = 10_000;
        var executions = new List<ToolExecution>(iterationCount);

        // Act
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterationCount; i++)
        {
            var execution = ToolExecution.Success(
                $"tool-{i}",
                $"{{\"iteration\": {i}}}",
                $"result-{i}",
                TimeSpan.FromMilliseconds(i));
            executions.Add(execution);
        }
        stopwatch.Stop();

        // Assert
        executions.Count.ShouldBe(iterationCount);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000); // Should complete within 1 second
    }

    #endregion

    #region Business Logic Tests

    [Test]
    public void FactoryMethods_ShouldCreateDistinctObjectTypes()
    {
        // Arrange
        const string toolName = "test-tool";
        const string arguments = """{"test": true}""";
        const string result = "operation result";
        const string errorMessage = "operation failed";
        var executionTime = TimeSpan.FromMilliseconds(100);

        // Act
        var successExecution = ToolExecution.Success(toolName, arguments, result, executionTime);
        var failureExecution = ToolExecution.Failure(toolName, arguments, errorMessage, executionTime);

        // Assert
        successExecution.IsSuccess.ShouldBeTrue();
        successExecution.Result.ShouldBe(result);

        failureExecution.IsSuccess.ShouldBeFalse();
        failureExecution.Result.ShouldBe($"Error: {errorMessage}");
        
        successExecution.ShouldNotBe(failureExecution);
    }

    [Test]
    public void Success_WithMultipleCallsSameParameters_ShouldCreateEqualObjects()
    {
        // Arrange
        const string toolName = "consistent-tool";
        const string arguments = """{"consistent": true}""";
        const string result = "consistent result";
        var executionTime = TimeSpan.FromMilliseconds(150);

        // Act
        var execution1 = ToolExecution.Success(toolName, arguments, result, executionTime);
        var execution2 = ToolExecution.Success(toolName, arguments, result, executionTime);

        // Assert
        execution1.ShouldBe(execution2);
        execution1.GetHashCode().ShouldBe(execution2.GetHashCode());
    }

    #endregion
}