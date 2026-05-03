using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Jobs;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Storage.Processors;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Reflection;
using System.Text;

namespace Cotton.Server.IntegrationTests;

public class GarbageCollectorJobTests : IntegrationTestBase
{
    [SetUp]
    public void SetUp()
    {
        ResetSettingsProviderCaches();

        var creator = DbContext.GetService<IRelationalDatabaseCreator>();
        creator.EnsureDeleted();
        DbContext.Database.Migrate();
    }

    [Test]
    public async Task RunOnce_DeletesOnlyDueUnreferencedChunks_AndClearsLiveSchedules()
    {
        DateTime now = DateTime.UtcNow;
        var storage = new InMemoryStorage();
        var keyProvider = new DatabaseBackupKeyProvider(new CottonEncryptionSettings
        {
            MasterEncryptionKey = "test-master-key"
        });

        byte[] dueOrphanHash = Hash("due-orphan");
        byte[] futureOrphanHash = Hash("future-orphan");
        byte[] unscheduledOrphanHash = Hash("unscheduled-orphan");
        byte[] fileHash = Hash("file");
        byte[] previewHash = Hash("preview");
        byte[] avatarHash = Hash("avatar");
        byte[] backupHash = Hash("backup");

        await WriteStorageObjectsAsync(
            storage,
            dueOrphanHash,
            futureOrphanHash,
            unscheduledOrphanHash,
            fileHash,
            previewHash,
            avatarHash,
            backupHash);
        await storage.WriteAsync(keyProvider.GetScopedPointerStorageKey(), new MemoryStream([1, 2, 3]));

        User user = CreateUser(avatarHash);
        DbContext.Users.Add(user);
        await DbContext.SaveChangesAsync();

        Layout layout = new()
        {
            OwnerId = user.Id,
            IsActive = true,
        };
        Node root = new()
        {
            OwnerId = user.Id,
            Layout = layout,
            Type = NodeType.Default,
        };
        root.SetName("root");

        FileManifest manifest = new()
        {
            ProposedContentHash = Hash("manifest"),
            ContentType = "text/plain",
            SizeBytes = 4,
            SmallFilePreviewHash = previewHash,
            SmallFilePreviewHashEncrypted = Hash("preview-encrypted"),
        };

        NodeFile nodeFile = new()
        {
            OwnerId = user.Id,
            Node = root,
            FileManifest = manifest,
            OriginalNodeFileId = Guid.NewGuid(),
        };
        nodeFile.SetName("file.txt");

        DbContext.UserLayouts.Add(layout);
        DbContext.Nodes.Add(root);
        DbContext.Chunks.AddRange(
            CreateChunk(dueOrphanHash, now.AddDays(-1)),
            CreateChunk(futureOrphanHash, now.AddDays(1)),
            CreateChunk(unscheduledOrphanHash, null),
            CreateChunk(fileHash, now.AddDays(-1)),
            CreateChunk(previewHash, now.AddDays(-1)),
            CreateChunk(avatarHash, now.AddDays(-1)),
            CreateChunk(backupHash, now.AddDays(1)));
        DbContext.FileManifests.Add(manifest);
        DbContext.FileManifestChunks.Add(new FileManifestChunk
        {
            FileManifest = manifest,
            ChunkHash = fileHash,
            ChunkOrder = 0,
        });
        DbContext.NodeFiles.Add(nodeFile);
        DbContext.ChunkOwnerships.Add(new ChunkOwnership
        {
            OwnerId = user.Id,
            ChunkHash = dueOrphanHash,
        });
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var backup = CreateBackupManifest(Hasher.ToHexStringHash(backupHash));
        var usage = CreateChunkUsageService(DbContext, storage, keyProvider, backup);
        var job = new GarbageCollectorJob(
            new PerfTracker(),
            storage,
            DbContext,
            usage,
            new SettingsProvider(DbContext),
            NullLogger<GarbageCollectorJob>.Instance);

        await job.RunOnceAsync(now, 1000);
        DbContext.ChangeTracker.Clear();

        bool dueOrphanChunkExists = await DbContext.Chunks.AnyAsync(c => c.Hash == dueOrphanHash);
        bool dueOrphanStorageExists = await storage.ExistsAsync(Hasher.ToHexStringHash(dueOrphanHash));
        bool dueOrphanOwnershipExists = await DbContext.ChunkOwnerships.AnyAsync(o => o.ChunkHash == dueOrphanHash);
        Chunk futureOrphanChunk = (await DbContext.Chunks.FindAsync(futureOrphanHash))!;
        Chunk unscheduledOrphanChunk = (await DbContext.Chunks.FindAsync(unscheduledOrphanHash))!;
        Chunk fileChunk = (await DbContext.Chunks.FindAsync(fileHash))!;
        Chunk previewChunk = (await DbContext.Chunks.FindAsync(previewHash))!;
        Chunk avatarChunk = (await DbContext.Chunks.FindAsync(avatarHash))!;
        Chunk backupChunk = (await DbContext.Chunks.FindAsync(backupHash))!;

        Assert.Multiple(() =>
        {
            Assert.That(dueOrphanChunkExists, Is.False);
            Assert.That(dueOrphanStorageExists, Is.False);
            Assert.That(dueOrphanOwnershipExists, Is.False);
            Assert.That(futureOrphanChunk.GCScheduledAfter, Is.EqualTo(now.AddDays(1)).Within(TimeSpan.FromSeconds(1)));
            Assert.That(unscheduledOrphanChunk.GCScheduledAfter, Is.EqualTo(now.AddDays(7)).Within(TimeSpan.FromSeconds(1)));
            Assert.That(fileChunk.GCScheduledAfter, Is.Null);
            Assert.That(previewChunk.GCScheduledAfter, Is.Null);
            Assert.That(avatarChunk.GCScheduledAfter, Is.Null);
            Assert.That(backupChunk.GCScheduledAfter, Is.Null);
        });
    }

    [Test]
    public async Task StorageConsistency_DoesNotRegisterProtectedBackupStorageKeys()
    {
        var storage = new InMemoryStorage();
        var keyProvider = new DatabaseBackupKeyProvider(new CottonEncryptionSettings
        {
            MasterEncryptionKey = "test-master-key"
        });

        byte[] orphanHash = Hash("storage-orphan");
        byte[] backupHash = Hash("storage-backup");
        string manifestStorageKey = Hasher.ToHexStringHash(Hash("backup-manifest"));

        await WriteStorageObjectsAsync(storage, orphanHash, backupHash);
        await storage.WriteAsync(manifestStorageKey, new MemoryStream([4, 5, 6]));
        await storage.WriteAsync(keyProvider.GetScopedPointerStorageKey(), new MemoryStream([1, 2, 3]));

        var backup = CreateBackupManifest(Hasher.ToHexStringHash(backupHash), manifestStorageKey);
        var usage = CreateChunkUsageService(DbContext, storage, keyProvider, backup);
        var job = new StorageConsistencyJob(
            storage,
            DbContext,
            new NoopNotificationsProvider(),
            usage,
            NullLogger<StorageConsistencyJob>.Instance);

        await job.RunOnceAsync();
        DbContext.ChangeTracker.Clear();

        bool orphanRegistered = await DbContext.Chunks.AnyAsync(c => c.Hash == orphanHash && c.GCScheduledAfter != null);
        bool backupRegistered = await DbContext.Chunks.AnyAsync(c => c.Hash == backupHash);
        bool manifestRegistered = await DbContext.Chunks.AnyAsync(c => c.Hash == Hash("backup-manifest"));
        bool pointerRegistered = await DbContext.Chunks.AnyAsync(c => c.Hash == Hasher.FromHexStringHash(keyProvider.GetScopedPointerStorageKey()));

        Assert.Multiple(() =>
        {
            Assert.That(orphanRegistered, Is.True);
            Assert.That(backupRegistered, Is.False);
            Assert.That(manifestRegistered, Is.False);
            Assert.That(pointerRegistered, Is.False);
        });
    }

    [Test]
    public async Task StorageConsistency_ClearsMissingPreviewAndAvatarReferences()
    {
        var storage = new InMemoryStorage();
        var keyProvider = new DatabaseBackupKeyProvider(new CottonEncryptionSettings
        {
            MasterEncryptionKey = "test-master-key"
        });

        byte[] missingHash = Hash("missing-preview-avatar");
        User user = CreateUser(missingHash);
        FileManifest manifest = new()
        {
            ProposedContentHash = Hash("manifest-with-missing-preview"),
            ContentType = "image/png",
            SizeBytes = 10,
            SmallFilePreviewHash = missingHash,
            SmallFilePreviewHashEncrypted = Hash("encrypted-missing-preview"),
            LargeFilePreviewHash = missingHash,
        };

        DbContext.Users.Add(user);
        DbContext.FileManifests.Add(manifest);
        DbContext.Chunks.Add(CreateChunk(missingHash, null));
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var usage = CreateChunkUsageService(DbContext, storage, keyProvider, latestBackup: null);
        var job = new StorageConsistencyJob(
            storage,
            DbContext,
            new NoopNotificationsProvider(),
            usage,
            NullLogger<StorageConsistencyJob>.Instance);

        await job.RunOnceAsync();
        DbContext.ChangeTracker.Clear();

        User updatedUser = (await DbContext.Users.FindAsync(user.Id))!;
        FileManifest updatedManifest = (await DbContext.FileManifests.FindAsync(manifest.Id))!;

        Assert.Multiple(() =>
        {
            Assert.That(updatedUser.AvatarHash, Is.Null);
            Assert.That(updatedUser.AvatarHashEncrypted, Is.Null);
            Assert.That(updatedManifest.SmallFilePreviewHash, Is.Null);
            Assert.That(updatedManifest.SmallFilePreviewHashEncrypted, Is.Null);
            Assert.That(updatedManifest.LargeFilePreviewHash, Is.Null);
        });
    }

    private static ChunkUsageService CreateChunkUsageService(
        CottonDbContext dbContext,
        InMemoryStorage storage,
        DatabaseBackupKeyProvider keyProvider,
        ResolvedBackupManifest? latestBackup)
    {
        return new ChunkUsageService(
            dbContext,
            storage,
            new StaticBackupManifestService(latestBackup),
            keyProvider,
            NullLogger<ChunkUsageService>.Instance);
    }

    private static User CreateUser(byte[] avatarHash)
    {
        return new User
        {
            Username = "gcuser" + Guid.NewGuid().ToString("N")[..8],
            PasswordPhc = "phc",
            WebDavTokenPhc = "webdav",
            Role = UserRole.Admin,
            AvatarHash = avatarHash,
            AvatarHashEncrypted = Hash("avatar-encrypted"),
        };
    }

    private static Chunk CreateChunk(byte[] hash, DateTime? gcScheduledAfter)
    {
        return new Chunk
        {
            Hash = hash,
            PlainSizeBytes = 4,
            StoredSizeBytes = 4,
            CompressionAlgorithm = CompressionProcessor.Algorithm,
            GCScheduledAfter = gcScheduledAfter,
        };
    }

    private static async Task WriteStorageObjectsAsync(InMemoryStorage storage, params byte[][] hashes)
    {
        foreach (byte[] hash in hashes)
        {
            await storage.WriteAsync(Hasher.ToHexStringHash(hash), new MemoryStream([1, 2, 3, 4]));
        }
    }

    private static ResolvedBackupManifest CreateBackupManifest(string protectedChunkStorageKey, string? manifestStorageKey = null)
    {
        manifestStorageKey ??= Hasher.ToHexStringHash(Hash("backup-manifest-default"));
        var manifest = new BackupManifest(
            SchemaVersion: 1,
            BackupId: "test-backup",
            CreatedAtUtc: DateTime.UtcNow,
            Contains: "postgres_database_dump",
            DumpFormat: "pg_dump_custom",
            SourceDatabase: "cotton_test",
            SourceHost: "localhost",
            SourcePort: "5432",
            HashAlgorithm: Hasher.SupportedHashAlgorithm,
            ChunkSizeBytes: 4,
            DumpSizeBytes: 4,
            DumpContentHash: protectedChunkStorageKey,
            ChunkCount: 1,
            Elapsed: TimeSpan.FromSeconds(1),
            Chunks: [new BackupChunkInfo(0, protectedChunkStorageKey, 4)]);

        var pointer = new BackupManifestPointer(
            SchemaVersion: 1,
            LogicalKey: DatabaseBackupKeyProvider.ManifestPointerLogicalKey,
            UpdatedAtUtc: DateTime.UtcNow,
            LatestManifestStorageKey: manifestStorageKey,
            LatestBackupId: "test-backup");

        return new ResolvedBackupManifest(manifestStorageKey, pointer, manifest);
    }

    private static byte[] Hash(string value)
    {
        return Hasher.HashData(Encoding.UTF8.GetBytes(value));
    }

    private static void ResetSettingsProviderCaches()
    {
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        Type settingsProviderType = typeof(SettingsProvider);

        settingsProviderType.GetField("_cache", flags)?.SetValue(null, null);
        settingsProviderType.GetField("_isServerInitializedCache", flags)?.SetValue(null, null);
        settingsProviderType.GetField("_serverHasUsersCache", flags)?.SetValue(null, null);
    }

    private sealed class StaticBackupManifestService(ResolvedBackupManifest? _latestBackup) : IDatabaseBackupManifestService
    {
        public Task<ResolvedBackupManifest?> TryGetLatestManifestAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_latestBackup);
        }
    }

    private sealed class NoopNotificationsProvider : INotificationsProvider
    {
        public Task<bool> SendEmailAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl)
        {
            return Task.FromResult(true);
        }

        public Task SendNotificationAsync(
            Guid userId,
            string title,
            string? content = null,
            NotificationPriority priority = NotificationPriority.None,
            Dictionary<string, string>? metadata = null)
        {
            return Task.CompletedTask;
        }
    }
}
