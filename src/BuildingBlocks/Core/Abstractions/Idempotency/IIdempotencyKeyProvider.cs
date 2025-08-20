// /BuildingBlocks/Core/Abstractions/Idempotency/IIdempotencyKeyProvider.cs
using BuildingBlocks.Core.Abstractions.Authentication;

namespace BuildingBlocks.Core.Abstractions.Idempotency;

public interface IIdempotencyKeyProvider<in TCommand>
    where TCommand : IIdempotentCommand
{
    string GenerateKey(TCommand command, ICurrentUserService currentUser);
}