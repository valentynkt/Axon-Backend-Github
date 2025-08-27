using System;
using System.Collections.Generic;
using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Queries.GetConversationMessages;
using Axon.Modules.Chat.Application.Tests.Builders;
using Axon.Modules.Chat.Application.Tests.Common;
using FluentValidation.TestHelper;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Application.Tests.Queries.GetConversationMessages;

[TestFixture]
public class GetConversationMessagesValidatorTests : ApplicationTestBase
{
    private GetConversationMessagesValidator _validator = null!;

    [SetUp]
    public new void SetUp()
    {
        base.SetUp();
        _validator = new GetConversationMessagesValidator();
    }

    #region Valid Query Tests

    [Test]
    public void Validate_WithValidQuery_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithFirstPage()
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestCaseSource(nameof(GetValidQueryScenarios))]
    public void Validate_WithValidQueryVariations_ShouldNotHaveValidationErrors(
        GetConversationMessagesQuery query,
        string _)
    {
        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Conversation ID Validation Tests

    [Test]
    public void Validate_WithEmptyConversationId_ShouldHaveValidationError()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithEmptyConversationId()
            .WithFirstPage()
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ConversationId)
            .WithErrorMessage("Conversation ID is required.");
    }

    #endregion

    #region Page Number Validation Tests

    [Test]
    public void Validate_WithZeroPageNumber_ShouldHaveValidationError()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithInvalidPageNumber()
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageNumber)
            .WithErrorMessage("Page number must be 1 or greater.");
    }

    [Test]
    public void Validate_WithNegativePageNumber_ShouldHaveValidationError()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithNegativePageNumber()
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageNumber)
            .WithErrorMessage("Page number must be 1 or greater.");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(10)]
    [TestCase(100)]
    public void Validate_WithValidPageNumber_ShouldNotHaveValidationError(int pageNumber)
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithPagination(pageNumber, 20)
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PageNumber);
    }

    #endregion

    #region Page Size Validation Tests

    [Test]
    public void Validate_WithZeroPageSize_ShouldHaveValidationError()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithZeroPageSize()
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorMessage($"Page size must be between 1 and {Page.MaxSize}.");
    }

    [Test]
    public void Validate_WithNegativePageSize_ShouldHaveValidationError()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithInvalidPageSize()
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorMessage($"Page size must be between 1 and {Page.MaxSize}.");
    }

    [Test]
    public void Validate_WithOversizedPageSize_ShouldHaveValidationError()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithOversizedPageSize()
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorMessage($"Page size must be between 1 and {Page.MaxSize}.");
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(20)]
    [TestCase(50)]
    [TestCase(100)]
    public void Validate_WithValidPageSize_ShouldNotHaveValidationError(int pageSize)
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithPagination(1, pageSize)
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    #endregion

    #region Include Deleted Validation Tests

    [TestCase(true)]
    [TestCase(false)]
    public void Validate_WithIncludeDeletedValue_ShouldNotHaveValidationError(bool includeDeleted)
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithFirstPage()
            .IncludeDeleted(includeDeleted)
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.IncludeDeleted);
    }

    #endregion

    #region Multiple Validation Errors Tests

    [Test]
    public void Validate_WithMultipleInvalidFields_ShouldReturnFirstValidationError()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithEmptyConversationId()
            .WithInvalidPageNumber()
            .WithInvalidPageSize()
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert - Due to CascadeMode.Stop, only the first validation error is returned
        result.ShouldHaveValidationErrorFor(x => x.PageNumber)
            .WithErrorMessage("Page number must be 1 or greater.");
        
        // Other fields should not have validation errors due to cascade mode stopping
        result.ShouldNotHaveValidationErrorFor(x => x.ConversationId);
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    #endregion

    #region Boundary Value Tests

    [Test]
    public void Validate_WithPageSizeAtMinimumBoundary_ShouldNotHaveValidationError()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithPagination(1, 1)
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Test]
    public void Validate_WithPageSizeAtMaximumBoundary_ShouldNotHaveValidationError()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithPagination(1, Page.MaxSize)
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Test]
    public void Validate_WithPageNumberAtMinimumBoundary_ShouldNotHaveValidationError()
    {
        // Arrange
        var query = QueryTestDataBuilder.GetConversationMessages()
            .WithRandomConversationId()
            .WithPagination(1, 20)
            .Build();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PageNumber);
    }

    #endregion

    #region Test Data Sources

    private static IEnumerable<TestCaseData> GetValidQueryScenarios()
    {
        var conversationId = Guid.NewGuid();

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithFirstPage(10)
                .ExcludeDeleted()
                .Build(),
            "First page with small size, exclude deleted")
            .SetName("ValidQuery_FirstPageSmallExcludeDeleted");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithSecondPage(50)
                .IncludeDeleted()
                .Build(),
            "Second page with large size, include deleted")
            .SetName("ValidQuery_SecondPageLargeIncludeDeleted");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithPagination(10, Page.MaxSize)
                .ExcludeDeleted()
                .Build(),
            "High page number with maximum size")
            .SetName("ValidQuery_HighPageMaxSize");

        yield return new TestCaseData(
            QueryTestDataBuilder.GetConversationMessages()
                .WithConversationId(conversationId)
                .WithPagination(1, 1)
                .IncludeDeleted()
                .Build(),
            "Minimum valid pagination values")
            .SetName("ValidQuery_MinimumPagination");
    }

    #endregion
}