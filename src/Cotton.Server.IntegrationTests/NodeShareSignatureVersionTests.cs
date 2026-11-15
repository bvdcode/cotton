// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using static Cotton.Server.IntegrationTests.Helpers.DatabaseIntegrityTestData;

namespace Cotton.Server.IntegrationTests
{
    public class NodeShareSignatureVersionTests
    {
        private const string LegacyPayload = "436F74746F6E2E4462496E746567726974792E526F770000000F247061796C6F61642D666F726D6174050000000100000004246D616301010000000C484D41432D5348412D323536000000112463616E6F6E6963616C2D77726974657205000000010000000724656E746974790101000000116E6F64655F73686172655F746F6B656E730000000724736368656D61050000000100000004246B657901010000002431303030303030302D303030302D303030302D303030302D303030303030303030303031000000024964020000002431303030303030302D303030302D303030302D303030302D30303030303030303030303100000005546F6B656E01010000000C6C65676163792D7368617265000000064E6F64654964020000002432303030303030302D303030302D303030302D303030302D3030303030303030303030310000000F437265617465644279557365724964020000002433303030303030302D303030302D303030302D303030302D303030303030303030303031000000094578706972657341740700";

        [Test]
        public void VersionOnePayloadRemainsByteForByteCompatible()
        {
            NodeShareToken share = CreateShare();
            NodeShareTokenIntegrityDescriptor descriptor = new();
            Assert.That(descriptor.ForVersion(1).BuildCanonicalPayload(share), Is.EqualTo(Convert.FromHexString(LegacyPayload)));
            Assert.That(CreateProtector().Verify(share, descriptor.ForVersion(1), LegacyMac()), Is.True);
        }

        [Test]
        public void ReadingLegacySignatureDoesNotModifyItAndTrackedSaveUpgradesOnlyThatRow()
        {
            using CottonDbContext db = CreateDbContext();
            NodeShareToken share = CreateShare();
            NodeShareToken untouched = CreateShare();
            db.Entry(untouched).Property(x => x.Id).CurrentValue = Guid.NewGuid();
            NodeShareTokenIntegrityDescriptor descriptor = new();
            EntityEntry entry = AttachLegacyRow(db, share, LegacyMac());
            entry.State = EntityState.Unchanged;
            byte[] untouchedMac = ComputeLegacyMac(descriptor.ForVersion(1).BuildCanonicalPayload(untouched));
            EntityEntry retained = AttachLegacyRow(db, untouched, untouchedMac);
            retained.State = EntityState.Unchanged;
            DatabaseIntegrityProtector protector = CreateProtector();
            DatabaseIntegrityVerifier verifier = CreateVerifier(protector, descriptor);
            Assert.DoesNotThrow(() => verifier.RequireValid(db, share, "test.share-read"));
            Assert.That(entry.State, Is.EqualTo(EntityState.Unchanged));
            Assert.That(entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue, Is.EqualTo(1));
            share.AllowUpload = true;
            DatabaseIntegrityChangeSigner signer = new(protector, new DatabaseIntegrityDescriptorRegistry([descriptor]),
                NullDatabaseIntegrityFailureReporter.Instance);
            signer.SignPendingChanges(db);
            Assert.Multiple(() =>
            {
                Assert.That(entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue, Is.EqualTo(NodeShareTokenIntegrityDescriptor.LatestVersion));
                Assert.That(retained.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue, Is.EqualTo(1));
                Assert.That(retained.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue, Is.EqualTo(untouchedMac));
            });
            Assert.DoesNotThrow(() => verifier.RequireValid(db, share, "test.share-upgraded"));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void UploadPermissionTamperingIsRejectedOnReadAndBeforeResigning(int version)
        {
            NodeShareToken share = CreateShare();
            NodeShareTokenIntegrityDescriptor descriptor = new();
            DatabaseIntegrityProtector protector = CreateProtector();
            byte[] mac = ComputeLegacyMac(descriptor.ForVersion(version).BuildCanonicalPayload(share));
            share.AllowUpload = true;
            using CottonDbContext db = CreateDbContext();
            EntityEntry entry = AttachLegacyRow(db, share, mac);
            entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = version;
            entry.Property(DatabaseIntegrityColumns.VersionProperty).OriginalValue = version;
            Assert.Throws<DatabaseIntegrityException>(() => CreateVerifier(protector, descriptor).RequireValid(db, share, "test.share-tampered"));
            share.Name = "renamed";
            DatabaseIntegrityChangeSigner signer = new(protector, new DatabaseIntegrityDescriptorRegistry([descriptor]),
                NullDatabaseIntegrityFailureReporter.Instance);
            Assert.Throws<DatabaseIntegrityException>(() => signer.SignPendingChanges(db));
            Assert.That(entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue, Is.EqualTo(mac));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(3)]
        public void ChangingVersionWithoutMatchingSignatureIsRejected(int version)
        {
            NodeShareToken share = CreateShare();
            using CottonDbContext db = CreateDbContext();
            EntityEntry entry = AttachLegacyRow(db, share, LegacyMac());
            entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = version;
            Assert.Throws<DatabaseIntegrityException>(() => CreateVerifier(CreateProtector(), new NodeShareTokenIntegrityDescriptor())
                .RequireValid(db, share, "test.share-version"));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void HiddenContentCannotBeChangedWithoutResigning(int version)
        {
            NodeShareToken share = CreateShare();
            NodeShareTokenIntegrityDescriptor descriptor = new();
            byte[] mac = ComputeLegacyMac(descriptor.ForVersion(version).BuildCanonicalPayload(share));
            using CottonDbContext db = CreateDbContext();
            EntityEntry entry = AttachLegacyRow(db, share, mac);
            entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = version;
            entry.Property(DatabaseIntegrityColumns.VersionProperty).OriginalValue = version;
            DatabaseIntegrityVerifier verifier = CreateVerifier(CreateProtector(), descriptor);
            Assert.DoesNotThrow(() => verifier.RequireValid(db, share, "test.share-original"));
            share.HideFolderContent = true;
            Assert.Throws<DatabaseIntegrityException>(() => verifier.RequireValid(db, share, "test.share-hidden"));
        }

        private static NodeShareToken CreateShare()
        {
            NodeShareToken share = new()
            {
                Token = "legacy-share", Name = "folder",
                NodeId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                CreatedByUserId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            };
            using CottonDbContext db = CreateDbContext();
            db.Entry(share).Property(x => x.Id).CurrentValue = Guid.Parse("10000000-0000-0000-0000-000000000001");
            return share;
        }

        private static byte[] LegacyMac() => ComputeLegacyMac(Convert.FromHexString(LegacyPayload));

        private static byte[] ComputeLegacyMac(byte[] payload)
        {
            using DatabaseIntegrityKeyProvider key = new(ConfigurationBuilderExtensions.DeriveEncryptionSettings("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
            using HMACSHA256 hmac = key.CreateHmac();
            return hmac.ComputeHash(payload);
        }
    }
}
