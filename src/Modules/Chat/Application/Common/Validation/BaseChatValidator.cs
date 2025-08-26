// /Modules/Chat/Application/Common/Validation/BaseChatValidator.cs
#nullable enable
using BuildingBlocks.Application.Validation.Base;
using BuildingBlocks.Core.Abstractions.CQRS;
using FluentValidation;

namespace Axon.Modules.Chat.Application.Common.Validation;

/// <summary>
/// Base validator for Chat module requests, providing common validation logic.
/// Extends BuildingBlocks BaseValidator for consistent validation patterns.
/// </summary>
/// <typeparam name="T">The request type to validate</typeparam>
public abstract class BaseChatValidator<T> : BaseValidator<T>
    where T : class
{
}

/// <summary>
/// Base validator for Chat module requests that include pagination parameters.
/// Provides common pagination validation rules that can be extended by specific validators.
/// </summary>
/// <typeparam name="T">The paginated request type to validate</typeparam>
public abstract class BasePaginatedChatValidator<T> : BaseChatValidator<T>
    where T : class, IPaginatedRequest
{
    protected BasePaginatedChatValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be 1 or greater.")
            .LessThanOrEqualTo(10000)
            .WithMessage("Page number must be between 1 and 10,000.");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("Page size must be between 1 and 100.")
            .LessThanOrEqualTo(100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}