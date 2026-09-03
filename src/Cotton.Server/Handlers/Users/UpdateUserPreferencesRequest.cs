// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Hubs;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cotton.Server.Handlers.Users
{
    public record UpdateUserPreferencesRequest(
        Guid UserId,
        IReadOnlyDictionary<string, string> Patch,
        string Token) : IRequest<IReadOnlyDictionary<string, string>>;

    public class UpdateUserPreferencesRequestHandler(
        CottonDbContext _dbContext,
        IHubContext<EventHub> _hubContext,
        ILogger<UpdateUserPreferencesRequestHandler> _logger)
        : IRequestHandler<UpdateUserPreferencesRequest, IReadOnlyDictionary<string, string>>
    {
        private const string DashboardPinnedFolderIdsKey = "dashboardPinnedFolderIds";
        private const int MaximumPinnedFolders = 128;

        public async Task<IReadOnlyDictionary<string, string>> Handle(
            UpdateUserPreferencesRequest request,
            CancellationToken ct)
        {
            ValidateDashboardPinnedFolders(request.UserId, request.Patch);

            User user = await _dbContext.Users
                .FirstOrDefaultAsync(candidate => candidate.Id == request.UserId, ct)
                ?? throw new EntityNotFoundException<User>();
            foreach ((string key, string value) in request.Patch)
            {
                user.Preferences[key] = value;
            }

            await _dbContext.SaveChangesAsync(ct);
            await _hubContext.Clients.User(request.UserId.ToString()).SendAsync(
                "PreferencesUpdated",
                request.Token,
                user.Preferences,
                ct);
            return user.Preferences;
        }

        private void ValidateDashboardPinnedFolders(
            Guid userId,
            IReadOnlyDictionary<string, string> patch)
        {
            if (!patch.TryGetValue(DashboardPinnedFolderIdsKey, out string? value))
            {
                return;
            }

            Guid[]? folderIds;
            try
            {
                folderIds = JsonSerializer.Deserialize<Guid[]>(value);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Rejected malformed pinned-folder preferences for user {UserId}.",
                    userId);
                throw new BadRequestException("Pinned folder ids must be a JSON array of GUIDs.");
            }

            if (folderIds is null)
            {
                throw new BadRequestException("Pinned folder ids must be a JSON array of GUIDs.");
            }
            if (folderIds.Length > MaximumPinnedFolders)
            {
                throw new BadRequestException(
                    $"A maximum of {MaximumPinnedFolders} pinned folders is allowed.");
            }
            if (folderIds.Any(folderId => folderId == Guid.Empty)
                || folderIds.Distinct().Count() != folderIds.Length)
            {
                throw new BadRequestException("Pinned folder ids must be unique non-empty GUIDs.");
            }
        }
    }
}
