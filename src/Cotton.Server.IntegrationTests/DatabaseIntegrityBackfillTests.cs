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
    public async Task Verifier_RejectsDirectDatabaseTampering()
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

        Assert.Throws<DatabaseIntegrityException>(() =>
            verifier.RequireValid(DbContext, tampered, "test.direct-tamper"));
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
    public async Task SaveChanges_RefusesToResignTamperedProtectedRow()
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

        Assert.ThrowsAsync<DatabaseIntegrityException>(async () =>
            await signingDbContext.SaveChangesAsync(CancellationToken.None));

        Assert.That(reporter.Failures, Has.Count.EqualTo(1));
        DatabaseIntegrityFailure failure = reporter.Failures[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(failure.EntityName, Is.EqualTo("users"));
            Assert.That(failure.EntityKey, Is.EqualTo(user.Id.ToString("D")));
            Assert.That(failure.Boundary, Is.EqualTo("save.original-state"));
        }
    }

    [Test]
    public async Task BridgeBackfill_UpgradesFileManifestLegacyV1_WithOperationalPreviewState()
    {
        FileManifest manifest = await AddUnsignedManifestAsync(
            previewGenerationError: "codec failed before upgrade",
            previewGeneratorVersion: 7);
        var legacyDescriptor = new LegacyFileManifestV1WithOperationalPreviewStateDescriptor();
        byte[] legacyMac = CreateProtector().Sign(manifest, legacyDescriptor);
        await WriteIntegrityMetadataAsync(manifest, legacyDescriptor.SchemaVersion, legacyMac);
        DbContext.ChangeTracker.Clear();

        int signed = await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        FileManifest upgraded = await DbContext.FileManifests.SingleAsync(x => x.Id == manifest.Id);
        byte[]? mac = ReadMac(upgraded);
        int? version = ReadVersion(upgraded);
        var currentDescriptor = new FileManifestIntegrityDescriptor();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signed, Is.EqualTo(1));
            Assert.That(version, Is.EqualTo(currentDescriptor.SchemaVersion));
            Assert.That(mac, Has.Length.EqualTo(32));
            Assert.That(CreateProtector().Verify(upgraded, currentDescriptor, mac!), Is.True);
        }
    }

    [Test]
    public async Task BridgeBackfill_UpgradesFileManifestLegacyV1_WithoutOperationalPreviewState()
    {
        FileManifest manifest = await AddUnsignedManifestAsync(
            previewGenerationError: null,
            previewGeneratorVersion: 8);
        var legacyDescriptor = new LegacyFileManifestV1WithoutOperationalPreviewStateDescriptor();
        byte[] legacyMac = CreateProtector().Sign(manifest, legacyDescriptor);
        await WriteIntegrityMetadataAsync(manifest, legacyDescriptor.SchemaVersion, legacyMac);
        DbContext.ChangeTracker.Clear();

        int signed = await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        FileManifest upgraded = await DbContext.FileManifests.SingleAsync(x => x.Id == manifest.Id);
        byte[]? mac = ReadMac(upgraded);
        int? version = ReadVersion(upgraded);
        var currentDescriptor = new FileManifestIntegrityDescriptor();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signed, Is.EqualTo(1));
            Assert.That(version, Is.EqualTo(currentDescriptor.SchemaVersion));
            Assert.That(mac, Has.Length.EqualTo(32));
            Assert.That(CreateProtector().Verify(upgraded, currentDescriptor, mac!), Is.True);
        }
    }

    [Test]
    public async Task BridgeBackfill_RejectsTamperedFileManifestLegacyV1()
    {
        FileManifest manifest = await AddUnsignedManifestAsync(
            previewGenerationError: "old error",
            previewGeneratorVersion: 7);
        var legacyDescriptor = new LegacyFileManifestV1WithOperationalPreviewStateDescriptor();
        byte[] legacyMac = CreateProtector().Sign(manifest, legacyDescriptor);
        await WriteIntegrityMetadataAsync(manifest, legacyDescriptor.SchemaVersion, legacyMac);
        await DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE file_manifests SET size_bytes = 999 WHERE id = {manifest.Id}");
        DbContext.ChangeTracker.Clear();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateBackfillService().BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None));
    }

    [Test]
    public async Task BridgeBackfill_RejectsUnsupportedStaleSchemaVersion()
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

        var reporter = new CapturingDatabaseIntegrityFailureReporter();
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateBackfillService(reporter: reporter).BackfillUnsignedPhaseOneRowsAsync(CancellationToken.None));

        Assert.That(reporter.Failures, Has.Count.EqualTo(1));
        DatabaseIntegrityFailure failure = reporter.Failures[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(failure.EntityName, Is.EqualTo("users"));
            Assert.That(failure.EntityKey, Is.EqualTo(user.Id.ToString("D")));
            Assert.That(failure.Boundary, Is.EqualTo("bridge.legacy-upgrade"));
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

    private DatabaseIntegrityBridgeBackfillService CreateBackfillService(
        CottonDbContext? dbContext = null,
        IDatabaseIntegrityFailureReporter? reporter = null)
    {
        return new DatabaseIntegrityBridgeBackfillService(
            dbContext ?? DbContext,
            CreateProtector(),
            new DatabaseIntegrityDescriptorRegistry(CreateDescriptors()),
            NullLogger<DatabaseIntegrityBridgeBackfillService>.Instance,
            reporter);
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

    private static void WriteFileManifestContentIdentityFields(
        DatabaseIntegrityCanonicalWriter writer,
        FileManifest entity)
    {
        writer.WriteGuidField(nameof(entity.Id), entity.Id);
        writer.WriteBytesField(nameof(entity.ComputedContentHash), entity.ComputedContentHash);
        writer.WriteBytesField(nameof(entity.ProposedContentHash), entity.ProposedContentHash);
        writer.WriteStringField(nameof(entity.ContentType), entity.ContentType);
        writer.WriteInt64Field(nameof(entity.SizeBytes), entity.SizeBytes);
        writer.WriteBytesField(nameof(entity.SmallFilePreviewHashEncrypted), entity.SmallFilePreviewHashEncrypted);
        writer.WriteBytesField(nameof(entity.SmallFilePreviewHash), entity.SmallFilePreviewHash);
        writer.WriteBytesField(nameof(entity.LargeFilePreviewHash), entity.LargeFilePreviewHash);
    }

    private sealed class LegacyFileManifestV1WithoutOperationalPreviewStateDescriptor :
        DatabaseIntegrityDescriptor<FileManifest>
    {
        public override string EntityName => "file_manifests";
        public override int SchemaVersion => 1;

        public override string GetEntityKey(FileManifest entity)
        {
            return entity.Id.ToString("D");
        }

        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, FileManifest entity)
        {
            WriteFileManifestContentIdentityFields(writer, entity);
        }
    }

    private sealed class LegacyFileManifestV1WithOperationalPreviewStateDescriptor :
        DatabaseIntegrityDescriptor<FileManifest>
    {
        public override string EntityName => "file_manifests";
        public override int SchemaVersion => 1;

        public override string GetEntityKey(FileManifest entity)
        {
            return entity.Id.ToString("D");
        }

        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, FileManifest entity)
        {
            WriteFileManifestContentIdentityFields(writer, entity);
            writer.WriteStringField(nameof(entity.PreviewGenerationError), entity.PreviewGenerationError);
            writer.WriteInt32Field(nameof(entity.PreviewGeneratorVersion), entity.PreviewGeneratorVersion);
        }
    }
}
