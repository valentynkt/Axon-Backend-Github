using NUnit.Framework;
using NSubstitute;

namespace Axon.Modules.Identity.Application;

/// <summary>
/// Base class for Identity Application tests using mocks only.
/// No database dependencies - pure unit testing approach.
/// </summary>
[TestFixture]
[CancelAfter(30000)] // 30 second timeout for all tests
public abstract class ApplicationTestBase
{
    [SetUp]
    public virtual void SetUp()
    {
        // Base setup for Application layer tests
        // Child classes can override for additional setup
    }

    [TearDown]
    public virtual void TearDown()
    {
        // Base cleanup for Application layer tests
        // Child classes can override for additional cleanup
    }

    /// <summary>
    /// Creates a substitute for any interface T for mocking.
    /// </summary>
    protected static T CreateMock<T>() where T : class
    {
        return Substitute.For<T>();
    }
}