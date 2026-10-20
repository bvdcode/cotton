// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Jobs;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    public class StorageConsistencyAvatarTests : IntegrationTestBase
    {
        [Test]
        public async Task RunOnce_ClearsMissingAvatarsThroughTrackingAndPreservesSignatures()
        {
            CottonEncryptionSettings settings = new()
            {
                MasterEncryptionKey = Convert.ToBase64String(SHA256.HashData("avatar-cleanup-contract"u8))
            };
            using DatabaseIntegrityKeyProvider integrityKeys = new(settings);
            DatabaseIntegrityProtector protector = new(integrityKeys);
            UserIntegrityDescriptor descriptor = new();
            DatabaseIntegrityChangeSigner signer = new(protector,
                new DatabaseIntegrityDescriptorRegistry([descriptor, new ChunkIntegrityDescriptor()]),
                NullDatabaseIntegrityFailureReporter.Instance);
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql(DbContext.Database.GetConnectionString());
            options.AddInterceptors(new DatabaseIntegritySaveChangesInterceptor(signer));
            await using CottonDbContext context = new(options.Options);
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();

            byte[] missingHash = SHA256.HashData("missing-avatar"u8);
            byte[] existingHash = SHA256.HashData("existing-avatar"u8);
            User firstAffected = CreateUser(missingHash);
            User secondAffected = CreateUser(missingHash);
            User unaffected = CreateUser(existingHash);
            User withoutAvatar = CreateUser(null);
            User[] users = [firstAffected, secondAffected, unaffected, withoutAvatar];
            byte[] originalEncryptedAvatar = unaffected.AvatarHashEncrypted!;
            context.Users.AddRange(users);
            context.Chunks.AddRange(new[] { missingHash, existingHash }.Select(hash => new Chunk
            {
                Hash = hash,
                PlainSizeBytes = 4,
                StoredSizeBytes = 4
            }));
            await context.SaveChangesAsync();
            Dictionary<Guid, byte[]> originalPayloads = users.ToDictionary(user => user.Id, descriptor.BuildCanonicalPayload);
            Dictionary<Guid, byte[]> originalSignatures = users.ToDictionary(user => user.Id,
                user => (byte[])context.Entry(user).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!);

            InMemoryStorage storage = new();
            await storage.WriteAsync(Hasher.ToHexStringHash(existingHash), new MemoryStream([1, 2, 3, 4]));
            ChunkUsageService usage = new(context, storage, new StaticBackupManifestService(null),
                new DatabaseBackupKeyProvider(settings), NullLogger<ChunkUsageService>.Instance);
            ServiceCollection services = new();
            services.AddDbContext<CottonDbContext>(builder => builder.UseNpgsql(context.Database.GetConnectionString()));
            await using ServiceProvider provider = services.BuildServiceProvider();
            StorageConsistencyJob job = new(storage, context, new NoopNotificationsProvider(), usage,
                NullLogger<StorageConsistencyJob>.Instance, provider.GetRequiredService<IServiceScopeFactory>());

            await job.RunOnceAsync();
            Assert.Multiple(() =>
            {
                Assert.That(firstAffected.AvatarHash, Is.Null);
                Assert.That(firstAffected.AvatarHashEncrypted, Is.Null);
                Assert.That(secondAffected.AvatarHash, Is.Null);
                Assert.That(secondAffected.AvatarHashEncrypted, Is.Null);
                Assert.That(context.Entry(firstAffected).State, Is.EqualTo(EntityState.Unchanged));
                Assert.That(context.Entry(secondAffected).State, Is.EqualTo(EntityState.Unchanged));
            });

            await job.RunOnceAsync();
            context.ChangeTracker.Clear();
            List<User> actual = await context.Users.ToListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(actual, Has.Count.EqualTo(users.Length));
                foreach (User user in actual)
                {
                    if (user.Id == unaffected.Id)
                    {
                        Assert.That(user.AvatarHash, Is.EqualTo(existingHash));
                        Assert.That(user.AvatarHashEncrypted, Is.EqualTo(originalEncryptedAvatar));
                    }
                    else
                    {
                        Assert.That(user.AvatarHash, Is.Null);
                        Assert.That(user.AvatarHashEncrypted, Is.Null);
                    }

                    byte[] signature = (byte[])context.Entry(user).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!;
                    Assert.That(descriptor.BuildCanonicalPayload(user), Is.EqualTo(originalPayloads[user.Id]));
                    Assert.That(signature, Is.EqualTo(originalSignatures[user.Id]));
                    Assert.That(protector.Verify(user, descriptor, signature), Is.True);
                }
            });
        }

        private static User CreateUser(byte[]? avatarHash)
        {
            return new User
            {
                Username = "avatar" + Guid.NewGuid().ToString("N")[..8],
                PasswordPhc = "test-password",
                WebDavTokenPhc = "test-webdav-token",
                AvatarHash = avatarHash,
                AvatarHashEncrypted = avatarHash is null ? null : SHA256.HashData(avatarHash)
            };
        }
    }
}
