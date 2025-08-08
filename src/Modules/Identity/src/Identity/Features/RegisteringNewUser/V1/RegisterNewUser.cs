using BuildingBlocks.Constants;
using BuildingBlocks.Core.Event;
using BuildingBlocks.Persistence.Common;
using BuildingBlocks.Persistence.Common.Interfaces;
using BuildingBlocks.Persistence.Write;
using Duende.IdentityServer.EntityFramework.Entities;
using Identity.Data;
using Identity.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Identity.Identity.Features.RegisteringNewUser.V1;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using BuildingBlocks.Contracts.EventBus.Messages;
using BuildingBlocks.Core;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Web;
using Exceptions;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Models;

public record RegisterNewUser(string FirstName, string LastName, string Username, string Email,
    string Password, string ConfirmPassword, string PassportNumber) : ICommand<RegisterNewUserResult>;

public record RegisterNewUserResult(Guid Id, string FirstName, string LastName, string Username, string PassportNumber);

public record RegisterNewUserRequestDto(string FirstName, string LastName, string Username, string Email,
    string Password, string ConfirmPassword, string PassportNumber);

public record RegisterNewUserResponseDto(Guid Id, string FirstName, string LastName, string Username,
    string PassportNumber);

public class RegisterNewUserEndpoint : IMinimalEndpoint
{
    public IEndpointRouteBuilder MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost($"{EndpointConfig.BaseApiPath}/identity/register-user", async (
                RegisterNewUserRequestDto request, IMediator mediator, IMapper mapper,
                CancellationToken cancellationToken) =>
            {
                var command = mapper.Map<RegisterNewUser>(request);

                var result = await mediator.Send(command, cancellationToken);

                var response = result.Adapt<RegisterNewUserResponseDto>();

                return Results.Ok(response);
            })
            .RequireAuthorization(nameof(ApiScope))
            .WithName("RegisterUser")
            .WithApiVersionSet(builder.NewApiVersionSet("Identity").Build())
            .Produces<RegisterNewUserResponseDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Register User")
            .WithDescription("Register User")
            .WithOpenApi()
            .HasApiVersion(1.0);

        return builder;
    }
}

public class RegisterNewUserValidator : AbstractValidator<RegisterNewUser>
{
    public RegisterNewUserValidator()
    {
        RuleFor(x => x.Password).NotEmpty().WithMessage("Please enter the password");
        RuleFor(x => x.ConfirmPassword).NotEmpty().WithMessage("Please enter the confirmation password");

        RuleFor(x => x).Custom((x, context) =>
        {
            if (x.Password != x.ConfirmPassword)
            {
                context.AddFailure(nameof(x.Password), "Passwords should match");
            }
        });

        RuleFor(x => x.Username).NotEmpty().WithMessage("Please enter the username");
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("Please enter the first name");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Please enter the last name");
        RuleFor(x => x.Email).NotEmpty().WithMessage("Please enter the last email")
            .EmailAddress().WithMessage("A valid email is required");
    }
}

internal class RegisterNewUserHandler : ICommandHandler<RegisterNewUser, RegisterNewUserResult>
{
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly IWriteUnitOfWork<IdentityWriteContext> _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;

    public RegisterNewUserHandler(
        IUserWriteRepository userWriteRepository,
        IWriteUnitOfWork<IdentityWriteContext> unitOfWork,
        IPasswordHasher<User> passwordHasher,
        IEventDispatcher eventDispatcher)
    {
        _userWriteRepository = userWriteRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<RegisterNewUserResult> Handle(RegisterNewUser request,
        CancellationToken cancellationToken)
    {
        Guard.Against.Null(request, nameof(request));

        // Check if user already exists
        if (await _userWriteRepository.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            throw new RegisterIdentityUserException($"User with email {request.Email} already exists.");
        }

        if (await _userWriteRepository.ExistsByUserNameAsync(request.Username, cancellationToken))
        {
            throw new RegisterIdentityUserException($"User with username {request.Username} already exists.");
        }

        // Create user aggregate using factory method
        var user = User.Create(
            request.FirstName,
            request.LastName,
            request.Username,
            request.Email,
            request.PassportNumber);

        // Hash the password
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        // Add to repository
        await _userWriteRepository.AddAsync(user, cancellationToken);

        // Save changes and dispatch domain events
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Dispatch integration event
        await _eventDispatcher.SendAsync(new UserCreated(user.Id,
            user.FirstName + " " + user.LastName,
            user.PassPortNumber), cancellationToken: cancellationToken);

        return new RegisterNewUserResult(user.Id, user.FirstName, user.LastName,
            user.UserName!, user.PassPortNumber);
    }
}