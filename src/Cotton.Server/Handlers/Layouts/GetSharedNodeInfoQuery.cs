// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Layouts
{
    public record GetSharedNodeInfoQuery(string Token, HttpContext HttpContext)
        : IRequest<SharedNodeInfoDto?>;

    public class GetSharedNodeInfoQueryHandler(
        IMediator mediator,
        ISharedLinkAccessNotifier notifications,
        SettingsProvider settings)
        : IRequestHandler<GetSharedNodeInfoQuery, SharedNodeInfoDto?>
    {
        public async Task<SharedNodeInfoDto?> Handle(GetSharedNodeInfoQuery request, CancellationToken ct)
        {
            SharedNodeAccess? access = await mediator.Send(new ResolveSharedNodeAccessQuery(request.Token), ct);
            if (access is null)
            {
                return null;
            }

            await notifications.NotifyOnceAsync(
                access.CreatedByUserId, access.ShareId, access.Name, request.HttpContext, ct);

            return new SharedNodeInfoDto
            {
                Token = access.Token,
                NodeId = access.NodeId,
                Name = access.HideFolderContent ? string.Empty : access.Name,
                ExpiresAt = access.ExpiresAt,
                AllowUpload = access.AllowUpload,
                HideFolderContent = access.HideFolderContent,
                MaxChunkSizeBytes = settings.GetServerSettings().MaxChunkSizeBytes,
                SupportedHashAlgorithm = Hasher.SupportedHashAlgorithmName.Name!,
            };
        }
    }
}
