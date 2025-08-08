using Microsoft.AspNetCore.Identity;
using BuildingBlocks.Core;
using BuildingBlocks.Core.Domain.Events;
using Identity.Identity.Events;

namespace Identity.Identity.Models;

public class User : IdentityUser<Guid>, IAggregate<Guid>, IVersion
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string PassPortNumber { get; init; }
    public long Version { get; set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public IEvent[] ClearDomainEvents()
    {
        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    // Factory method for creating new users
    public static User Create(
        string firstName,
        string lastName,
        string userName,
        string email,
        string passPortNumber)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            UserName = userName,
            Email = email,
            PassPortNumber = passPortNumber,
            EmailConfirmed = false,
            PhoneNumberConfirmed = false,
            TwoFactorEnabled = false,
            LockoutEnabled = true,
            AccessFailedCount = 0
        };

        // Add domain event for user registration
        user.AddDomainEvent(new UserRegisteredDomainEvent(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email!,
            user.UserName!));

        return user;
    }
}