using Axon.Modules.Chat.Application.DTOs.ViewModels;
using Axon.Modules.Chat.Application.Common.Models;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Application.Contracts.Persistence;

public interface IChatReadDbContext : IReadDbContext<ChatModule>
{
    DbSet<ConversationHeaderRow> ConversationHeaders { get; }
    DbSet<MessageRow> Messages { get; }
}