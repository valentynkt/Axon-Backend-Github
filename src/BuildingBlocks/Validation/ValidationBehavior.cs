using FluentValidation;
using MediatR;
using BuildingBlocks.Core.Abstractions.CQRS;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Validation;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IAxonRequest<TResponse>
{
    private IValidator<TRequest>? _validator;
    private readonly IServiceProvider _serviceProvider;

    public ValidationBehavior(IServiceProvider serviceProvider, IValidator<TRequest> validator)
    {
        _serviceProvider = serviceProvider;
        _validator = validator;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _validator = _serviceProvider.GetService<IValidator<TRequest>>();
        if (_validator is null)
            return await next(cancellationToken);

        await _validator.HandleValidationAsync(request, cancellationToken);

        return await next(cancellationToken);
    }
}