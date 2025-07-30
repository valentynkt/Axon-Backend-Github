using System.Reflection;
using System.Xml.Linq;

namespace Axon.ArchitectureTests;

/// <summary>
/// Architecture tests to validate test project structure and configuration.
/// Ensures proper test project setup and adherence to standards.
/// </summary>
[TestFixture]
public class TestProjectStructureTests
{
    private static readonly string TestsDirectory = GetTestsDirectory();

    [Test]
    public void TestProjects_ShouldHaveCorrectSdkReference()
    {
        var testProjectFiles = GetAllTestProjectFiles();

        foreach (var projectFile in testProjectFiles)
        {
            var doc = XDocument.Load(projectFile);
            var projectElement = doc.Element("Project");
            
            projectElement.ShouldNotBeNull($"Project file {projectFile} should have a Project root element");
            
            var sdkAttribute = projectElement!.Attribute("Sdk")?.Value;
            (sdkAttribute?.StartsWith("Microsoft.NET.Sdk") == true).ShouldBeTrue(
                $"Test project {Path.GetFileName(projectFile)} should use Microsoft.NET.Sdk");
        }
    }

    [Test]
    public void TestProjects_ShouldBeMarkedAsTestProjects()
    {
        var testProjectFiles = GetAllTestProjectFiles();

        foreach (var projectFile in testProjectFiles)
        {
            var doc = XDocument.Load(projectFile);
            var isTestProject = doc.Descendants("IsTestProject")
                .Any(e => e.Value.Equals("true", StringComparison.OrdinalIgnoreCase));

            isTestProject.ShouldBeTrue(
                $"Test project {Path.GetFileName(projectFile)} should have <IsTestProject>true</IsTestProject>");
        }
    }

    [Test]
    public void TestProjects_ShouldNotBePackable()
    {
        var testProjectFiles = GetAllTestProjectFiles();

        foreach (var projectFile in testProjectFiles)
        {
            var doc = XDocument.Load(projectFile);
            var isPackableElements = doc.Descendants("IsPackable").ToList();
            
            if (isPackableElements.Count > 0)
            {
                var isPackable = isPackableElements.Any(e => 
                    e.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
                
                isPackable.ShouldBeFalse(
                    $"Test project {Path.GetFileName(projectFile)} should have <IsPackable>false</IsPackable>");
            }
        }
    }

    [Test]
    public void TestProjects_ShouldReferenceRequiredNuGetPackages()
    {
        var requiredPackages = new[]
        {
            "Microsoft.NET.Test.Sdk",
            "NUnit",
            "NUnit3TestAdapter", 
            "Shouldly",
            "coverlet.collector"
        };

        var testProjectFiles = GetAllTestProjectFiles()
            .Where(f => !Path.GetFileName(f).Contains("ArchitectureTests")); // Exclude self

        foreach (var projectFile in testProjectFiles)
        {
            var doc = XDocument.Load(projectFile);
            var packageReferences = doc.Descendants("PackageReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            foreach (var requiredPackage in requiredPackages)
            {
                var hasPackage = packageReferences.Any(pkg => 
                    pkg?.Equals(requiredPackage, StringComparison.OrdinalIgnoreCase) == true);

                hasPackage.ShouldBeTrue(
                    $"Test project {Path.GetFileName(projectFile)} should reference {requiredPackage}");
            }
        }
    }

    [Test]
    public void TestProjects_ShouldNotReferenceProhibitedPackages()
    {
        var prohibitedPackages = new[]
        {
            "xunit",
            "xunit.core",
            "xunit.runner.visualstudio",
            "xunit.analyzers",
            "FluentAssertions"
        };

        var testProjectFiles = GetAllTestProjectFiles();

        foreach (var projectFile in testProjectFiles)
        {
            var doc = XDocument.Load(projectFile);
            var packageReferences = doc.Descendants("PackageReference")
                .Select(e => e.Attribute("Include")?.Value?.ToLowerInvariant())
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            foreach (var prohibitedPackage in prohibitedPackages)
            {
                var hasProhibitedPackage = packageReferences.Any(pkg => 
                    pkg?.Equals(prohibitedPackage.ToLowerInvariant()) == true);

                hasProhibitedPackage.ShouldBeFalse(
                    $"Test project {Path.GetFileName(projectFile)} should not reference prohibited package: {prohibitedPackage}");
            }
        }
    }

    [Test]
    public void TestProjects_ShouldHaveProperGlobalUsings()
    {
        var expectedGlobalUsings = new[]
        {
            "NUnit.Framework",
            "Shouldly"
        };

        var testProjectFiles = GetAllTestProjectFiles()
            .Where(f => !Path.GetFileName(f).Contains("ArchitectureTests")); // Exclude self

        foreach (var projectFile in testProjectFiles)
        {
            var doc = XDocument.Load(projectFile);
            var globalUsings = doc.Descendants("Using")
                .Where(e => e.Attribute("Include") != null)
                .Select(e => e.Attribute("Include")!.Value)
                .ToList();

            foreach (var expectedUsing in expectedGlobalUsings)
            {
                var hasUsing = globalUsings.Any(u => 
                    u.Equals(expectedUsing, StringComparison.OrdinalIgnoreCase));

                hasUsing.ShouldBeTrue(
                    $"Test project {Path.GetFileName(projectFile)} should have global using for {expectedUsing}");
            }
        }
    }

    [Test]
    public void TestProjects_ShouldFollowNamingConvention()
    {
        var testProjectFiles = GetAllTestProjectFiles();

        foreach (var projectFile in testProjectFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(projectFile);
            var followsConvention = fileName.EndsWith(".Tests") || fileName.Contains("Tests");

            followsConvention.ShouldBeTrue(
                $"Test project {fileName} should follow naming convention ending with '.Tests' or containing 'Tests'");
        }
    }

    [Test]
    public void TestProjects_ShouldHaveCorrectTreatWarningsAsErrorsSetting()
    {
        var testProjectFiles = GetAllTestProjectFiles();

        foreach (var projectFile in testProjectFiles)
        {
            var doc = XDocument.Load(projectFile);
            var fileName = Path.GetFileName(projectFile);
            
            // Check if TreatWarningsAsErrors is explicitly set
            var treatWarningsAsErrorsElements = doc.Descendants("TreatWarningsAsErrors").ToList();
            
            if (treatWarningsAsErrorsElements.Any())
            {
                var treatWarningsAsErrors = treatWarningsAsErrorsElements.First().Value;
                
                // Architecture tests should have warnings as errors, others inherit from Directory.Build.props
                if (fileName.Contains("ArchitectureTests"))
                {
                    treatWarningsAsErrors.ShouldBe("true",
                        $"Architecture test projects should have TreatWarningsAsErrors=true for strict enforcement");
                }
            }
        }
    }

    [Test]
    public void TestAssemblyNames_ShouldFollowNamingConvention()
    {
        var testAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.Contains("Tests") == true)
            .Where(a => !a.IsDynamic);

        foreach (var assembly in testAssemblies)
        {
            var assemblyName = assembly.GetName().Name!;
            var followsConvention = assemblyName.StartsWith("Axon.") && 
                                   (assemblyName.EndsWith(".Tests") || assemblyName.Contains("Tests"));

            followsConvention.ShouldBeTrue(
                $"Test assembly {assemblyName} should follow naming convention: Axon.[Module].[Layer].Tests");
        }
    }

    private static string GetTestsDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var testsDir = currentDirectory;
        
        while (!Directory.Exists(Path.Combine(testsDir, "tests")) && 
               Path.GetDirectoryName(testsDir) != null)
        {
            testsDir = Path.GetDirectoryName(testsDir)!;
        }
        
        return Path.Combine(testsDir, "tests");
    }

    private static IEnumerable<string> GetAllTestProjectFiles()
    {
        if (!Directory.Exists(TestsDirectory))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.GetFiles(TestsDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f).Contains("Test"));
    }
}