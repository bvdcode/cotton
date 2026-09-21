// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Nodes;
using EasyExtensions.Models.Dto;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    public static class DirectoryListing
    {
        public static async Task<(List<NodeDto> Nodes, List<TFile> Files, int TotalCount)> ReadPageAsync<TFile>(
            IQueryable<Node> nodesQuery,
            IQueryable<NodeFile> filesQuery,
            int skip,
            int pageSize,
            CancellationToken cancellationToken)
            where TFile : BaseDto<Guid>
        {
            int nodesCount = await nodesQuery.CountAsync(cancellationToken);
            int filesCount = await filesQuery.CountAsync(cancellationToken);
            int nodesToTake = Math.Max(0, Math.Min(pageSize, nodesCount - skip));
            int filesSkip = Math.Max(0, skip - nodesCount);
            int filesToTake = Math.Max(0, pageSize - nodesToTake);

            List<NodeDto> nodes = nodesToTake == 0 ? []
                : await nodesQuery.OrderBy(node => node.NameKey)
                    .Skip(skip).Take(nodesToTake).ProjectToType<NodeDto>().ToListAsync(cancellationToken);
            List<TFile> files = filesToTake == 0 ? []
                : await LoadFilesAsync<TFile>(filesQuery, filesSkip, filesToTake, cancellationToken);
            return (nodes, files, nodesCount + filesCount);
        }

        private static async Task<List<TFile>> LoadFilesAsync<TFile>(
            IQueryable<NodeFile> filesQuery,
            int skip,
            int take,
            CancellationToken cancellationToken)
            where TFile : BaseDto<Guid>
        {
            Guid[] fileIds = await filesQuery.OrderBy(file => file.NameKey).ThenBy(file => file.Id)
                .Skip(skip).Take(take).Select(file => file.Id).ToArrayAsync(cancellationToken);
            if (fileIds.Length == 0)
            {
                return [];
            }
            List<TFile> files = await filesQuery.Where(file => fileIds.Contains(file.Id))
                .Include(file => file.FileManifest).ProjectToType<TFile>().ToListAsync(cancellationToken);
            Dictionary<Guid, int> order = fileIds.Select((id, index) => new { id, index })
                .ToDictionary(item => item.id, item => item.index);
            files.Sort((left, right) => order[left.Id].CompareTo(order[right.Id]));
            return files;
        }
    }
}
