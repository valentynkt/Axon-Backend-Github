using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.Authentication;
using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using Axon.Modules.Chat.Application.Commands.StartConversation;
using Axon.Modules.Chat.Application.Contracts.Dispatching;
using Axon.Modules.Chat.Application.Services;
using BuildingBlocks.Core.Abstractions.Idempotency;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Axon.Modules.Chat.Application.DependencyInjection;

/// <summary>
/// Validates all critical Chat Application layer DI registrations
/// </summary>
public static class ApplicationDIValidation
{
    /// <summary>
    /// Validates that all Chat Application services can be resolved from the DI container
    /// </summary>
    /// <param name="serviceProvider">The configured service provider</param>
    /// <returns>List of validation errors, if any</returns>
    public static List<string> ValidateApplicationServices(IServiceProvider serviceProvider)
    {
        var errors = new List<string>();

        try
        {
            // Core application services
            TestServiceResolution<IChatCommandDispatcher>(serviceProvider, errors, "IChatCommandDispatcher");
            TestServiceResolution<IUserAuthenticationService>(serviceProvider, errors, "IUserAuthenticationService");
            TestServiceResolution<IMcpServerResolutionService>(serviceProvider, errors, "IMcpServerResolutionService");
            TestServiceResolution<IAiProcessingService>(serviceProvider, errors, "IAiProcessingService");
            TestServiceResolution<IMessageProcessingOrchestrator>(serviceProvider, errors, "IMessageProcessingOrchestrator");
            
            // TimeProvider registration
            TestServiceResolution<TimeProvider>(serviceProvider, errors, "TimeProvider - System time provider");
            
            // MediatR core services
            TestServiceResolution<IMediator>(serviceProvider, errors, "IMediator - MediatR dispatcher");
            
            // Idempotency key providers
            TestServiceResolution<IIdempotencyKeyProvider<AppendUserMessageCommand>>(serviceProvider, errors, "IIdempotencyKeyProvider<AppendUserMessageCommand>");
            TestServiceResolution<IIdempotencyKeyProvider<StartConversationCommand>>(serviceProvider, errors, "IIdempotencyKeyProvider<StartConversationCommand>");
            
            // Validate MediatR handlers are registered (sample key handlers)
            ValidateMediatRHandlers(serviceProvider, errors);
            
            // Validate FluentValidation validators are registered
            ValidateFluentValidationRegistrations(serviceProvider, errors);
            
        }
        catch (Exception ex)
        {
            errors.Add($"CRITICAL: Application DI validation failed with exception: {ex.Message}");
        }

        return errors;
    }

    private static void ValidateMediatRHandlers(IServiceProvider serviceProvider, List<string> errors)
    {
        try
        {
            // Test that MediatR can resolve handlers for critical commands
            var mediator = serviceProvider.GetService<IMediator>();
            if (mediator == null)
            {
                errors.Add("APPLICATION: MediatR is not properly registered - cannot validate handlers");
                return;
            }

            // We can't directly test handler registration without creating real commands,
            // but we can check if the handler types are registered
            var handlerServiceType = typeof(IRequestHandler<,>);
            var services = serviceProvider.GetServices(handlerServiceType);
            
            // At minimum, we should have handlers registered
            if (!services.Any())
            {
                errors.Add("APPLICATION: No MediatR handlers appear to be registered - check assembly scanning");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"APPLICATION: Failed to validate MediatR handlers: {ex.Message}");
        }
    }

    private static void ValidateFluentValidationRegistrations(IServiceProvider serviceProvider, List<string> errors)
    {
        try
        {
            // Check if any validators are registered
            var validatorServiceType = typeof(IValidator<>);
            var validatorFactory = serviceProvider.GetService<IValidatorFactory>();
            
            if (validatorFactory == null)
            {
                errors.Add("APPLICATION: FluentValidation IValidatorFactory is not registered");
            }

            // Try to resolve a specific validator if it exists
            var appendMessageValidator = serviceProvider.GetService<IValidator<AppendUserMessageCommand>>();
            if (appendMessageValidator == null)
            {
                errors.Add("APPLICATION: AppendUserMessageCommand validator not found - check FluentValidation assembly scanning");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"APPLICATION: Failed to validate FluentValidation setup: {ex.Message}");
        }
    }

    private static void TestServiceResolution<T>(IServiceProvider serviceProvider, List<string> errors, string serviceName)
        where T : class
    {
        try
        {
            var service = serviceProvider.GetService<T>();
            if (service == null)
            {
                errors.Add($"APPLICATION: {serviceName} ({typeof(T).Name}) could not be resolved from DI container");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"APPLICATION: {serviceName} ({typeof(T).Name}) failed to resolve: {ex.Message}");
        }
    }
}