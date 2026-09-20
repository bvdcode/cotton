// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cotton.Server.IntegrationTests
{
    public partial class DatabaseIntegrityFoundationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ChangeSigner_VerifiesLegacyRowBeforeUpgradingSignature(bool nodeFile)
        {
            (object entity, IDatabaseIntegrityDescriptor descriptor, byte[] legacyMac) = CreateLegacyRow(nodeFile);
            DatabaseIntegrityProtector protector = CreateProtector();
            using CottonDbContext dbContext = CreateDbContext();
            EntityEntry entry = AttachLegacyRow(dbContext, entity, legacyMac);
            DatabaseIntegrityVerifier verifier = CreateVerifier(protector, descriptor);
            Assert.DoesNotThrow(() => verifier.RequireValid(dbContext, entity, "test.legacy-read"));
            Assert.That(entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue, Is.EqualTo(1));

            entry.Property("ContentType").CurrentValue = "application/pdf";
            DatabaseIntegrityChangeSigner signer = new(protector, CreateVersionedRegistry(), NullDatabaseIntegrityFailureReporter.Instance);
            signer.SignPendingChanges(dbContext);

            byte[] mac = (byte[])entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!;
            Assert.Multiple(() =>
            {
                Assert.That(entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue, Is.EqualTo(descriptor.SchemaVersion));
                Assert.That(entry.Property(DatabaseIntegrityColumns.MacProperty).OriginalValue, Is.EqualTo(legacyMac));
                Assert.That(protector.Verify(entity, descriptor, mac), Is.True);
                Assert.That(mac, Is.Not.EqualTo(legacyMac));
            });
            Assert.DoesNotThrow(() => verifier.RequireValid(dbContext, entity, "test.upgraded-read"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChangeSigner_DoesNotResignTamperedLegacyRow(bool nodeFile)
        {
            (object entity, IDatabaseIntegrityDescriptor descriptor, byte[] legacyMac) = CreateLegacyRow(nodeFile);
            if (entity is NodeFile file)
            {
                file.SetName("tampered.txt");
            }
            else
            {
                ((FileManifest)entity).SizeBytes++;
            }
            DatabaseIntegrityProtector protector = CreateProtector();
            using CottonDbContext dbContext = CreateDbContext();
            EntityEntry entry = AttachLegacyRow(dbContext, entity, legacyMac);
            Assert.Throws<DatabaseIntegrityException>(() => CreateVerifier(protector, descriptor)
                .RequireValid(dbContext, entity, "test.tampered-legacy-read"));

            entry.Property("ContentType").CurrentValue = "application/pdf";
            DatabaseIntegrityChangeSigner signer = new(protector, CreateVersionedRegistry(), NullDatabaseIntegrityFailureReporter.Instance);
            Assert.Throws<DatabaseIntegrityException>(() => signer.SignPendingChanges(dbContext));
            Assert.That(entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue, Is.EqualTo(legacyMac));
        }

        [TestCase(false, 0)]
        [TestCase(false, 2)]
        [TestCase(false, 3)]
        [TestCase(true, 0)]
        [TestCase(true, 2)]
        [TestCase(true, 3)]
        public void VersionChangeWithoutMatchingMac_IsRejectedOnReadAndWrite(bool nodeFile, int storedVersion)
        {
            (object entity, IDatabaseIntegrityDescriptor descriptor, byte[] legacyMac) = CreateLegacyRow(nodeFile);
            DatabaseIntegrityProtector protector = CreateProtector();
            using CottonDbContext dbContext = CreateDbContext();
            EntityEntry entry = AttachLegacyRow(dbContext, entity, legacyMac);
            entry.Property(DatabaseIntegrityColumns.VersionProperty).OriginalValue = storedVersion;
            entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = storedVersion;
            Assert.Throws<DatabaseIntegrityException>(() => CreateVerifier(protector, descriptor)
                .RequireValid(dbContext, entity, "test.version-tampering"));

            entry.Property("ContentType").CurrentValue = "application/pdf";
            DatabaseIntegrityChangeSigner signer = new(protector, CreateVersionedRegistry(), NullDatabaseIntegrityFailureReporter.Instance);
            Assert.Throws<DatabaseIntegrityException>(() => signer.SignPendingChanges(dbContext));
        }

        private static EntityEntry AttachLegacyRow(CottonDbContext dbContext, object entity, byte[] mac)
        {
            EntityEntry entry = dbContext.Attach(entity);
            entry.Property(DatabaseIntegrityColumns.VersionProperty).OriginalValue = 1;
            entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = 1;
            entry.Property(DatabaseIntegrityColumns.MacProperty).OriginalValue = mac;
            entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue = mac;
            return entry;
        }
    }
}
