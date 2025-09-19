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
    /// Validates user authentication and creates a AxonUserId.
    /// </summary>
    /// <param name="isAuthenticated">Whether the user is authenticated</param>
    /// <param name="userIdString">The user ID string from authentication</param>
    /// <returns>Result with AxonUserId if valid, Error if not</returns>
    public static Result<AxonUserId, Error> ValidateAndCreateAxonUserId(bool isAuthenticated, string? userIdString)
    {
        try
        {
            // Check authentication
            CheckRule(new UserMustBeAuthenticatedRule(isAuthenticated, userIdString));
            
            // Check AxonUserId format
            CheckRule(new UserIdMustBeValidFormatRule(userIdString));
            
            // Parse the AxonUserId - we know it's valid because the rule passed
            var ownerGuid = Guid.Parse(userIdString!);
            return Result.Success<AxonUserId, Error>(new AxonUserId(ownerGuid));
        }
        catch (BusinessRuleException ex)
        {
            return ex.Error.Type switch
            {
                ErrorType.Unauthorized => Result.Failure<AxonUserId, Error>(Error.Unauthorized(ex.Message, ex.Error.Code)),
                ErrorType.Validation => Result.Failure<AxonUserId, Error>(Error.Validation(ex.Message, ex.Error.Code)),
                _ => Result.Failure<AxonUserId, Error>(Error.Validation(ex.Message, ex.Error.Code))
            };
        }
    }

    private static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
            throw new BusinessRuleException(rule);
    }
}