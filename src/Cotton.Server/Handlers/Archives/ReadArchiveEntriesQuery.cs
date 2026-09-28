// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Runtime.CompilerServices;

namespace Cotton.Server.Handlers.Archives
{
    public record ReadArchiveEntriesQuery(ArchiveDownloadTicket Ticket, bool IncludeContent = true)
        : IRequest<IAsyncEnumerable<ArchiveDownloadEntry>>;

    public class ReadArchiveEntriesQueryHandler(IServiceScopeFactory scopes)
        : IRequestHandler<ReadArchiveEntriesQuery, IAsyncEnumerable<ArchiveDownloadEntry>>
    {
        public Task<IAsyncEnumerable<ArchiveDownloadEntry>> Handle(ReadArchiveEntriesQuery request, CancellationToken ct)
        {
            return Task.FromResult(ReadAsync(request, ct));
        }

        private async IAsyncEnumerable<ArchiveDownloadEntry> ReadAsync(
            ReadArchiveEntriesQuery request, [EnumeratorCancellation] CancellationToken ct)
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            await using IDbContextTransaction snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            FileGraphIntegrityVerifier contentIntegrity = scope.ServiceProvider.GetRequiredService<FileGraphIntegrityVerifier>();
            IDatabaseIntegrityVerifier integrity = scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>();
            ArchiveDownloadTicket ticket = request.Ticket;
            ArchivePathUniquifier rootNames = new();
            HashSet<Guid> explicitFiles = [.. ticket.FileIds];
            HashSet<Guid> visitedFolders = [];
            Queue<ArchiveFolder> pending = new();

            foreach (Guid[] ids in ticket.FileIds.Chunk(ArchiveDownloadLimits.BatchSize))
            {
                List<NodeFile> files = await LoadFilesAsync(db, ids, ticket.UserId, request.IncludeContent, ct);
                if (files.Count != ids.Length)
                {
                    throw new EntityNotFoundException(nameof(NodeFile), "Selected file no longer exists.");
                }
                Dictionary<Guid, NodeFile> byId = files.ToDictionary(file => file.Id);
                foreach (Guid id in ids)
                {
                    NodeFile file = byId[id];
                    yield return CreateFileEntry(db, contentIntegrity, file, rootNames.AddFile(file.Name), request.IncludeContent);
                }
                db.ChangeTracker.Clear();
            }

            foreach (Guid[] ids in ticket.NodeIds.Chunk(ArchiveDownloadLimits.BatchSize))
            {
                List<Node> nodes = await db.Nodes.Where(node => ids.Contains(node.Id)
                    && node.OwnerId == ticket.UserId && node.Type == NodeType.Default).ToListAsync(ct);
                if (nodes.Count != ids.Length)
                {
                    throw new EntityNotFoundException(nameof(Node), "Selected folder no longer exists.");
                }
                Dictionary<Guid, Node> byId = nodes.ToDictionary(node => node.Id);
                foreach (Guid id in ids)
                {
                    Node node = byId[id];
                    integrity.RequireValid(db, node, "archive.selected-folder");
                    string path = rootNames.AddDirectory(node.Name).TrimEnd('/');
                    visitedFolders.Add(node.Id);
                    pending.Enqueue(new ArchiveFolder(node.Id, node.LayoutId, path));
                    yield return new ArchiveDownloadDirectoryEntry(path + "/");
                }
                db.ChangeTracker.Clear();
            }

            while (pending.TryDequeue(out ArchiveFolder? parent))
            {
                ArchivePathUniquifier siblingNames = new();
                string? lastName = null;
                while (true)
                {
                    IQueryable<Node> query = db.Nodes.Where(node => node.ParentId == parent.Id
                        && node.LayoutId == parent.LayoutId && node.OwnerId == ticket.UserId && node.Type == NodeType.Default);
                    if (lastName is not null)
                    {
                        query = query.Where(node => string.Compare(node.NameKey, lastName) > 0);
                    }
                    List<Node> children = await query.OrderBy(node => node.NameKey).Take(ArchiveDownloadLimits.BatchSize).ToListAsync(ct);
                    foreach (Node child in children)
                    {
                        integrity.RequireValid(db, child, "archive.child-folder");
                        if (!visitedFolders.Add(child.Id))
                        {
                            continue;
                        }
                        string name = siblingNames.AddDirectory(child.Name).TrimEnd('/');
                        string path = ArchivePathUniquifier.Combine(parent.Path, name);
                        pending.Enqueue(new ArchiveFolder(child.Id, child.LayoutId, path));
                        yield return new ArchiveDownloadDirectoryEntry(path + "/");
                    }
                    db.ChangeTracker.Clear();
                    if (children.Count < ArchiveDownloadLimits.BatchSize)
                    {
                        break;
                    }
                    lastName = children[^1].NameKey;
                }

                lastName = null;
                Guid lastId = Guid.Empty;
                while (true)
                {
                    IQueryable<NodeFile> query = db.NodeFiles.Where(file => file.NodeId == parent.Id
                        && file.OwnerId == ticket.UserId && file.Node.Type == NodeType.Default);
                    if (lastName is not null)
                    {
                        query = query.Where(file => string.Compare(file.NameKey, lastName) > 0
                            || file.NameKey == lastName && file.Id.CompareTo(lastId) > 0);
                    }
                    var page = await query.OrderBy(file => file.NameKey).ThenBy(file => file.Id)
                        .Take(ArchiveDownloadLimits.BatchSize).Select(file => new { file.Id, file.NameKey }).ToListAsync(ct);
                    if (page.Count == 0)
                    {
                        break;
                    }
                    Guid[] ids = [.. page.Select(file => file.Id).Where(id => !explicitFiles.Contains(id))];
                    List<NodeFile> files = await LoadFilesAsync(db, ids, ticket.UserId, request.IncludeContent, ct);
                    Dictionary<Guid, NodeFile> byId = files.ToDictionary(file => file.Id);
                    foreach (Guid id in ids)
                    {
                        if (!byId.TryGetValue(id, out NodeFile? file) || file.NodeId != parent.Id)
                        {
                            throw new IOException("Folder contents changed while preparing the archive. Retry the download.");
                        }
                        string path = ArchivePathUniquifier.Combine(parent.Path, siblingNames.AddFile(file.Name));
                        yield return CreateFileEntry(db, contentIntegrity, file, path, request.IncludeContent);
                    }
                    db.ChangeTracker.Clear();
                    lastName = page[^1].NameKey;
                    lastId = page[^1].Id;
                    if (page.Count < ArchiveDownloadLimits.BatchSize)
                    {
                        break;
                    }
                }
            }
            await snapshot.CommitAsync(ct);
        }

        private static Task<List<NodeFile>> LoadFilesAsync(
            CottonDbContext db, Guid[] ids, Guid userId, bool includeContent, CancellationToken ct)
        {
            IQueryable<NodeFile> query = db.NodeFiles.Where(file => ids.Contains(file.Id)
                && file.OwnerId == userId && file.Node.Type == NodeType.Default)
                .Include(file => file.Node).Include(file => file.FileManifest);
            if (includeContent)
            {
                query = query.Include(file => file.FileManifest).ThenInclude(manifest => manifest.FileManifestChunks)
                    .ThenInclude(chunk => chunk.Chunk);
            }
            return query.AsSplitQuery().ToListAsync(ct);
        }

        private static ArchiveDownloadFileEntry CreateFileEntry(
            CottonDbContext db, FileGraphIntegrityVerifier integrity, NodeFile file, string path, bool includeContent)
        {
            if (includeContent)
            {
                integrity.RequireValidContent(db, file, "archive.file");
            }
            else
            {
                integrity.RequireValidMetadata(db, file, "archive.file-count");
            }
            return new ArchiveDownloadFileEntry(path, file.FileManifest.SizeBytes,
                file.FileManifest.FileManifestChunks.GetChunkHashes(),
                file.FileManifest.FileManifestChunks.GetChunkLengths());
        }
    }
}
