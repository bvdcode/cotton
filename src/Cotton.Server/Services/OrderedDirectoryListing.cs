// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Nodes;
using Cotton.Server.Models.Enums;
using Cotton.Server.Models.Requests;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Models.Dto;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    public static class OrderedDirectoryListing
    {
        public static async Task<(List<NodeDto> Nodes, List<TFile> Files, int NodeCount, int FileCount)> ReadPageAsync<TFile>(
            IQueryable<Node> nodesQuery, IQueryable<NodeFile> filesQuery,
            int skip, int pageSize, DirectoryListingOptions options, CancellationToken ct)
            where TFile : BaseDto<Guid>
        {
            bool needsSizes = options.SortBy == DirectoryField.SizeBytes
                || (options.FilterOperator != DirectoryFilterOperator.None && options.FilterBy == DirectoryField.SizeBytes);
            IQueryable<DirectoryPageEntry> entries = nodesQuery.Select(node => new DirectoryPageEntry
            {
                Id = node.Id, IsFolder = true, NameKey = node.NameKey, SizeBytes = null,
            }).Concat(filesQuery.Select(file => new DirectoryPageEntry
            {
                Id = file.Id, IsFolder = false, NameKey = file.NameKey,
                SizeBytes = needsSizes ? file.FileManifest.SizeBytes : null,
            }));
            entries = DirectoryPageFilter.Apply(entries, options);
            int nodeCount = await entries.CountAsync(item => item.IsFolder, ct);
            int fileCount = await entries.CountAsync(item => !item.IsFolder, ct);
            IOrderedQueryable<DirectoryPageEntry> ordered = entries.OrderByDescending(item => item.IsFolder);
            ordered = (options.SortBy, options.Descending) switch
            {
                (DirectoryField.Name, false) => ordered.ThenBy(item => item.NameKey),
                (DirectoryField.Name, true) => ordered.ThenByDescending(item => item.NameKey),
                (DirectoryField.SizeBytes, false) => ordered.ThenBy(item => item.SizeBytes).ThenBy(item => item.NameKey),
                (DirectoryField.SizeBytes, true) => ordered.ThenByDescending(item => item.SizeBytes).ThenBy(item => item.NameKey),
                _ => throw new BadRequestException("Invalid directory sort field."),
            };
            List<DirectoryPageEntry> page = await ordered.ThenBy(item => item.Id).Skip(skip).Take(pageSize).ToListAsync(ct);
            Guid[] nodeIds = [.. page.Where(item => item.IsFolder).Select(item => item.Id)];
            Guid[] fileIds = [.. page.Where(item => !item.IsFolder).Select(item => item.Id)];
            List<NodeDto> nodes = nodeIds.Length == 0 ? [] : await nodesQuery.Where(node => nodeIds.Contains(node.Id))
                .ProjectToType<NodeDto>().ToListAsync(ct);
            List<TFile> files = fileIds.Length == 0 ? [] : await filesQuery.Where(file => fileIds.Contains(file.Id))
                .Include(file => file.FileManifest).ProjectToType<TFile>().ToListAsync(ct);
            Dictionary<Guid, int> order = page.Select((item, index) => new { item.Id, index })
                .ToDictionary(item => item.Id, item => item.index);
            nodes.Sort((left, right) => order[left.Id].CompareTo(order[right.Id]));
            files.Sort((left, right) => order[left.Id].CompareTo(order[right.Id]));
            return (nodes, files, nodeCount, fileCount);
        }
    }
}
