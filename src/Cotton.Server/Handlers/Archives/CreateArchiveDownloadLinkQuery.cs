// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Archives
{
    public record CreateArchiveDownloadLinkQuery(Guid UserId, CreateArchiveDownloadLinkRequest Selection, string? ShareToken = null)
        : IRequest<CreateArchiveDownloadLinkResult>;

    public class CreateArchiveDownloadLinkQueryHandler(
        CottonDbContext database, ArchiveDownloadTicketStore tickets, IMediator mediator)
        : IRequestHandler<CreateArchiveDownloadLinkQuery, CreateArchiveDownloadLinkResult>
    {
        public async Task<CreateArchiveDownloadLinkResult> Handle(CreateArchiveDownloadLinkQuery request, CancellationToken ct)
        {
            Guid[] fileIds = [.. (request.Selection.FileIds ?? []).Where(id => id != Guid.Empty).Distinct()];
            Guid[] nodeIds = [.. (request.Selection.NodeIds ?? []).Where(id => id != Guid.Empty).Distinct()];
            if (fileIds.Length + nodeIds.Length == 0)
            {
                return CreateArchiveDownloadLinkResult.BadRequest("Select at least one file or folder to download.");
            }

            List<string> fileNames = await database.NodeFiles.AsNoTracking()
                .Where(file => fileIds.Contains(file.Id) && file.OwnerId == request.UserId && file.Node.Type == NodeType.Default)
                .Select(file => file.Name).ToListAsync(ct);
            List<string> folderNames = await database.Nodes.AsNoTracking()
                .Where(node => nodeIds.Contains(node.Id) && node.OwnerId == request.UserId && node.Type == NodeType.Default)
                .Select(node => node.Name).ToListAsync(ct);
            if (fileNames.Count != fileIds.Length || folderNames.Count != nodeIds.Length)
            {
                return CreateArchiveDownloadLinkResult.NotFound("One or more selected items were not found.");
            }

            string? name = request.Selection.ArchiveName;
            if (string.IsNullOrWhiteSpace(name) && fileNames.Count + folderNames.Count == 1)
            {
                name = fileNames.Concat(folderNames).Single();
            }
            string fileName = BuildFileName(name);
            ArchiveDownloadTicket ticket = new(request.UserId, fileName, fileIds, nodeIds,
                request.Selection.EnforcePublicShareLimits, request.ShareToken);
            if (ticket.EnforcePublicShareLimits)
            {
                IAsyncEnumerable<ArchiveDownloadEntry> entries = await mediator.Send(new ReadArchiveEntriesQuery(ticket, false), ct);
                int count = 0;
                await foreach (ArchiveDownloadEntry entry in entries.WithCancellation(ct))
                {
                    if (++count > ArchiveDownloadLimits.PublicShareMaxEntries)
                    {
                        return CreateArchiveDownloadLinkResult.BadRequest(ArchiveDownloadLimits.PublicShareLimitMessage);
                    }
                }
            }

            string token = tickets.Store(ticket);
            return CreateArchiveDownloadLinkResult.Success(new ArchiveDownloadLinkDto
            {
                Url = $"{Routes.V1.Archives}/{token}",
                FileName = fileName,
            });
        }

        private static string BuildFileName(string? requestedName)
        {
            string name = requestedName?.Trim() ?? "cotton-download";
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^4];
            }
            if (!NameValidator.TryNormalizeAndValidate(name, out string normalized, out _))
            {
                normalized = "cotton-download";
            }
            return normalized + ".zip";
        }
    }
}
