// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Auth;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Layouts;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class ExpiredShareTokenReuseTests : IntegrationTestBase
    {
        private readonly DatabaseIntegrityProtector _protector = DatabaseIntegrityTestData.CreateProtector();
        private readonly DatabaseIntegrityDescriptorRegistry _descriptors = new([
            new DownloadTokenIntegrityDescriptor(),
            new NodeShareTokenIntegrityDescriptor(),
            new NodeIntegrityDescriptor(),
        ]);
        private Guid _ownerId;
        private Guid _folderId;
        private Guid _otherFolderId;
        private Guid _fileId;
        private Guid _otherFileId;

        [SetUp]
        public async Task PrepareShares()
        {
            await RecreateDatabaseAsync();
            await SeedSharesAsync();
        }

        private async Task SeedSharesAsync()
        {
            await using CottonDbContext database = CreateContext();
            User owner = new() { Username = "share-owner", PasswordPhc = "test", WebDavTokenPhc = "test" };
            Layout layout = new() { Owner = owner, IsActive = true };
            Node folder = new() { Owner = owner, Layout = layout, Type = NodeType.Default };
            folder.SetName("original");
            Node otherFolder = new() { Owner = owner, Layout = layout, Parent = folder, Type = NodeType.Default };
            otherFolder.SetName("replacement");
            FileManifest manifest = new() { ProposedContentHash = SHA256.HashData([]) };
            database.Entry(manifest).Property<string>("ContentType").CurrentValue = string.Empty;
            NodeFile file = new() { Owner = owner, Node = folder, FileManifest = manifest };
            file.SetName("original.txt");
            NodeFile otherFile = new() { Owner = owner, Node = otherFolder, FileManifest = manifest };
            otherFile.SetName("replacement.txt");
            database.NodeFiles.AddRange(file, otherFile);
            await database.SaveChangesAsync();
            _ownerId = owner.Id;
            _folderId = folder.Id;
            _otherFolderId = otherFolder.Id;
            _fileId = file.Id;
            _otherFileId = otherFile.Id;
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task ReusesExpiredNameWithoutChangingHistoryOrSignatures(bool oldFolder, bool newFolder)
        {
            const string name = "reusable-name";
            Guid oldId = await AddHistoryAsync(oldFolder, name, DateTime.UtcNow.AddMinutes(-1));
            (byte[] payload, byte[] mac) = await ReadSignatureAsync(oldFolder, oldId);
            await using CottonDbContext database = CreateContext();

            Assert.That(await CreateLinkAsync(database, newFolder, name), Is.True);
            (byte[] retainedPayload, byte[] retainedMac) = await ReadSignatureAsync(oldFolder, oldId);
            Assert.Multiple(() =>
            {
                Assert.That(retainedPayload, Is.EqualTo(payload));
                Assert.That(retainedMac, Is.EqualTo(mac));
            });
            await AssertOneActiveLinkAsync(database, name);
            Assert.That(await database.DownloadTokens.CountAsync(x => x.Token == name)
                + await database.NodeShareTokens.CountAsync(x => x.Token == name), Is.EqualTo(2));
            if (newFolder)
            {
                DatabaseIntegrityVerifier verifier = new(_protector, _descriptors,
                    NullDatabaseIntegrityFailureReporter.Instance, NullLogger<DatabaseIntegrityVerifier>.Instance);
                ResolveSharedNodeAccessQueryHandler handler = new(database, verifier);
                SharedNodeAccess? access = await handler.Handle(new(name), CancellationToken.None);
                Assert.That(access?.NodeId, Is.EqualTo(_otherFolderId));
            }
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        public async Task ActiveAndIndefiniteNamesRemainReserved(bool oldFolder, bool newFolder, bool indefinite)
        {
            const string name = "occupied-name";
            DateTime? expiration = null;
            if (!indefinite)
            {
                expiration = DateTime.UtcNow.AddDays(1);
            }
            Guid oldId = await AddHistoryAsync(oldFolder, name, expiration);
            (byte[] payload, byte[] mac) = await ReadSignatureAsync(oldFolder, oldId);
            await using CottonDbContext database = CreateContext();

            Assert.That(await CreateLinkAsync(database, newFolder, name), Is.False);
            (byte[] retainedPayload, byte[] retainedMac) = await ReadSignatureAsync(oldFolder, oldId);
            Assert.Multiple(() =>
            {
                Assert.That(retainedPayload, Is.EqualTo(payload));
                Assert.That(retainedMac, Is.EqualTo(mac));
            });
            await AssertOneActiveLinkAsync(database, name);
            Assert.That(database.ChangeTracker.Entries().Any(x => x.State == EntityState.Added), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ExpireAllReleasesCustomNames(bool folder)
        {
            const string name = "invalidated-name";
            await AddHistoryAsync(folder, name, null);
            await using CottonDbContext database = CreateContext();
            InvalidateShareLinksRequestHandler invalidate = new(database);
            await invalidate.Handle(new(_ownerId), CancellationToken.None);

            Assert.That(await CreateLinkAsync(database, folder, name), Is.True);
            await AssertOneActiveLinkAsync(database, name);
        }

        [Test]
        public async Task FileDownloadFindsActiveRowAfterMultipleExpiredRows()
        {
            const string name = "file-history";
            await AddHistoryAsync(false, name, DateTime.UtcNow.AddDays(-2), _otherFileId);
            await AddHistoryAsync(false, name, DateTime.UtcNow.AddDays(-1), _otherFileId);
            await using CottonDbContext database = CreateContext();
            Assert.That(await CreateLinkAsync(database, false, name), Is.True);

            DownloadToken? active = await database.DownloadTokens.FindActiveAsync(name, _otherFileId);
            Assert.That(active, Is.Not.Null);
            Assert.That(active!.ExpiresAt, Is.GreaterThan(DateTime.UtcNow));
            Assert.That(await database.DownloadTokens.FindActiveAsync(name, _fileId), Is.Null);
            DatabaseIntegrityVerifier verifier = DatabaseIntegrityTestData.CreateVerifier(_protector, new DownloadTokenIntegrityDescriptor());
            Assert.DoesNotThrow(() => verifier.RequireValid(database, active, "test.reused-file-link"));
        }

        [Test]
        public async Task SchemaUpgradePreservesExistingLinksAndAllowsHistory()
        {
            await DbContext.Database.EnsureDeletedAsync();
            await DbContext.GetService<IMigrator>().MigrateAsync("20261112202849_AddHiddenSharedFolderContent");
            await SeedSharesAsync();
            Guid fileTokenId = await AddHistoryAsync(false, "upgrade-file", DateTime.UtcNow.AddDays(-1));
            Guid folderTokenId = await AddHistoryAsync(true, "upgrade-folder", DateTime.UtcNow.AddDays(-1));
            (byte[] filePayload, byte[] fileMac) = await ReadSignatureAsync(false, fileTokenId);
            (byte[] folderPayload, byte[] folderMac) = await ReadSignatureAsync(true, folderTokenId);

            await DbContext.Database.MigrateAsync();

            (byte[] retainedFilePayload, byte[] retainedFileMac) = await ReadSignatureAsync(false, fileTokenId);
            (byte[] retainedFolderPayload, byte[] retainedFolderMac) = await ReadSignatureAsync(true, folderTokenId);
            Assert.Multiple(() =>
            {
                Assert.That(retainedFilePayload, Is.EqualTo(filePayload));
                Assert.That(retainedFileMac, Is.EqualTo(fileMac));
                Assert.That(retainedFolderPayload, Is.EqualTo(folderPayload));
                Assert.That(retainedFolderMac, Is.EqualTo(folderMac));
            });
            await using CottonDbContext database = CreateContext();
            Assert.That(await CreateLinkAsync(database, false, "upgrade-file"), Is.True);
            Assert.That(await CreateLinkAsync(database, true, "upgrade-folder"), Is.True);
            Assert.That(await database.DownloadTokens.CountAsync(), Is.EqualTo(2));
            Assert.That(await database.NodeShareTokens.CountAsync(), Is.EqualTo(2));
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public async Task SimultaneousRequestsCannotClaimTheSameName(bool firstFolder, bool secondFolder)
        {
            ShareTokenRaceInterceptor race = new();
            await using CottonDbContext first = CreateContext(race);
            await using CottonDbContext second = CreateContext(race);
            bool[] results = await Task.WhenAll(
                CreateLinkAsync(first, firstFolder, "concurrent-name"),
                CreateLinkAsync(second, secondFolder, "concurrent-name"));

            Assert.That(results.Count(x => x), Is.EqualTo(1));
            await AssertOneActiveLinkAsync(DbContext, "concurrent-name");
        }

        [Test]
        public async Task ConcurrentDistinctNamesBothSucceed()
        {
            ShareTokenRaceInterceptor race = new();
            await using CottonDbContext first = CreateContext(race);
            await using CottonDbContext second = CreateContext(race);
            bool[] results = await Task.WhenAll(
                CreateLinkAsync(first, false, "first-name"),
                CreateLinkAsync(second, true, "second-name"));

            Assert.That(results, Is.All.True);
            await AssertOneActiveLinkAsync(DbContext, "first-name");
            await AssertOneActiveLinkAsync(DbContext, "second-name");
        }

        private CottonDbContext CreateContext(DbCommandInterceptor? interceptor = null)
        {
            DatabaseIntegrityChangeSigner signer = new(_protector, _descriptors, NullDatabaseIntegrityFailureReporter.Instance);
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql(TestDataSource)
                .AddInterceptors(new DatabaseIntegritySaveChangesInterceptor(signer));
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }
            return new(options.Options);
        }

        private async Task<bool> CreateLinkAsync(CottonDbContext database, bool folder, string name)
        {
            PublicShareTokenGenerator generator = new(database);
            if (folder)
            {
                CreateNodeShareLinkRequestHandler handler = new(database, generator);
                CreateNodeShareLinkResult result = await handler.Handle(new(_ownerId, _otherFolderId, 60, name), CancellationToken.None);
                Assert.That(result.Status, Is.AnyOf(CreateNodeShareLinkStatus.Created, CreateNodeShareLinkStatus.TokenConflict));
                return result.Status == CreateNodeShareLinkStatus.Created;
            }
            CreateFileShareLinkRequestHandler fileHandler = new(database, generator);
            CreateFileShareLinkResult fileResult = await fileHandler.Handle(new(_ownerId, _otherFileId, 60, name, false), CancellationToken.None);
            Assert.That(fileResult.Status, Is.AnyOf(CreateFileShareLinkStatus.Created, CreateFileShareLinkStatus.TokenConflict));
            return fileResult.Status == CreateFileShareLinkStatus.Created;
        }

        private async Task<Guid> AddHistoryAsync(bool folder, string name, DateTime? expiresAt, Guid? fileId = null)
        {
            await using CottonDbContext database = CreateContext();
            if (folder)
            {
                NodeShareToken token = new() { Token = name, Name = "original", NodeId = _folderId, CreatedByUserId = _ownerId, ExpiresAt = expiresAt };
                database.NodeShareTokens.Add(token);
                await database.SaveChangesAsync();
                return token.Id;
            }
            DownloadToken fileToken = new() { Token = name, FileName = "original.txt", NodeFileId = fileId ?? _fileId, CreatedByUserId = _ownerId, ExpiresAt = expiresAt };
            database.DownloadTokens.Add(fileToken);
            await database.SaveChangesAsync();
            return fileToken.Id;
        }

        private async Task<(byte[] Payload, byte[] Mac)> ReadSignatureAsync(bool folder, Guid id)
        {
            await using CottonDbContext database = CreateContext();
            object token;
            IDatabaseIntegrityDescriptor descriptor;
            if (folder)
            {
                token = await database.NodeShareTokens.SingleAsync(x => x.Id == id);
                descriptor = new NodeShareTokenIntegrityDescriptor();
            }
            else
            {
                token = await database.DownloadTokens.SingleAsync(x => x.Id == id);
                descriptor = new DownloadTokenIntegrityDescriptor();
            }
            byte[] mac = (byte[])database.Entry(token).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!;
            Assert.That(_protector.Verify(token, descriptor, mac), Is.True);
            return (descriptor.BuildCanonicalPayload(token), mac);
        }

        private static async Task AssertOneActiveLinkAsync(CottonDbContext database, string name)
        {
            DateTime now = DateTime.UtcNow;
            int files = await database.DownloadTokens.CountAsync(x => x.Token == name && (x.ExpiresAt == null || x.ExpiresAt > now));
            int folders = await database.NodeShareTokens.CountAsync(x => x.Token == name && (x.ExpiresAt == null || x.ExpiresAt > now));
            Assert.That(files + folders, Is.EqualTo(1));
        }
    }
}
