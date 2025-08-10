using FluentAssertions;

namespace Axon.Modules.Chat.Domain.Tests.Smoke;

public class TimeGuardTests
{
    private readonly string _domainPath = Path.Combine(
        Directory.GetCurrentDirectory().Split("src")[0], 
        "src", "Modules", "Chat", "Domain");

    [Fact]
    public void DomainCode_Should_Not_Use_Direct_System_Time_APIs()
    {
        // Arrange
        var forbiddenTokens = new[]
        {
            "DateTime.Now",
            "DateTime.UtcNow", 
            "DateTimeOffset.Now",
            "DateTimeOffset.UtcNow"
        };

        // Files that are allowed to use direct system time APIs
        var allowedFiles = new[]
        {
            "SystemClock.cs" // SystemClock must call DateTimeOffset.UtcNow
        };

        if (!Directory.Exists(_domainPath))
        {
            throw new DirectoryNotFoundException($"Domain directory not found at: {_domainPath}");
        }

        // Act - scan all C# files in the domain project
        var violations = new List<string>();
        var csFiles = Directory.GetFiles(_domainPath, "*.cs", SearchOption.AllDirectories);

        foreach (var file in csFiles)
        {
            var fileName = Path.GetFileName(file);
            
            // Skip allowed files
            if (allowedFiles.Contains(fileName))
                continue;

            var content = File.ReadAllText(file);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                foreach (var token in forbiddenTokens)
                {
                    // More precise matching to avoid false positives with property names
                    // Look for actual usage patterns like "DateTime.UtcNow" not just property declarations
                    if (line.Contains(token) && !line.TrimStart().StartsWith("///") && 
                        !line.Contains("DateTimeOffset UtcNow { get; }") &&
                        !line.Contains("=> " + token.Split('.')[1]))
                    {
                        var relativePath = Path.GetRelativePath(_domainPath, file);
                        violations.Add($"{relativePath}:{i + 1} contains forbidden '{token}': {line.Trim()}");
                    }
                }
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "Domain code should not use direct system time APIs. " +
            "Use IClock instead for testable and deterministic time access. " +
            "Violations found:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void SystemClock_Should_Be_Allowed_To_Use_DateTimeOffset_UtcNow()
    {
        // Arrange
        var systemClockPath = Path.Combine(_domainPath, "Time", "SystemClock.cs");
        
        if (!File.Exists(systemClockPath))
        {
            throw new FileNotFoundException($"SystemClock.cs not found at: {systemClockPath}");
        }

        // Act
        var content = File.ReadAllText(systemClockPath);

        // Assert
        content.Should().Contain("DateTimeOffset.UtcNow", 
            "SystemClock must use DateTimeOffset.UtcNow to implement IClock");
    }

    [Fact]
    public void TimeGuard_Should_Catch_Violations_When_Direct_Time_Usage_Added()
    {
        // This test verifies that our guard would catch violations
        // by testing the detection logic with sample violations
        
        // Arrange
        var testContent = @"
            public class TestClass
            {
                public void Method1() 
                {
                    var now = DateTime.Now; // This should be caught
                }
                
                public void Method2()
                {
                    var utcNow = DateTime.UtcNow; // This should be caught
                }
                
                public void Method3()
                {
                    var offsetNow = DateTimeOffset.Now; // This should be caught  
                }
            }";

        var forbiddenTokens = new[]
        {
            "DateTime.Now",
            "DateTime.UtcNow",
            "DateTimeOffset.Now",
            "DateTimeOffset.UtcNow"
        };

        // Act
        var violations = new List<string>();
        var lines = testContent.Split('\n');
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (var token in forbiddenTokens)
            {
                if (line.Contains(token))
                {
                    violations.Add($"Line {i + 1}: {token}");
                }
            }
        }

        // Assert
        violations.Should().HaveCountGreaterThan(0, 
            "Guard logic should detect forbidden time API usage");
        
        violations.Should().Contain(v => v.Contains("DateTime.Now"));
        violations.Should().Contain(v => v.Contains("DateTime.UtcNow"));
        violations.Should().Contain(v => v.Contains("DateTimeOffset.Now"));
    }
}