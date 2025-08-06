namespace Axon.Modules.Chat.Domain.Types;

/// <summary>
/// Domain representation of a tool execution result following FRD specifications
/// Contains all properties required for tool execution tracking and analysis
/// </summary>
public sealed record ToolExecutionResult
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
    /// Time taken to execute the tool in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; }
    
    /// <summary>
    /// Whether the tool execution was successful
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// UTC timestamp when the tool execution started
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// UTC timestamp when the tool execution completed
    /// </summary>
    public DateTime CompletedAt { get; }

    /// <summary>
    /// Optional error message if execution failed
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Optional metadata associated with the tool execution
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; }

    public ToolExecutionResult(
        string toolName, 
        string arguments, 
        string result, 
        long executionTimeMs, 
        bool isSuccess,
        DateTime startedAt,
        DateTime completedAt,
        string? errorMessage = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));
            
        ToolName = toolName;
        Arguments = arguments ?? string.Empty;
        Result = result ?? string.Empty;
        ExecutionTimeMs = executionTimeMs;
        IsSuccess = isSuccess;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        ErrorMessage = errorMessage;
        Metadata = metadata;
    }
    
    /// <summary>
    /// Creates a ToolExecutionResult for a successful execution
    /// </summary>
    public static ToolExecutionResult Success(
        string toolName, 
        string arguments, 
        string result, 
        DateTime startedAt,
        DateTime completedAt,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var executionTimeMs = (long)(completedAt - startedAt).TotalMilliseconds;
        return new(toolName, arguments, result, executionTimeMs, true, startedAt, completedAt, null, metadata);
    }
    
    /// <summary>
    /// Creates a ToolExecutionResult for a failed execution
    /// </summary>
    public static ToolExecutionResult Failure(
        string toolName, 
        string arguments, 
        string errorMessage, 
        DateTime startedAt,
        DateTime completedAt,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var executionTimeMs = (long)(completedAt - startedAt).TotalMilliseconds;
        return new(toolName, arguments, string.Empty, executionTimeMs, false, startedAt, completedAt, errorMessage, metadata);
    }

    /// <summary>
    /// Gets the execution time as a TimeSpan for compatibility
    /// </summary>
    public TimeSpan ExecutionTime => TimeSpan.FromMilliseconds(ExecutionTimeMs);

    /// <summary>
    /// Checks if the execution exceeded a specified timeout
    /// </summary>
    /// <param name="timeoutMs">Timeout threshold in milliseconds</param>
    /// <returns>True if execution time exceeded the timeout</returns>
    public bool ExceededTimeout(long timeoutMs) => ExecutionTimeMs > timeoutMs;

    /// <summary>
    /// Gets a summary string for logging purposes
    /// </summary>
    public string GetSummary() => 
        $"{ToolName}: {(IsSuccess ? "Success" : "Failed")} ({ExecutionTimeMs}ms)";
}