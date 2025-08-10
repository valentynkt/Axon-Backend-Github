namespace Axon.BuildingBlocks.Tests.Application.Events;

using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Axon.BuildingBlocks.Application.Events.Consumption.TypeResolution;
using Axon.BuildingBlocks.Application.Events.Enveloping;
using Axon.BuildingBlocks.Core.Domain.Events;
using FluentAssertions;
using Xunit;

public sealed class DefaultIntegrationEventTypeCatalogTests
{
    [Fact]
    public void Constructor_Should_Build_Catalog_From_Current_Assembly()
    {
        // Arrange
        var options = Options.Create(new IntegrationEventTypeCatalogOptions
        {
            AssembliesToScan = [Assembly.GetExecutingAssembly()],
            IncludeEntryAssembly = false,
            IncludeReferencedAssemblies = false
        });

        // Act
        var catalog = new DefaultIntegrationEventTypeCatalog(options, NullLogger<DefaultIntegrationEventTypeCatalog>.Instance);

        // Assert
        var mapping = catalog.GetTypeMapping();
        mapping.Should().ContainKey(typeof(TestIntegrationEvent).FullName!);
        mapping.Should().ContainKey(typeof(AttributedIntegrationEvent).FullName!);
        mapping.Should().ContainKey("TestCustomEventName"); // From attribute
    }

    [Fact]
    public void ResolveType_Should_Find_Type_By_Full_Name()
    {
        // Arrange
        var options = Options.Create(new IntegrationEventTypeCatalogOptions
        {
            AssembliesToScan = [Assembly.GetExecutingAssembly()],
            IncludeEntryAssembly = false,
            IncludeReferencedAssemblies = false
        });
        var catalog = new DefaultIntegrationEventTypeCatalog(options, NullLogger<DefaultIntegrationEventTypeCatalog>.Instance);

        // Act
        var result = catalog.ResolveType(typeof(TestIntegrationEvent).FullName!);

        // Assert
        result.Should().Be(typeof(TestIntegrationEvent));
    }

    [Fact]
    public void ResolveType_Should_Find_Type_By_Attribute_Name()
    {
        // Arrange
        var options = Options.Create(new IntegrationEventTypeCatalogOptions
        {
            AssembliesToScan = [Assembly.GetExecutingAssembly()],
            IncludeEntryAssembly = false,
            IncludeReferencedAssemblies = false
        });
        var catalog = new DefaultIntegrationEventTypeCatalog(options, NullLogger<DefaultIntegrationEventTypeCatalog>.Instance);

        // Act
        var result = catalog.ResolveType("TestCustomEventName");

        // Assert
        result.Should().Be(typeof(AttributedIntegrationEvent));
    }

    [Fact]
    public void ResolveType_Should_Return_Null_For_Unknown_Type()
    {
        // Arrange
        var options = Options.Create(new IntegrationEventTypeCatalogOptions
        {
            AssembliesToScan = [],
            IncludeEntryAssembly = false,
            IncludeReferencedAssemblies = false
        });
        var catalog = new DefaultIntegrationEventTypeCatalog(options, NullLogger<DefaultIntegrationEventTypeCatalog>.Instance);

        // Act
        var result = catalog.ResolveType("NonExistentType");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveType_Should_Be_Case_Insensitive()
    {
        // Arrange
        var options = Options.Create(new IntegrationEventTypeCatalogOptions
        {
            AssembliesToScan = [Assembly.GetExecutingAssembly()],
            IncludeEntryAssembly = false,
            IncludeReferencedAssemblies = false
        });
        var catalog = new DefaultIntegrationEventTypeCatalog(options, NullLogger<DefaultIntegrationEventTypeCatalog>.Instance);

        // Act
        var result = catalog.ResolveType("testcustomeventname"); // lowercase

        // Assert
        result.Should().Be(typeof(AttributedIntegrationEvent));
    }

    [Fact]
    public void GetAllTypes_Should_Return_All_Registered_Types()
    {
        // Arrange
        var options = Options.Create(new IntegrationEventTypeCatalogOptions
        {
            AssembliesToScan = [Assembly.GetExecutingAssembly()],
            IncludeEntryAssembly = false,
            IncludeReferencedAssemblies = false
        });
        var catalog = new DefaultIntegrationEventTypeCatalog(options, NullLogger<DefaultIntegrationEventTypeCatalog>.Instance);

        // Act
        var allTypes = catalog.GetAllTypes();

        // Assert
        allTypes.Should().Contain(typeof(TestIntegrationEvent));
        allTypes.Should().Contain(typeof(AttributedIntegrationEvent));
        allTypes.Should().Contain(typeof(DuplicateNameEvent1));
        allTypes.Should().Contain(typeof(DuplicateNameEvent2));
    }

    [Fact]
    public void Constructor_Should_Prioritize_Attributed_Types_Over_Full_Names()
    {
        // Arrange
        var options = Options.Create(new IntegrationEventTypeCatalogOptions
        {
            AssembliesToScan = [Assembly.GetExecutingAssembly()],
            IncludeEntryAssembly = false,
            IncludeReferencedAssemblies = false,
            AttributedNameTakesPrecedence = true
        });
        var catalog = new DefaultIntegrationEventTypeCatalog(options, NullLogger<DefaultIntegrationEventTypeCatalog>.Instance);

        // Act - Both DuplicateNameEvent1 and DuplicateNameEvent2 have same name
        // but only DuplicateNameEvent1 has attribute
        var result = catalog.ResolveType("DuplicateName");

        // Assert - Should resolve to the attributed type
        result.Should().Be(typeof(DuplicateNameEvent1));
    }

    private sealed record TestIntegrationEvent(
        Guid EventId,
        DateTime OccurredAtUtc,
        string Data) : IIntegrationEvent;

    [IntegrationEventName("TestCustomEventName")]
    private sealed record AttributedIntegrationEvent(
        Guid EventId,
        DateTime OccurredAtUtc,
        string Data) : IIntegrationEvent;

    [IntegrationEventName("DuplicateName")]
    private sealed record DuplicateNameEvent1(
        Guid EventId,
        DateTime OccurredAtUtc) : IIntegrationEvent;

    // No attribute, but class name would also match "DuplicateName" pattern
    private sealed record DuplicateNameEvent2(
        Guid EventId,
        DateTime OccurredAtUtc) : IIntegrationEvent;
}