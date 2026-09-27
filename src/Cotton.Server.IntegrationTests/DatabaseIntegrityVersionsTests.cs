// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using static Cotton.Server.IntegrationTests.Helpers.DatabaseIntegrityTestData;

namespace Cotton.Server.IntegrationTests
{
    public class DatabaseIntegrityVersionsTests
    {

        [Test]
        public void FileManifestDescriptor_PreservesReleaseSignature()
        {
            FileManifest manifest = CreateVersionedManifest();
            IDatabaseIntegrityDescriptor<FileManifest> descriptor = CreateVersionedRegistry().Get<FileManifest>(version: 1);
            Assert.That(CreateProtector().Verify(manifest, descriptor, Convert.FromHexString(LegacyManifestMac)), Is.True);
        }

        [Test]
        public void NodeFileDescriptor_PreservesReleaseSignature()
        {
            NodeFile file = CreateVersionedNodeFile();
            IDatabaseIntegrityDescriptor<NodeFile> descriptor = CreateVersionedRegistry().Get<NodeFile>(version: 1);
            Assert.That(CreateProtector().Verify(file, descriptor, Convert.FromHexString(LegacyNodeFileMac)), Is.True);
        }

        [Test]
        public void FileManifestDescriptor_PreservesVersion2Signature()
        {
            FileManifest manifest = CreateVersionedManifest();
            IDatabaseIntegrityDescriptor<FileManifest> descriptor = CreateVersionedRegistry().Get<FileManifest>(version: 2);
            Assert.That(CreateProtector().Verify(manifest, descriptor, Convert.FromHexString(Version2ManifestMac)), Is.True);
        }

        [Test]
        public void NodeFileDescriptor_PreservesVersion2Signature()
        {
            NodeFile file = CreateVersionedNodeFile();
            IDatabaseIntegrityDescriptor<NodeFile> descriptor = CreateVersionedRegistry().Get<NodeFile>(version: 2);
            Assert.That(CreateProtector().Verify(file, descriptor, Convert.FromHexString(Version2NodeFileMac)), Is.True);
        }

        [Test]
        public void Registry_ConcurrentVersionSelection_PreservesBothFormats()
        {
            DatabaseIntegrityDescriptorRegistry registry = CreateVersionedRegistry();
            DatabaseIntegrityProtector protector = CreateProtector();
            NodeFile file = CreateVersionedNodeFile();
            byte[][] macs = [Convert.FromHexString(LegacyNodeFileMac), Convert.FromHexString(Version2NodeFileMac)];
            Parallel.For(0, 100, index =>
            {
                int version = index % 2 + 1;
                IDatabaseIntegrityDescriptor<NodeFile> descriptor = registry.Get<NodeFile>(version);
                Assert.That(protector.Verify(file, descriptor, macs[version - 1]), Is.True);
                Assert.That(registry.Get<NodeFile>().SchemaVersion, Is.EqualTo(NodeFileIntegrityDescriptor.LatestVersion));
            });
        }

        [Test]
        public void Registry_VersionSelectionDoesNotChangeLatestDescriptor()
        {
            DatabaseIntegrityDescriptorRegistry registry = CreateVersionedRegistry();
            IDatabaseIntegrityDescriptor<NodeFile> latest = registry.Get<NodeFile>();
            IDatabaseIntegrityDescriptor<NodeFile> legacy = registry.Get<NodeFile>(version: 1);
            Assert.Multiple(() =>
            {
                Assert.That(legacy.SchemaVersion, Is.EqualTo(1));
                Assert.That(latest.SchemaVersion, Is.EqualTo(NodeFileIntegrityDescriptor.LatestVersion));
                Assert.That(registry.Get<NodeFile>(), Is.SameAs(latest));
                Assert.That(legacy.Latest, Is.SameAs(latest));
                Assert.That(registry.All, Has.Count.EqualTo(2));
            });
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(3)]
        public void Registry_RejectsUnsupportedVersion(int version)
        {
            DatabaseIntegrityDescriptorRegistry registry = CreateVersionedRegistry();
            Assert.Throws<ArgumentOutOfRangeException>(() => registry.Get<NodeFile>(version));
            Assert.That(registry.TryGet(typeof(NodeFile), version, out _), Is.False);
            Assert.That(registry.Get<NodeFile>().SchemaVersion, Is.EqualTo(NodeFileIntegrityDescriptor.LatestVersion));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Protector_AlwaysSignsLatestVersion_EvenWithLegacyDescriptor(bool nodeFile)
        {
            (object entity, IDatabaseIntegrityDescriptor descriptor, _) = CreateLegacyRow(nodeFile);
            DatabaseIntegrityProtector protector = CreateProtector();
            byte[] mac = protector.Sign(entity, descriptor.ForVersion(1));
            Assert.That(protector.Verify(entity, descriptor, mac), Is.True);
            Assert.That(protector.Verify(entity, descriptor.ForVersion(1), mac), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Descriptors_ContentTypeMovesFromManifestToNodeFile(bool nodeFile)
        {
            (object entity, IDatabaseIntegrityDescriptor descriptor, byte[] legacyMac) = CreateLegacyRow(nodeFile);
            DatabaseIntegrityProtector protector = CreateProtector();
            byte[] latestMac = protector.Sign(entity, descriptor);
            using CottonDbContext dbContext = CreateDbContext();
            dbContext.Entry(entity).Property("ContentType").CurrentValue = "application/pdf";
            Assert.Multiple(() =>
            {
                Assert.That(protector.Verify(entity, descriptor, latestMac), Is.EqualTo(!nodeFile));
                Assert.That(protector.Verify(entity, descriptor.ForVersion(1), legacyMac), Is.EqualTo(nodeFile));
            });
        }
    }
}
