using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using Axon.Modules.Chat.Domain.Rules;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Domain.Services;

/// <summary>
/// Domain service for user authentication operations.
/// Encapsulates authentication business rules and validation.
/// </summary>
public static class UserAuthenticationDomainService
{
    /// <summary>
    /// Validates user authentication and creates a UserId.
    /// </summary>
    /// <param name="isAuthenticated">Whether the user is authenticated</param>
    /// <param name="userIdString">The user ID string from authentication</param>
    /// <returns>Result with UserId if valid, Error if not</returns>
    public static Result<UserId, Error> ValidateAndCreateUserId(bool isAuthenticated, string? userIdString)
    {
        try
        {
            // Check authentication
            CheckRule(new UserMustBeAuthenticatedRule(isAuthenticated, userIdString));
            
            // Check UserId format
            CheckRule(new UserIdMustBeValidFormatRule(userIdString));
            
            // Parse the UserId - we know it's valid because the rule passed
            var ownerGuid = Guid.Parse(userIdString!);
            return Result.Success<UserId, Error>(new UserId(ownerGuid));
        }
        catch (BusinessRuleException ex)
        {
            return ex.Error.Type switch
            {
                ErrorType.Unauthorized => Result.Failure<UserId, Error>(Error.Unauthorized(ex.Message, ex.Error.Code)),
                ErrorType.Validation => Result.Failure<UserId, Error>(Error.Validation(ex.Message, ex.Error.Code)),
                _ => Result.Failure<UserId, Error>(Error.Validation(ex.Message, ex.Error.Code))
            };
        }
    }

    private static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
            throw new BusinessRuleException(rule);
    }
}