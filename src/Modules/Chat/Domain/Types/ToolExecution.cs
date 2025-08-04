namespace Axon.Modules.Chat.Domain.Types;

/// <summary>
/// Domain representation of a tool execution result
/// </summary>
public sealed record ToolExecution
{
    /// <summary>
    /// Name of the tool that was executed
    /// </summary>
    public string ToolName { get; }
    
    /// <summary>
    /// JSON arguments passed to the tool
    /// </summary>
    public string Arguments { get; }
    
    /// <summary>
    /// Result returned by the tool execution
    /// </summary>
    public string Result { get; }
    
    /// <summary>
    /// Time taken to execute the tool
    /// </summary>
    public TimeSpan ExecutionTime { get; }
    
    /// <summary>
    /// Whether the tool execution was successful
    /// </summary>
    public bool IsSuccess { get; }

    public ToolExecution(string toolName, string arguments, string result, TimeSpan executionTime, bool isSuccess)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));
            
        ToolName = toolName;
        Arguments = arguments ?? string.Empty;
        Result = result ?? string.Empty;
        ExecutionTime = executionTime;
        IsSuccess = isSuccess;
    }
    
    /// <summary>
    /// Creates a ToolExecution for a successful execution
    /// </summary>
    public static ToolExecution Success(string toolName, string arguments, string result, TimeSpan executionTime) =>
        new(toolName, arguments, result, executionTime, isSuccess: true);
    
    /// <summary>
    /// Creates a ToolExecution for a failed execution
    /// </summary>
    public static ToolExecution Failure(string toolName, string arguments, string errorMessage, TimeSpan executionTime) =>
        new(toolName, arguments, $"Error: {errorMessage}", executionTime, isSuccess: false);
}