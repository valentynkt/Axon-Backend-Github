using NUnit.Framework;
using Shouldly;
using Axon.ArchitectureTests.Framework.Configuration;

namespace Axon.ArchitectureTests.Framework.Tests.Configuration;

[TestFixture]
public sealed class ArchitectureSettingsTests
{
    [Test]
    public void ArchitectureSettings_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var settings = new ArchitectureSettings();

        // Assert
        settings.ShouldNotBeNull();
        settings.Execution.ShouldNotBeNull();
        settings.Logging.ShouldNotBeNull();
        settings.Rules.ShouldNotBeNull();
    }

    [Test]
    public void ExecutionSettings_DefaultValues_ShouldBeReasonable()
    {
        // Act
        var settings = new ExecutionSettings();

        // Assert
        settings.MaxDegreeOfParallelism.ShouldBe(Environment.ProcessorCount);
        settings.TimeoutPerRule.ShouldBe(TimeSpan.FromMinutes(5));
        settings.FailFastOnCritical.ShouldBeFalse();
        settings.EnableDetailedTiming.ShouldBeFalse();
    }

    [Test]
    public void LoggingSettings_DefaultValues_ShouldBeReasonable()
    {
        // Act
        var settings = new LoggingSettings();

        // Assert
        settings.EnableVerboseLogging.ShouldBeFalse();
        settings.LogRuleExecution.ShouldBeTrue();
        settings.LogViolations.ShouldBeTrue();
        settings.LogPerformanceMetrics.ShouldBeFalse();
    }

    [Test]
    public void RuleSettings_DefaultValues_ShouldBeReasonable()
    {
        // Act
        var settings = new RuleSettings();

        // Assert
        settings.TreatWarningsAsErrors.ShouldBeFalse();
        settings.EnabledCategories.ShouldBeNull();
        settings.DisabledRules.ShouldBeNull();
        settings.CustomRuleAssemblies.ShouldBeNull();
    }

    [Test]
    public void ExecutionSettings_SetMaxDegreeOfParallelism_ShouldValidateInput()
    {
        // Arrange
        var settings = new ExecutionSettings();

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => settings.MaxDegreeOfParallelism = 0);
        Should.Throw<ArgumentOutOfRangeException>(() => settings.MaxDegreeOfParallelism = -1);
        
        // Valid values should not throw
        Should.NotThrow(() => settings.MaxDegreeOfParallelism = 1);
        Should.NotThrow(() => settings.MaxDegreeOfParallelism = Environment.ProcessorCount * 2);
    }

    [Test]
    public void ExecutionSettings_SetTimeoutPerRule_ShouldValidateInput()
    {
        // Arrange
        var settings = new ExecutionSettings();

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => settings.TimeoutPerRule = TimeSpan.Zero);
        Should.Throw<ArgumentOutOfRangeException>(() => settings.TimeoutPerRule = TimeSpan.FromMilliseconds(-1));
        
        // Valid values should not throw
        Should.NotThrow(() => settings.TimeoutPerRule = TimeSpan.FromMilliseconds(1));
        Should.NotThrow(() => settings.TimeoutPerRule = TimeSpan.FromHours(1));
    }

    [Test]
    public void ArchitectureSettings_Configuration_ShouldImplementInterfaces()
    {
        // Arrange
        var settings = new ArchitectureSettings();

        // Act & Assert
        settings.ShouldBeAssignableTo<IArchitectureConfiguration>();
        settings.Execution.ShouldBeAssignableTo<IExecutionConfiguration>();
        settings.Logging.ShouldBeAssignableTo<ILoggingConfiguration>();
        settings.Rules.ShouldBeAssignableTo<IRuleConfiguration>();
    }

    [Test]
    public void ArchitectureSettings_ShouldBeSerializable()
    {
        // Arrange
        var originalSettings = new ArchitectureSettings
        {
            Execution = new ExecutionSettings
            {
                MaxDegreeOfParallelism = 4,
                TimeoutPerRule = TimeSpan.FromMinutes(2),
                FailFastOnCritical = true,
                EnableDetailedTiming = true
            },
            Logging = new LoggingSettings
            {
                EnableVerboseLogging = true,
                LogRuleExecution = false,
                LogViolations = true,
                LogPerformanceMetrics = true
            },
            Rules = new RuleSettings
            {
                TreatWarningsAsErrors = true,
                EnabledCategories = new[] { "CleanArchitecture", "CQRS" },
                DisabledRules = new[] { "CA001", "DDD002" },
                CustomRuleAssemblies = new[] { "Custom.Rules.Assembly" }
            }
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(originalSettings);
        var deserializedSettings = System.Text.Json.JsonSerializer.Deserialize<ArchitectureSettings>(json);

        // Assert
        deserializedSettings.ShouldNotBeNull();
        deserializedSettings.Execution.MaxDegreeOfParallelism.ShouldBe(4);
        deserializedSettings.Execution.TimeoutPerRule.ShouldBe(TimeSpan.FromMinutes(2));
        deserializedSettings.Execution.FailFastOnCritical.ShouldBeTrue();
        deserializedSettings.Execution.EnableDetailedTiming.ShouldBeTrue();
        
        deserializedSettings.Logging.EnableVerboseLogging.ShouldBeTrue();
        deserializedSettings.Logging.LogRuleExecution.ShouldBeFalse();
        deserializedSettings.Logging.LogViolations.ShouldBeTrue();
        deserializedSettings.Logging.LogPerformanceMetrics.ShouldBeTrue();
        
        deserializedSettings.Rules.TreatWarningsAsErrors.ShouldBeTrue();
        deserializedSettings.Rules.EnabledCategories.ShouldBe(new[] { "CleanArchitecture", "CQRS" });
        deserializedSettings.Rules.DisabledRules.ShouldBe(new[] { "CA001", "DDD002" });
        deserializedSettings.Rules.CustomRuleAssemblies.ShouldBe(new[] { "Custom.Rules.Assembly" });
    }

    [Test]
    public void RuleSettings_EnabledCategories_ShouldBeCaseInsensitive()
    {
        // Arrange
        var settings = new RuleSettings
        {
            EnabledCategories = new[] { "cleanarchitecture", "CQRS", "DdD" }
        };

        // Act & Assert
        settings.EnabledCategories.ShouldContain("cleanarchitecture");
        settings.EnabledCategories.ShouldContain("CQRS");
        settings.EnabledCategories.ShouldContain("DdD");
    }

    [Test]
    public void RuleSettings_DisabledRules_ShouldPreserveCase()
    {
        // Arrange
        var settings = new RuleSettings
        {
            DisabledRules = new[] { "CA001", "ca002", "DDD003" }
        };

        // Act & Assert
        settings.DisabledRules.ShouldContain("CA001");
        settings.DisabledRules.ShouldContain("ca002");
        settings.DisabledRules.ShouldContain("DDD003");
    }

    [Test]
    public void ExecutionSettings_MaxDegreeOfParallelism_ShouldHaveReasonableBounds()
    {
        // Arrange
        var settings = new ExecutionSettings();

        // Act & Assert
        settings.MaxDegreeOfParallelism.ShouldBeGreaterThan(0);
        settings.MaxDegreeOfParallelism.ShouldBeLessThanOrEqualTo(Environment.ProcessorCount * 4); // Reasonable upper bound
    }

    [Test]
    public void LoggingSettings_CombinationOfFlags_ShouldWork()
    {
        // Arrange
        var settings = new LoggingSettings
        {
            EnableVerboseLogging = true,
            LogRuleExecution = true,
            LogViolations = true,
            LogPerformanceMetrics = true
        };

        // Act & Assert
        settings.EnableVerboseLogging.ShouldBeTrue();
        settings.LogRuleExecution.ShouldBeTrue();
        settings.LogViolations.ShouldBeTrue();
        settings.LogPerformanceMetrics.ShouldBeTrue();
    }

    [Test]
    public void Settings_Modification_ShouldNotAffectDefaults()
    {
        // Arrange
        var settings1 = new ArchitectureSettings();
        var settings2 = new ArchitectureSettings();

        // Act
        settings1.Execution.MaxDegreeOfParallelism = 1;
        settings1.Logging.EnableVerboseLogging = true;
        settings1.Rules.TreatWarningsAsErrors = true;

        // Assert
        settings2.Execution.MaxDegreeOfParallelism.ShouldBe(Environment.ProcessorCount);
        settings2.Logging.EnableVerboseLogging.ShouldBeFalse();
        settings2.Rules.TreatWarningsAsErrors.ShouldBeFalse();
    }

    [Test]
    public void ArchitectureSettings_ToString_ShouldProvideUsefulInformation()
    {
        // Arrange
        var settings = new ArchitectureSettings();

        // Act
        var result = settings.ToString();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("ArchitectureSettings");
    }
}