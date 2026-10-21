// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class FileVersionStorageTests : IntegrationTestBase
    {
        private DatabaseIntegrityKeyProvider _keys = null!;
        private DatabaseIntegrityProtector _protector = null!;
        private DatabaseIntegrityDescriptorRegistry _descriptors = null!;

        [OneTimeSetUp]
        public async Task CreateDatabase()
        {
            await RecreateDatabaseAsync();
            _keys = new(new CottonEncryptionSettings
            {
                MasterEncryptionKey = Convert.ToBase64String(SHA256.HashData("file-version-deletion"u8))
            });
            _protector = new(_keys);
            _descriptors = new([new UserIntegrityDescriptor(), new NodeIntegrityDescriptor(),
                new NodeFileIntegrityDescriptor(), new FileManifestIntegrityDescriptor(), new DownloadTokenIntegrityDescriptor()]);
        }

        [SetUp]
        public async Task ClearDatabase()
        {
            DbContext.ChangeTracker.Clear();
            DbContext.DownloadTokens.RemoveRange(await DbContext.DownloadTokens.ToListAsync());
            DbContext.NodeFiles.RemoveRange(await DbContext.NodeFiles.ToListAsync());
            DbContext.Nodes.RemoveRange(await DbContext.Nodes.ToListAsync());
            DbContext.UserLayouts.RemoveRange(await DbContext.UserLayouts.ToListAsync());
            DbContext.Users.RemoveRange(await DbContext.Users.ToListAsync());
            DbContext.FileManifests.RemoveRange(await DbContext.FileManifests.ToListAsync());
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }

        [OneTimeTearDown]
        public void DisposeKeys()
        {
            _keys.Dispose();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task DeleteHistoricalVersions_RemovesDependentTokensAndHonorsTransaction(bool keepSibling, bool rollBack)
        {
            await using CottonDbContext context = CreateSignedContext();
            NodeFile[] versions = await SeedAsync(context);
            Guid ownerId = versions[0].OwnerId;
            Guid wrapperId = versions[0].NodeId;
            Guid[] selectedIds = keepSibling ? [versions[0].Id] : [.. versions.Select(version => version.Id)];
            context.ChangeTracker.Clear();
            List<NodeFile> beforeFiles = await context.NodeFiles.Include(file => file.FileManifest).ToListAsync();
            List<DownloadToken> beforeTokens = await context.DownloadTokens.ToListAsync();
            Dictionary<Guid, byte[]> signatures = beforeTokens.ToDictionary(token => token.Id,
                token => (byte[])context.Entry(token).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!);
            NodeFile[] selected = beforeFiles.Where(file => selectedIds.Contains(file.Id)).ToArray();
            long expectedBytes = selected.Sum(file => file.FileManifest.SizeBytes);
            FileVersionStorageService service = new(context);
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

            long removedBytes = await service.DeleteHistoricalVersionsAsync(ownerId, selected);
            Assert.That(removedBytes, Is.EqualTo(expectedBytes));
            Assert.That(await context.NodeFiles.CountAsync(), Is.EqualTo(beforeFiles.Count - selected.Length));
            Assert.That(await context.DownloadTokens.CountAsync(),
                Is.EqualTo(beforeTokens.Count(token => !selectedIds.Contains(token.NodeFileId))));
            if (rollBack)
            {
                await transaction.RollbackAsync();
            }
            else
            {
                await transaction.CommitAsync();
            }

            context.ChangeTracker.Clear();
            List<NodeFile> remainingFiles = await context.NodeFiles.ToListAsync();
            List<DownloadToken> remainingTokens = await context.DownloadTokens.ToListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(remainingFiles.Select(file => file.Id),
                    Is.EquivalentTo(beforeFiles.Where(file => rollBack || !selectedIds.Contains(file.Id)).Select(file => file.Id)));
                Assert.That(remainingTokens.Select(token => token.Id),
                    Is.EquivalentTo(beforeTokens.Where(token => rollBack || !selectedIds.Contains(token.NodeFileId)).Select(token => token.Id)));
            });
            Assert.That(await context.Nodes.AnyAsync(node => node.Id == wrapperId), Is.EqualTo(rollBack || keepSibling));
            DatabaseIntegrityVerifier verifier = new(_protector, _descriptors, NullDatabaseIntegrityFailureReporter.Instance,
                NullLogger<DatabaseIntegrityVerifier>.Instance);
            foreach (DownloadToken token in remainingTokens)
            {
                Assert.That(context.Entry(token).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue,
                    Is.EqualTo(signatures[token.Id]));
                verifier.RequireValid(context, token, "test.remaining-download-token");
            }
            foreach (NodeFile file in remainingFiles)
            {
                verifier.RequireValid(context, file, "test.remaining-version");
            }
        }

        private CottonDbContext CreateSignedContext()
        {
            DatabaseIntegrityChangeSigner signer = new(_protector, _descriptors, NullDatabaseIntegrityFailureReporter.Instance);
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql(TestDataSource);
            options.AddInterceptors(new DatabaseIntegritySaveChangesInterceptor(signer));
            return new(options.Options);
        }

        private static async Task<NodeFile[]> SeedAsync(CottonDbContext context)
        {
            User owner = new() { Username = "versionowner", PasswordPhc = "test", WebDavTokenPhc = "test" };
            User otherOwner = new() { Username = "otherowner", PasswordPhc = "test", WebDavTokenPhc = "test" };
            Layout layout = new() { Owner = owner, IsActive = true };
            Layout otherLayout = new() { Owner = otherOwner, IsActive = true };
            Node root = new() { Owner = owner, Layout = layout, Type = NodeType.Default };
            Node wrapper = new() { Owner = owner, Layout = layout, Type = NodeType.Trash };
            Node otherRoot = new() { Owner = otherOwner, Layout = otherLayout, Type = NodeType.Default };
            root.SetName("root");
            wrapper.SetName("versions");
            otherRoot.SetName("other-root");
            NodeFile current = CreateFile(root, "current.txt", 400);
            NodeFile first = CreateFile(wrapper, "first.txt", 100);
            NodeFile second = CreateFile(wrapper, "second.txt", 200);
            NodeFile other = CreateFile(otherRoot, "other.txt", 300);
            NodeFile[] files = [current, first, second, other];
            context.NodeFiles.AddRange(files);
            foreach (NodeFile file in files)
            {
                file.OriginalNodeFileId = file.Id;
                context.Entry(file.FileManifest).Property<string>("ContentType").CurrentValue = string.Empty;
                context.DownloadTokens.Add(CreateToken(file));
            }
            first.OriginalNodeFileId = current.Id;
            second.OriginalNodeFileId = current.Id;
            context.DownloadTokens.Add(CreateToken(first));
            await context.SaveChangesAsync();
            return [first, second];
        }

        private static NodeFile CreateFile(Node node, string name, long sizeBytes)
        {
            FileManifest manifest = new() { ProposedContentHash = SHA256.HashData(Guid.NewGuid().ToByteArray()), SizeBytes = sizeBytes };
            NodeFile file = new() { Owner = node.Owner, Node = node, FileManifest = manifest };
            file.SetName(name);
            return file;
        }

        private static DownloadToken CreateToken(NodeFile file)
        {
            return new DownloadToken
            {
                NodeFile = file,
                CreatedByUser = file.Owner,
                FileName = file.Name,
                Token = Guid.NewGuid().ToString("N")
            };
        }
    }
}
