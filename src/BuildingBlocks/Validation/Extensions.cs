using BuildingBlocks.Exception;
using FluentValidation;

namespace BuildingBlocks.Validation
{
    public static class Extensions
    {
        /// <summary>
        /// Ref https://www.jerriepelser.com/blog/validation-response-aspnet-core-webapi
        /// </summary>
        public static async Task HandleValidationAsync<TRequest>(this IValidator<TRequest> validator, TRequest request, CancellationToken cancellationToken = default)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errorMessage = validationResult.Errors?.First()?.ErrorMessage ?? "Validation failed";
                throw new Exception.ValidationException(errorMessage);
            }
        }
    }
}