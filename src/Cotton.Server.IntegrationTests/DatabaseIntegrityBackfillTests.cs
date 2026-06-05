// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public sealed class DatabaseIntegrityBackfillTests : IntegrationTestBase
{
    [SetUp]
    public async Task SetUp()
    {
        await DbContext.Database.EnsureDeletedAsync();
        await DbContext.Database.MigrateAsync();
    }

    [Test]
    public async Task BridgeBackfill_SignsExistingUnsignedPhaseOneRows()
    {
        var user = new User
        {
            Username = "alice",
            PasswordPhc = "password-phc",
            WebDavTokenPhc = "webdav-phc",
            Role = UserRole.User,
            Email = "alice@example.test",
            IsEmailVerified = true
        };
        await DbContext.Users.AddAsync(user);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        User unsignedUser = await DbContext.Users.SingleAsync();
        Assert.That(ReadMac(unsignedUser), Is.Null);
        DbContext.ChangeTracker.Clear();

        var service = CreateBackfillService();
        int signed = await service.BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        User signedUser = await DbContext.Users.SingleAsync();
        byte[]? mac = ReadMac(signedUser);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signed, Is.EqualTo(1));
            Assert.That(mac, Has.Length.EqualTo(32));
            Assert.That(CreateProtector().Verify(signedUser, new UserIntegrityDescriptor(), mac!), Is.True);
        }
    }

    [Test]
    public async Task BridgeBackfill_SignsExistingUnsignedRows_WithSigningDbContext()
    {
        var user = new User
        {
            Username = "frank",
            PasswordPhc = "password-phc",
            WebDavTokenPhc = "webdav-phc",
            Role = UserRole.User
        };
        await DbContext.Users.AddAsync(user);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await using CottonDbContext signingDbContext = CreateSigningDbContext();
        int signed = await CreateBackfillService(signingDbContext)
            .BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        User signedUser = await DbContext.Users.SingleAsync(x => x.Id == user.Id);
        byte[]? mac = ReadMac(signedUser);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signed, Is.EqualTo(1));
            Assert.That(mac, Has.Length.EqualTo(32));
            Assert.That(CreateProtector().Verify(signedUser, new UserIntegrityDescriptor(), mac!), Is.True);
        }
    }

    [Test]
    public async Task Verifier_AllowsUnsignedLegacyRowsDuringRollout()
    {
        var user = new User
        {
            Username = "legacy",
            PasswordPhc = "password-phc",
            WebDavTokenPhc = "webdav-phc",
            Role = UserRole.User
        };
        await DbContext.Users.AddAsync(user);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        User unsignedUser = await DbContext.Users.SingleAsync(x => x.Id == user.Id);
        var verifier = new DatabaseIntegrityVerifier(
            CreateProtector(),
            new DatabaseIntegrityDescriptorRegistry(CreateDescriptors()),
            NullDatabaseIntegrityFailureReporter.Instance,
            NullLogger<DatabaseIntegrityVerifier>.Instance);

        Assert.DoesNotThrow(() => verifier.RequireValid(DbContext, unsignedUser, "test.legacy-row"));
    }

    [Test]
    public async Task Verifier_AllowsSignedRowsWithMismatchedMacDuringBridgeRepair()
    {
        var user = new User
        {
            Username = "bob",
            PasswordPhc = "password-phc",
            WebDavTokenPhc = "webdav-phc",
            Role = UserRole.User
        };
        await DbContext.Users.AddAsync(user);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);
        await DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE users SET role = {(int)UserRole.Admin} WHERE id = {user.Id}");
        DbContext.ChangeTracker.Clear();

        User tampered = await DbContext.Users.SingleAsync(x => x.Id == user.Id);
        var verifier = new DatabaseIntegrityVerifier(
            CreateProtector(),
            new DatabaseIntegrityDescriptorRegistry(CreateDescriptors()),
            NullDatabaseIntegrityFailureReporter.Instance,
            NullLogger<DatabaseIntegrityVerifier>.Instance);

        Assert.DoesNotThrow(() =>
            verifier.RequireValid(DbContext, tampered, "test.direct-tamper"));
    }

    [Test]
    public async Task Verifier_AllowsFileManifestMismatchedMacDuringBridgeRecovery()
    {
        FileManifest manifest = await AddUnsignedManifestAsync(
            previewGenerationError: "codec failed before upgrade",
            previewGeneratorVersion: 7);
        var descriptor = new FileManifestIntegrityDescriptor();
        byte[] mac = CreateProtector().Sign(manifest, descriptor);
        await WriteIntegrityMetadataAsync(manifest, descriptor.SchemaVersion, mac);
        await DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE file_manifests SET size_bytes = 999 WHERE id = {manifest.Id}");
        DbContext.ChangeTracker.Clear();

        FileManifest stale = await DbContext.FileManifests.SingleAsync(x => x.Id == manifest.Id);
        var verifier = new DatabaseIntegrityVerifier(
            CreateProtector(),
            new DatabaseIntegrityDescriptorRegistry(CreateDescriptors()),
            NullDatabaseIntegrityFailureReporter.Instance,
            NullLogger<DatabaseIntegrityVerifier>.Instance);

        Assert.DoesNotThrow(() =>
            verifier.RequireValid(DbContext, stale, "test.file-manifest-recovery"));
    }

    [Test]
    public async Task BridgeBackfill_SignsExistingChunkRows()
    {
        var chunk = new Chunk
        {
            Hash = [1, 2, 3],
            PlainSizeBytes = 10,
            StoredSizeBytes = 12,
            CompressionAlgorithm = CompressionAlgorithm.Zstd
        };
        await DbContext.Chunks.AddAsync(chunk);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        int signed = await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        Chunk signedChunk = await DbContext.Chunks.SingleAsync();
        byte[]? mac = ReadMac(signedChunk);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signed, Is.EqualTo(1));
            Assert.That(mac, Has.Length.EqualTo(32));
            Assert.That(CreateProtector().Verify(signedChunk, new ChunkIntegrityDescriptor(), mac!), Is.True);
        }
    }

    [Test]
    public async Task BridgeBackfill_SignsUnsignedRowsAfterIntegrityMetadataExists()
    {
        var user = new User
        {
            Username = "dora",
            PasswordPhc = "password-phc",
            WebDavTokenPhc = "webdav-phc",
            Role = UserRole.User
        };
        await DbContext.Users.AddAsync(user);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);

        await DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE users SET integrity_mac = NULL WHERE id = {user.Id}");
        DbContext.ChangeTracker.Clear();

        int signed = await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        User signedUser = await DbContext.Users.SingleAsync(x => x.Id == user.Id);
        byte[]? mac = ReadMac(signedUser);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signed, Is.EqualTo(1));
            Assert.That(mac, Has.Length.EqualTo(32));
        }
    }

    [Test]
    public async Task SaveChanges_ReSignsTamperedProtectedRowDuringBridgeRepair()
    {
        var user = new User
        {
            Username = "erin",
            PasswordPhc = "password-phc",
            WebDavTokenPhc = "webdav-phc",
            Role = UserRole.User
        };
        await DbContext.Users.AddAsync(user);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);
        await DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE users SET role = {(int)UserRole.Admin} WHERE id = {user.Id}");
        DbContext.ChangeTracker.Clear();

        var reporter = new CapturingDatabaseIntegrityFailureReporter();
        await using CottonDbContext signingDbContext = CreateSigningDbContext(reporter);
        User tampered = await signingDbContext.Users.SingleAsync(x => x.Id == user.Id);
        tampered.FirstName = "Legitimate edit";

        Assert.DoesNotThrowAsync(async () =>
            await signingDbContext.SaveChangesAsync(CancellationToken.None));

        DbContext.ChangeTracker.Clear();
        User resigned = await DbContext.Users.SingleAsync(x => x.Id == user.Id);
        byte[]? mac = ReadMac(resigned);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.Failures, Is.Empty);
            Assert.That(resigned.Role, Is.EqualTo(UserRole.Admin));
            Assert.That(resigned.FirstName, Is.EqualTo("Legitimate edit"));
            Assert.That(mac, Has.Length.EqualTo(32));
            Assert.That(CreateProtector().Verify(resigned, new UserIntegrityDescriptor(), mac!), Is.True);
        }
    }

    [Test]
    public async Task BridgeBackfill_ReSignsFileManifestWithStalePreviewMac()
    {
        FileManifest manifest = await AddUnsignedManifestAsync(
            previewGenerationError: "codec failed before upgrade",
            previewGeneratorVersion: 7);
        var descriptor = new FileManifestIntegrityDescriptor();
        byte[]? encryptedPreviewHash = manifest.SmallFilePreviewHashEncrypted;
        byte[]? smallPreviewHash = manifest.SmallFilePreviewHash;
        byte[]? largePreviewHash = manifest.LargeFilePreviewHash;
        manifest.SmallFilePreviewHashEncrypted = null;
        manifest.SmallFilePreviewHash = null;
        manifest.LargeFilePreviewHash = null;
        byte[] staleMac = CreateProtector().Sign(manifest, descriptor);
        manifest.SmallFilePreviewHashEncrypted = encryptedPreviewHash;
        manifest.SmallFilePreviewHash = smallPreviewHash;
        manifest.LargeFilePreviewHash = largePreviewHash;
        await WriteIntegrityMetadataAsync(manifest, descriptor.SchemaVersion, staleMac);
        DbContext.ChangeTracker.Clear();

        FileManifest stale = await DbContext.FileManifests.SingleAsync(x => x.Id == manifest.Id);
        Assert.That(CreateProtector().Verify(stale, descriptor, ReadMac(stale)!), Is.False);
        DbContext.ChangeTracker.Clear();

        int signed = await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        FileManifest upgraded = await DbContext.FileManifests.SingleAsync(x => x.Id == manifest.Id);
        byte[]? mac = ReadMac(upgraded);
        int? version = ReadVersion(upgraded);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signed, Is.GreaterThanOrEqualTo(1));
            Assert.That(version, Is.EqualTo(descriptor.SchemaVersion));
            Assert.That(mac, Has.Length.EqualTo(32));
            Assert.That(upgraded.SmallFilePreviewHashEncrypted, Is.Not.Null);
            Assert.That(CreateProtector().Verify(upgraded, descriptor, mac!), Is.True);
        }
    }

    [Test]
    public async Task BridgeBackfill_ReSignsFileManifestWithStaleSchemaVersion()
    {
        FileManifest manifest = await AddUnsignedManifestAsync(
            previewGenerationError: null,
            previewGeneratorVersion: 8);
        var descriptor = new FileManifestIntegrityDescriptor();
        byte[] mac = CreateProtector().Sign(manifest, descriptor);
        await WriteIntegrityMetadataAsync(manifest, 999, mac);
        DbContext.ChangeTracker.Clear();

        int signed = await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        FileManifest upgraded = await DbContext.FileManifests.SingleAsync(x => x.Id == manifest.Id);
        byte[]? upgradedMac = ReadMac(upgraded);
        int? version = ReadVersion(upgraded);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signed, Is.GreaterThanOrEqualTo(1));
            Assert.That(version, Is.EqualTo(descriptor.SchemaVersion));
            Assert.That(upgradedMac, Has.Length.EqualTo(32));
            Assert.That(CreateProtector().Verify(upgraded, descriptor, upgradedMac!), Is.True);
        }
    }

    [Test]
    public async Task BridgeBackfill_ReSignsTamperedFileManifestCurrentState()
    {
        FileManifest manifest = await AddUnsignedManifestAsync(
            previewGenerationError: "old error",
            previewGeneratorVersion: 7);
        var descriptor = new FileManifestIntegrityDescriptor();
        byte[] mac = CreateProtector().Sign(manifest, descriptor);
        await WriteIntegrityMetadataAsync(manifest, descriptor.SchemaVersion, mac);
        await DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE file_manifests SET size_bytes = 999 WHERE id = {manifest.Id}");
        DbContext.ChangeTracker.Clear();

        int signed = await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        FileManifest resigned = await DbContext.FileManifests.SingleAsync(x => x.Id == manifest.Id);
        byte[]? resignedMac = ReadMac(resigned);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signed, Is.GreaterThanOrEqualTo(1));
            Assert.That(resigned.SizeBytes, Is.EqualTo(999));
            Assert.That(CreateProtector().Verify(resigned, descriptor, resignedMac!), Is.True);
        }
    }

    [Test]
    public async Task BridgeBackfill_ReSignsUnsupportedStaleSchemaVersion()
    {
        var user = new User
        {
            Username = "unsupported-stale",
            PasswordPhc = "password-phc",
            WebDavTokenPhc = "webdav-phc",
            Role = UserRole.User
        };
        await DbContext.Users.AddAsync(user);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);
        await DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE users SET integrity_version = 999 WHERE id = {user.Id}");
        DbContext.ChangeTracker.Clear();

        int signed = await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        User resigned = await DbContext.Users.SingleAsync(x => x.Id == user.Id);
        byte[]? mac = ReadMac(resigned);
        var descriptor = new UserIntegrityDescriptor();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(signed, Is.GreaterThanOrEqualTo(1));
            Assert.That(ReadVersion(resigned), Is.EqualTo(descriptor.SchemaVersion));
            Assert.That(CreateProtector().Verify(resigned, descriptor, mac!), Is.True);
        }
    }

    private byte[]? ReadMac(object entity)
    {
        return (byte[]?)DbContext.Entry(entity)
            .Property(DatabaseIntegrityColumns.MacProperty)
            .CurrentValue;
    }

    private int? ReadVersion(object entity)
    {
        return (int?)DbContext.Entry(entity)
            .Property(DatabaseIntegrityColumns.VersionProperty)
            .CurrentValue;
    }

    private async Task<FileManifest> AddUnsignedManifestAsync(
        string? previewGenerationError,
        int previewGeneratorVersion)
    {
        var manifest = new FileManifest
        {
            ProposedContentHash = [1, 2, 3],
            ComputedContentHash = [1, 2, 3],
            ContentType = "image/heic",
            SizeBytes = 3,
            SmallFilePreviewHashEncrypted = [4, 5, 6],
            SmallFilePreviewHash = [7, 8, 9],
            LargeFilePreviewHash = [10, 11, 12],
            PreviewGenerationError = previewGenerationError,
            PreviewGeneratorVersion = previewGeneratorVersion
        };

        await DbContext.FileManifests.AddAsync(manifest);
        await DbContext.SaveChangesAsync();
        return manifest;
    }

    private async Task WriteIntegrityMetadataAsync(object entity, int version, byte[] mac)
    {
        DbContext.Entry(entity)
            .Property(DatabaseIntegrityColumns.VersionProperty)
            .CurrentValue = version;
        DbContext.Entry(entity)
            .Property(DatabaseIntegrityColumns.MacProperty)
            .CurrentValue = mac;
        await DbContext.SaveChangesAsync();
    }

    private CottonDbContext CreateSigningDbContext(IDatabaseIntegrityFailureReporter? reporter = null)
    {
        DbContextOptionsBuilder<CottonDbContext> optionsBuilder = new();
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = TestPostgresHost,
            Port = TestPostgresPort,
            Username = TestPostgresUsername,
            Password = TestPostgresPassword,
            Database = CurrentDatabaseName
        };
        optionsBuilder.UseNpgsql(builder.ConnectionString, x => x.UseAdminDatabase("postgres"));

        return new CottonDbContext(
            optionsBuilder.Options,
            integrityChangeSigner: new DatabaseIntegrityChangeSigner(
                CreateProtector(),
                new DatabaseIntegrityDescriptorRegistry(CreateDescriptors()),
                reporter));
    }

    private sealed class CapturingDatabaseIntegrityFailureReporter : IDatabaseIntegrityFailureReporter
    {
        public List<DatabaseIntegrityFailure> Failures { get; } = [];

        public void Report(DatabaseIntegrityFailure failure)
        {
            Failures.Add(failure);
        }
    }

    private DatabaseIntegrityBridgeBackfillService CreateBackfillService(CottonDbContext? dbContext = null)
    {
        return new DatabaseIntegrityBridgeBackfillService(
            dbContext ?? DbContext,
            CreateProtector(),
            new DatabaseIntegrityDescriptorRegistry(CreateDescriptors()),
            NullLogger<DatabaseIntegrityBridgeBackfillService>.Instance);
    }

    private static DatabaseIntegrityProtector CreateProtector()
    {
        var settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        return new DatabaseIntegrityProtector(new DatabaseIntegrityKeyProvider(settings));
    }

    private static IDatabaseIntegrityDescriptor[] CreateDescriptors()
    {
        return
        [
            new UserIntegrityDescriptor(),
            new UserPasskeyCredentialIntegrityDescriptor(),
            new ExtendedRefreshTokenIntegrityDescriptor(),
            new DownloadTokenIntegrityDescriptor(),
            new NodeShareTokenIntegrityDescriptor(),
            new CottonServerSettingsIntegrityDescriptor(),
            new NodeIntegrityDescriptor(),
            new NodeFileIntegrityDescriptor(),
            new FileManifestIntegrityDescriptor(),
            new FileManifestChunkIntegrityDescriptor(),
            new ChunkIntegrityDescriptor()
        ];
    }

}
