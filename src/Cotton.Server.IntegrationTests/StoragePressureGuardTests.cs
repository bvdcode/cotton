// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Models.Configuration;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

[NonParallelizable]
public class StoragePressureGuardTests : IntegrationTestBase
{
    [SetUp]
    public void SetUp()
    {
        var creator = DbContext.GetService<IRelationalDatabaseCreator>();
        creator.EnsureDeleted();
        DbContext.Database.Migrate();
    }

    [TearDown]
    public void TearDown()
    {
        DbContext.GetService<IRelationalDatabaseCreator>().EnsureDeleted();
    }

    [Test]
    public void EnsureCanAcceptWriteAsync_WhenBackendDoesNotReportCapacity_DoesNotBlock()
    {
        var notifications = new RecordingNotificationsProvider();
        var guard = CreateGuard(
            new NonReportingBackend(),
            notifications,
            new StoragePressureOptions
            {
                MinFreePercent = 100,
                MinFreeBytes = long.MaxValue,
            });

        Assert.DoesNotThrowAsync(async () => await guard.EnsureCanAcceptWriteAsync(long.MaxValue));
        Assert.That(notifications.Sent, Is.Empty);
    }

    [Test]
    public async Task EnsureCanAcceptWriteAsync_WhenFilesystemReserveWouldBeCrossed_ThrowsAndNotifiesAdmins()
    {
        var admin = CreateUser("adminuser", UserRole.Admin);
        DbContext.Users.Add(admin);
        await DbContext.SaveChangesAsync();

        var notifications = new RecordingNotificationsProvider();
        var backend = new ReportingBackend(new StorageCapacitySnapshot(
            Backend: "filesystem",
            RootPath: "/storage",
            TotalBytes: 1_000,
            AvailableBytes: 100));
        var guard = CreateGuard(
            backend,
            notifications,
            new StoragePressureOptions
            {
                MinFreePercent = 0,
                MinFreeBytes = 100,
            });

        var ex = Assert.ThrowsAsync<StoragePressureException>(
            async () => await guard.EnsureCanAcceptWriteAsync(1));

        Assert.That(ex, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(notifications.Sent, Has.Count.EqualTo(1));
            Assert.That(notifications.Sent[0].UserId, Is.EqualTo(admin.Id));
            Assert.That(notifications.Sent[0].Priority, Is.EqualTo(NotificationPriority.High));
            Assert.That(notifications.Sent[0].Metadata["kind"], Is.EqualTo("storage-pressure"));
            Assert.That(ex!.Pressure.RequiredFreeBytes, Is.EqualTo(100));
        });
    }

    [Test]
    public async Task EnsureCanAcceptWriteAsync_CachesCapacitySnapshotForHotPath()
    {
        var notifications = new RecordingNotificationsProvider();
        var backend = new ReportingBackend(new StorageCapacitySnapshot(
            Backend: "filesystem",
            RootPath: "/storage",
            TotalBytes: 1_000,
            AvailableBytes: 900));
        var guard = CreateGuard(
            backend,
            notifications,
            new StoragePressureOptions
            {
                MinFreePercent = 5,
                MinFreeBytes = 0,
                CheckIntervalSeconds = 60,
            });

        await guard.EnsureCanAcceptWriteAsync(1);
        await guard.EnsureCanAcceptWriteAsync(1);

        Assert.That(backend.SnapshotReads, Is.EqualTo(1));
        Assert.That(notifications.Sent, Is.Empty);
    }

    private StoragePressureGuard CreateGuard(
        IStorageBackend backend,
        RecordingNotificationsProvider notifications,
        StoragePressureOptions options)
    {
        return new StoragePressureGuard(
            new StaticStorageBackendProvider(backend),
            DbContext,
            notifications,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(options),
            NullLogger<StoragePressureGuard>.Instance);
    }

    private static User CreateUser(string username, UserRole role)
    {
        return new User
        {
            Username = username,
            PasswordPhc = "test",
            WebDavTokenPhc = "test",
            Role = role,
        };
    }

    private sealed class StaticStorageBackendProvider(IStorageBackend backend) : IStorageBackendProvider
    {
        public IStorageBackend GetBackend() => backend;
    }

    private sealed class ReportingBackend(StorageCapacitySnapshot snapshot) : StorageBackendStub, IStorageCapacityReporter
    {
        public int SnapshotReads { get; private set; }

        public StorageCapacitySnapshot GetCapacitySnapshot()
        {
            SnapshotReads++;
            return snapshot;
        }
    }

    private sealed class NonReportingBackend : StorageBackendStub;

    private abstract class StorageBackendStub : IStorageBackend
    {
        public void CleanupTempFiles(TimeSpan ttl) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(string uid) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(string uid) => throw new NotImplementedException();
        public Task<long> GetSizeAsync(string uid) => throw new NotImplementedException();
        public Task<Stream> ReadAsync(string uid) => throw new NotImplementedException();
        public Task WriteAsync(string uid, Stream stream) => throw new NotImplementedException();
        public IAsyncEnumerable<string> ListAllKeysAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class RecordingNotificationsProvider : INotificationsProvider
    {
        public List<SentNotification> Sent { get; } = [];

        public Task<bool> SendEmailAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl)
        {
            return Task.FromResult(false);
        }

        public Task SendSmtpTestEmailAsync(Guid userId, string serverBaseUrl)
        {
            return Task.CompletedTask;
        }

        public Task SendNotificationAsync(
            Guid userId,
            string title,
            string? content = null,
            NotificationPriority priority = NotificationPriority.None,
            Dictionary<string, string>? metadata = null)
        {
            Sent.Add(new SentNotification(
                userId,
                title,
                content,
                priority,
                metadata ?? []));
            return Task.CompletedTask;
        }
    }

    private sealed record SentNotification(
        Guid UserId,
        string Title,
        string? Content,
        NotificationPriority Priority,
        Dictionary<string, string> Metadata);
}
