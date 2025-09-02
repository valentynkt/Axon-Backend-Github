using Axon.Modules.Identity.Application.Common.Models;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

public interface IIdentityReadDbContext : IReadDbContext<IdentityModule>
{

}