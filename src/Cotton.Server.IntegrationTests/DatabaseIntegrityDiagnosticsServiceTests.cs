// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class DatabaseIntegrityDiagnosticsServiceTests : IntegrationTestBase
    {
        [SetUp]
        public void SetUp()
        {
            DbContext.Database.EnsureDeleted();
            DbContext.Database.Migrate();
        }

        [Test]
        public async Task GetSnapshotAsync_CountsMissingAndUnsupportedIntegrityMetadata()
        {
            UserIntegrityDescriptor descriptor = new();
            User validUser = CreateUser("valid-user");
            User unsignedUser = CreateUser("unsigned-user");
            User outdatedUser = CreateUser("outdated-user");
            DbContext.Users.AddRange(validUser, unsignedUser, outdatedUser);
            SetIntegrityMetadata(validUser, descriptor.SchemaVersion, [1]);
            SetIntegrityMetadata(unsignedUser, descriptor.SchemaVersion, null);
            SetIntegrityMetadata(outdatedUser, descriptor.SchemaVersion + 1, [1]);
            await DbContext.SaveChangesAsync();
            DatabaseIntegrityDiagnosticsService diagnostics = new(
                DbContext,
                new DatabaseIntegrityDescriptorRegistry([descriptor]));

            DatabaseIntegrityDiagnosticsDto snapshot = await diagnostics.GetSnapshotAsync(
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Enabled, Is.True);
                Assert.That(snapshot.ProtectedEntityTypes, Is.EqualTo(1));
                Assert.That(snapshot.UnsignedProtectedRows, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task GetSnapshotAsync_AcceptsSupportedLegacyVersions_AndRejectsMissingOrUnknownVersions()
        {
            FileManifestIntegrityDescriptor descriptor = new();
            int?[] versions = [1, FileManifestIntegrityDescriptor.LatestVersion, null, 0, FileManifestIntegrityDescriptor.LatestVersion + 1, 1];
            for (int index = 0; index < versions.Length; index++)
            {
                FileManifest manifest = new() { ProposedContentHash = [(byte)index], ContentType = "text/plain" };
                DbContext.FileManifests.Add(manifest);
                SetIntegrityMetadata(manifest, versions[index], index == versions.Length - 1 ? null : [1]);
            }
            await DbContext.SaveChangesAsync();
            DatabaseIntegrityDiagnosticsService diagnostics = new(DbContext, new DatabaseIntegrityDescriptorRegistry([descriptor]));

            DatabaseIntegrityDiagnosticsDto snapshot = await diagnostics.GetSnapshotAsync(CancellationToken.None);

            Assert.That(snapshot.ProtectedEntityTypes, Is.EqualTo(1));
            Assert.That(snapshot.UnsignedProtectedRows, Is.EqualTo(4));
        }

        private void SetIntegrityMetadata<T>(T entity, int? version, byte[]? mac) where T : class
        {
            EntityEntry<T> entry = DbContext.Entry(entity);
            entry.Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue = version;
            entry.Property<byte[]?>(DatabaseIntegrityColumns.MacProperty).CurrentValue = mac;
        }

        private static User CreateUser(string username)
        {
            return new User
            {
                Username = username,
                PasswordPhc = "password",
                WebDavTokenPhc = "webdav",
                Role = UserRole.User,
            };
        }
    }
}
