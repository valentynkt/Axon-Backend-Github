using Axon.Modules.Identity.Domain.Tests.TestData;

namespace Axon.Modules.Identity.Domain.Tests.Rules;

[TestFixture]
public class TimestampMonotonicityRuleTests
{
    private readonly DateTimeOffset _baseTime = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
    
    [Test]
    public void NewTimestamp_AfterBothExisting_ShouldNotBreakRule()
    {
        // Arrange
        var firstSeen = _baseTime;
        var currentLastSeen = _baseTime.AddHours(1);
        var newObserved = _baseTime.AddHours(2);
        
        var rule = new TimestampMonotonicityRule(currentLastSeen, firstSeen, newObserved);
        
        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }
    
    [Test]
    public void NewTimestamp_EqualToLastSeen_ShouldNotBreakRule()
    {
        // Arrange
        var firstSeen = _baseTime;
        var currentLastSeen = _baseTime.AddHours(1);
        var newObserved = currentLastSeen; // Same as last seen
        
        var rule = new TimestampMonotonicityRule(currentLastSeen, firstSeen, newObserved);
        
        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }
    
    [Test]
    public void NewTimestamp_BeforeFirstSeen_ShouldBreakRule()
    {
        // Arrange
        var firstSeen = _baseTime;
        var currentLastSeen = _baseTime.AddHours(1);
        var newObserved = _baseTime.AddHours(-1); // Before first seen
        
        var rule = new TimestampMonotonicityRule(currentLastSeen, firstSeen, newObserved);
        
        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
    }
    
    [Test]
    public void NewTimestamp_BeforeLastSeen_ShouldBreakRule()
    {
        // Arrange
        var firstSeen = _baseTime;
        var currentLastSeen = _baseTime.AddHours(2);
        var newObserved = _baseTime.AddHours(1); // Between first and last, but before last
        
        var rule = new TimestampMonotonicityRule(currentLastSeen, firstSeen, newObserved);
        
        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
    }
    
    [TestCase(0, 1, 0.5, true, Description = "New timestamp between first and last should break")]
    [TestCase(0, 1, 1, false, Description = "New timestamp equal to last should not break")]
    [TestCase(0, 1, 2, false, Description = "New timestamp after last should not break")]
    [TestCase(0, 1, -1, true, Description = "New timestamp before first should break")]
    public void TimestampValidation_WithVariousOffsets(
        double firstOffset, double lastOffset, double newOffset, bool shouldBreak)
    {
        // Arrange
        var firstSeen = _baseTime.AddHours(firstOffset);
        var currentLastSeen = _baseTime.AddHours(lastOffset);
        var newObserved = _baseTime.AddHours(newOffset);
        
        var rule = new TimestampMonotonicityRule(currentLastSeen, firstSeen, newObserved);
        
        // Act & Assert
        rule.IsBroken().ShouldBe(shouldBreak);
    }
    
    [Test]
    public void Rule_HasCorrectErrorDetails()
    {
        // Arrange
        var rule = new TimestampMonotonicityRule(_baseTime, _baseTime, _baseTime);
        
        // Assert
        rule.Message.ShouldContain("timestamp cannot regress");
        rule.Code.ShouldBe("WALLET.TIMESTAMP_REGRESSION");
    }
}