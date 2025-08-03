namespace Axon.ArchitectureTests.Core.Models;

/// <summary>
/// Enhanced test context for comprehensive architecture testing
/// </summary>
public class EnhancedTestContext
{
    public string TestName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}