namespace Axon.ArchitectureTests.Core.Models;

/// <summary>
/// Architecture metrics for performance and quality tracking
/// </summary>
public class ArchitectureMetrics
{
    public double CyclomaticComplexity { get; set; }
    public double CouplingScore { get; set; }
    public double CohesionScore { get; set; }
    public int TotalTypes { get; set; }
    public int TotalViolations { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}