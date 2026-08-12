// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Users
{
    public class AdminDeleteUserRequest(Guid adminUserId, Guid userId) : IRequest
    {
        public Guid AdminUserId { get; } = adminUserId;

        public Guid UserId { get; } = userId;
    }

    public class AdminDeleteUserRequestHandler(
        CottonDbContext _dbContext,
        ILayoutMutationGate _layoutMutationGate,
        SettingsProvider _settingsProvider,
        SessionAccessTokenRevocationStore _sessionRevocations)
        : IRequestHandler<AdminDeleteUserRequest>
    {
        public async Task Handle(AdminDeleteUserRequest request, CancellationToken cancellationToken)
        {
            if (request.AdminUserId == request.UserId)
            {
                throw new BadRequestException<User>("Administrators cannot delete their own account");
            }

            User admin = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.AdminUserId, cancellationToken)
                    ?? throw new EntityNotFoundException<User>();
            if (admin.Role != UserRole.Admin)
            {
                throw new AccessDeniedException<User>("Only administrators can delete user accounts");
            }

            bool userExists = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.UserId, cancellationToken);
            if (!userExists)
            {
                throw new EntityNotFoundException<User>();
            }

            List<Guid> layoutIds = await _dbContext.UserLayouts
                .AsNoTracking()
                .Where(x => x.OwnerId == request.UserId)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            List<IAsyncDisposable> layoutLeases = [];

            try
            {
                foreach (Guid layoutId in layoutIds)
                {
                    IAsyncDisposable layoutLease = await _layoutMutationGate.EnterAsync(
                        layoutId,
                        cancellationToken);
                    layoutLeases.Add(layoutLease);
                }

                await DeleteUserAsync(request.UserId, cancellationToken);
            }
            finally
            {
                for (int index = layoutLeases.Count - 1; index >= 0; index--)
                {
                    await layoutLeases[index].DisposeAsync();
                }
            }
        }

        private async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            List<string> sessionIds = await _dbContext.RefreshTokens
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.SessionId != null && x.SessionId != string.Empty)
                .Select(x => x.SessionId!)
                .Distinct()
                .ToListAsync(cancellationToken);
            List<Guid> candidateManifestIds = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.OwnerId == userId)
                .Select(x => x.FileManifestId)
                .Distinct()
                .ToListAsync(cancellationToken);

            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);
            try
            {
                await _settingsProvider.ClearDefaultUserTemplateForOwnerAsync(userId, cancellationToken);
                await DeleteDirectUserRecordsAsync(userId, cancellationToken);
                await DeleteUserFileTreeAsync(userId, cancellationToken);
                await DeleteOrphanedManifestsAsync(candidateManifestIds, cancellationToken);

                int deletedUsers = await _dbContext.Users
                    .Where(x => x.Id == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                if (deletedUsers != 1)
                {
                    throw new InvalidOperationException("The user account changed while it was being deleted.");
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            foreach (string sessionId in sessionIds)
            {
                _sessionRevocations.Revoke(userId, sessionId, TimeSpan.Zero);
            }
        }

        private async Task DeleteDirectUserRecordsAsync(Guid userId, CancellationToken cancellationToken)
        {
            await _dbContext.DownloadTokens
                .Where(x => x.CreatedByUserId == userId || x.NodeFile.OwnerId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.NodeShareTokens
                .Where(x => x.CreatedByUserId == userId || x.Node.OwnerId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.Notifications
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.UserPasskeyCredentials
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.UserExternalIdentities
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.OidcLoginStates
                .Where(x => x.LinkUserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.RefreshTokens
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.SyncChanges
                .Where(x => x.OwnerId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.ChunkOwnerships
                .Where(x => x.OwnerId == userId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        private async Task DeleteUserFileTreeAsync(Guid userId, CancellationToken cancellationToken)
        {
            await _dbContext.NodeFiles
                .Where(x => x.OwnerId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            while (await _dbContext.Nodes.AnyAsync(x => x.OwnerId == userId, cancellationToken))
            {
                int deletedNodes = await _dbContext.Nodes
                    .Where(x => x.OwnerId == userId && !x.Children.Any())
                    .ExecuteDeleteAsync(cancellationToken);
                if (deletedNodes == 0)
                {
                    throw new InvalidOperationException("The user folder tree contains unresolved references.");
                }
            }

            await _dbContext.UserLayouts
                .Where(x => x.OwnerId == userId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        private async Task DeleteOrphanedManifestsAsync(
            List<Guid> candidateManifestIds,
            CancellationToken cancellationToken)
        {
            if (candidateManifestIds.Count == 0)
            {
                return;
            }

            List<Guid> orphanManifestIds = await _dbContext.FileManifests
                .AsNoTracking()
                .Where(x => candidateManifestIds.Contains(x.Id) && !x.NodeFiles.Any())
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            if (orphanManifestIds.Count == 0)
            {
                return;
            }

            await _dbContext.FileManifestChunks
                .Where(x => orphanManifestIds.Contains(x.FileManifestId))
                .ExecuteDeleteAsync(cancellationToken);
            int deletedManifests = await _dbContext.FileManifests
                .Where(x => orphanManifestIds.Contains(x.Id) && !x.NodeFiles.Any())
                .ExecuteDeleteAsync(cancellationToken);
            if (deletedManifests != orphanManifestIds.Count)
            {
                throw new InvalidOperationException("A file manifest became referenced while the account was being deleted.");
            }
        }
    }
}
