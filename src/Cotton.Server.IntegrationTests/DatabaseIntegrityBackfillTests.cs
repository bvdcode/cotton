// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
            NullLogger<DatabaseIntegrityVerifier>.Instance);

        Assert.Throws<DatabaseIntegrityException>(() =>
            verifier.RequireValid(DbContext, tampered, "test.direct-tamper"));
    }

    private byte[]? ReadMac(User user)
    {
        return (byte[]?)DbContext.Entry(user)
            .Property(DatabaseIntegrityColumns.MacProperty)
            .CurrentValue;
    }

    private DatabaseIntegrityBridgeBackfillService CreateBackfillService()
    {
        return new DatabaseIntegrityBridgeBackfillService(
            DbContext,
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
            new CottonServerSettingsIntegrityDescriptor()
        ];
    }
}
