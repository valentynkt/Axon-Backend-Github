using Axon.Modules.Identity.Application.Common.Models;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

public interface IIdentityWriteDbContext : IWriteDbContext<IdentityModule>
{
    // No extra members. Add module-specific helpers later only if needed.
}