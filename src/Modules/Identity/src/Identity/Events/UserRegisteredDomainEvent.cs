using BuildingBlocks.Core;
using BuildingBlocks.Core.Domain.Events;

namespace Identity.Identity.Events;

public record UserRegisteredDomainEvent(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string UserName) : IDomainEvent;