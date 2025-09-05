using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.UpsertWalletActivity;

/// <summary>
/// Handles UpsertWalletActivity command.
/// Single write surface for wallet "touch", meta patch, and tag add/remove 
/// (also register on the fly if needed).
/// INTERNAL USE ONLY - Not part of public Identity API surface.
/// </summary>
internal sealed class UpsertWalletActivityHandler : BaseIdentityIdempotentCommandHandler<UpsertWalletActivityCommand, WalletActivityResponse>
{
    private readonly IWalletWriteRepository _walletRepository;
    private readonly ILogger<UpsertWalletActivityHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public UpsertWalletActivityHandler(
        ICurrentUserService currentUserService,
        IWalletWriteRepository walletRepository,
        TimeProvider timeProvider,
        ILogger<UpsertWalletActivityHandler> logger)
        : base(currentUserService)
    {
        _walletRepository = walletRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override async Task<Result<WalletActivityResponse, Error>> Handle(
        UpsertWalletActivityCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing UpsertWalletActivity command");

        // Validate command
        if (!command.IsValid())
        {
            return Result.Failure<WalletActivityResponse, Error>(
                IdentityDomainErrors.Validation.EitherWalletIdOrCoordinatesRequired());
        }

        // Resolve wallet
        var walletResult = await ResolveOrCreateWalletAsync(command, cancellationToken);
        if (walletResult.IsFailure)
        {
            return Result.Failure<WalletActivityResponse, Error>(walletResult.Error);
        }

        var (wallet, wasCreated) = walletResult.Value;

        // Track changes
        var keysChanged = new List<string>();
        var tagsAdded = new List<string>();
        var tagsRemoved = new List<string>();

        // Apply LastSeenAt bump if ObservedAt provided
        if (command.ObservedAt.HasValue)
        {
            var touchResult = wallet.TouchSeen(command.ObservedAt.Value);
            if (touchResult.IsFailure)
            {
                return Result.Failure<WalletActivityResponse, Error>(touchResult.Error);
            }
        }

        // Apply MetaPatch if provided
        if (command.MetaPatch is not null && command.MetaPatch.Count > 0)
        {
            // TODO: Update profile functionality - temporarily disabled
            // Profile updates disabled temporarily

            keysChanged.AddRange(command.MetaPatch.Keys);
        }

        // Apply tag changes
        if (command.TagsToAdd is not null)
        {
            foreach (var tagValue in command.TagsToAdd)
            {
                // Snapshot existing tags before adding
                var existingTags = wallet.Tags.Select(t => t.Value).ToHashSet();
                
                var addTagResult = wallet.AddTag(tagValue);
                if (addTagResult.IsFailure)
                {
                    return Result.Failure<WalletActivityResponse, Error>(addTagResult.Error);
                }

                // Only add to diff if tag was not already present
                if (!existingTags.Contains(tagValue))
                {
                    tagsAdded.Add(tagValue);
                }
            }
        }

        if (command.TagsToRemove is not null)
        {
            foreach (var tagValue in command.TagsToRemove)
            {
                var originalTagCount = wallet.Tags.Count;
                var removeTagResult = wallet.RemoveTag(tagValue);
                if (removeTagResult.IsFailure)
                {
                    return Result.Failure<WalletActivityResponse, Error>(removeTagResult.Error);
                }

                // Check if tag was actually removed
                if (wallet.Tags.Count < originalTagCount)
                {
                    tagsRemoved.Add(tagValue);
                }
            }
        }

        // Handle overlapping add/remove operations (set semantics - remove wins)
        if (command.TagsToAdd is not null && command.TagsToRemove is not null)
        {
            var overlapping = command.TagsToAdd.Intersect(command.TagsToRemove).ToArray();
            foreach (var overlapTag in overlapping)
            {
                // Remove from added list since remove wins
                tagsAdded.Remove(overlapTag);
                // Ensure it's in removed list
                if (!tagsRemoved.Contains(overlapTag))
                {
                    tagsRemoved.Add(overlapTag);
                }
            }
        }

        // Save changes
        await _walletRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // Determine status
        var status = wasCreated 
            ? WalletActivityStatus.RegisteredAndUpdated
            : (keysChanged.Count > 0 || tagsAdded.Count > 0 || tagsRemoved.Count > 0 || command.ObservedAt.HasValue)
                ? WalletActivityStatus.Updated
                : WalletActivityStatus.NoOp;

        return Result.Success<WalletActivityResponse, Error>(new WalletActivityResponse(
            Wallet: MapToWalletDto(wallet),
            KeysChanged: keysChanged.Count > 0 ? keysChanged.ToArray() : null,
            TagDiff: new TagDiff(tagsAdded.ToArray(), tagsRemoved.ToArray()),
            Status: status
        ));
    }

    private async Task<Result<(Wallet wallet, bool wasCreated), Error>> ResolveOrCreateWalletAsync(
        UpsertWalletActivityCommand command,
        CancellationToken cancellationToken)
    {
        // Try to find by WalletId first
        if (command.WalletId.HasValue)
        {
            var wallet = await _walletRepository.GetByIdAsync(command.WalletId.Value, cancellationToken);
            if (wallet is not null)
            {
                return Result.Success<(Wallet, bool), Error>((wallet, false));
            }

            return Result.Failure<(Wallet, bool), Error>(
                WalletDomainErrors.Wallet.NotFound());
        }

        // Resolve by ChainId + Address
        if (command.ChainId.HasValue && !string.IsNullOrEmpty(command.RawAddress))
        {
            var addressResult = Address.CreateForChain(command.ChainId.Value, command.RawAddress);
            if (addressResult.IsFailure)
            {
                return Result.Failure<(Wallet, bool), Error>(addressResult.Error);
            }

            var existingWallet = await _walletRepository.GetByChainAndAddressAsync(
                command.ChainId.Value, addressResult.Value, cancellationToken);

            if (existingWallet is not null)
            {
                return Result.Success<(Wallet, bool), Error>((existingWallet, false));
            }

            // Create new wallet with FirstSeenAt = ObservedAt ?? now
            var firstSeenAt = command.ObservedAt ?? _timeProvider.GetUtcNow();
            var walletResult = await Wallet.RegisterAsync(
                command.ChainId.Value, 
                command.RawAddress,
                firstSeenAt,
                async (chainId, address) => 
                {
                    var existing = await _walletRepository.GetByChainAndAddressAsync(chainId, address, cancellationToken);
                    return existing is not null;
                });

            if (walletResult.IsFailure)
            {
                return Result.Failure<(Wallet, bool), Error>(walletResult.Error);
            }

            var newWallet = walletResult.Value;
            await _walletRepository.AddAsync(newWallet, cancellationToken);

            return Result.Success<(Wallet, bool), Error>((newWallet, true));
        }

        return Result.Failure<(Wallet, bool), Error>(
            IdentityDomainErrors.Validation.InvalidWalletIdentification());
    }

    // Mapping method
    private static WalletDto MapToWalletDto(Wallet wallet)
    {
        return IdentityDtoMapper.ToWalletDto(wallet, includeTags: true);
    }
}