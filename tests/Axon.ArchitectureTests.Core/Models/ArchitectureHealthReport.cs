namespace Axon.ArchitectureTests.Core.Models;

/// <summary>
/// Comprehensive architecture health report
/// </summary>
public class ArchitectureHealthReport
{
    public string OverallScore { get; set; } = "Healthy";
    public Dictionary<string, string> MetricScores { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}