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

        await using CottonDbContext signingDbContext = CreateSigningDbContext();
        User tampered = await signingDbContext.Users.SingleAsync(x => x.Id == user.Id);
        tampered.FirstName = "Legitimate edit";

        Assert.ThrowsAsync<DatabaseIntegrityException>(async () =>
            await signingDbContext.SaveChangesAsync(CancellationToken.None));
    }

    private byte[]? ReadMac(User user)
    {
        return (byte[]?)DbContext.Entry(user)
            .Property(DatabaseIntegrityColumns.MacProperty)
            .CurrentValue;
    }

    private CottonDbContext CreateSigningDbContext()
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
                new DatabaseIntegrityDescriptorRegistry(CreateDescriptors())));
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
