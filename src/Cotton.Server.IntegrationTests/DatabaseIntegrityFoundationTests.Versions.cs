// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests
{
    public partial class DatabaseIntegrityFoundationTests
    {
        private const string LegacyManifestMac = "DC91915DAB9B91702A3900E1B5D3462BBE3B0A70026569BA19CCA122D2C7183C";
        private const string LegacyNodeFileMac = "4D7DDD8629D423731E862FFD94C2F57483A564A753FC9DDD2FED25E8B2021E04";
        private const string Version2ManifestMac = "039CDB85366DD5D84DAB2D1A21D8B59F55A28432B185C35DF44FD7C89D81AE3B";
        private const string Version2NodeFileMac = "3C17F8E3247C45218300F030E388E8E6C91CF581E9C6CF3B9FC1A946CD5B051D";

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

        private static DatabaseIntegrityDescriptorRegistry CreateVersionedRegistry()
        {
            return new DatabaseIntegrityDescriptorRegistry([new FileManifestIntegrityDescriptor(), new NodeFileIntegrityDescriptor()]);
        }

        private static (object Entity, IDatabaseIntegrityDescriptor Descriptor, byte[] Mac) CreateLegacyRow(bool nodeFile)
        {
            if (nodeFile)
            {
                return (CreateVersionedNodeFile(), new NodeFileIntegrityDescriptor(), Convert.FromHexString(LegacyNodeFileMac));
            }
            return (CreateVersionedManifest(), new FileManifestIntegrityDescriptor(), Convert.FromHexString(LegacyManifestMac));
        }

        private static FileManifest CreateVersionedManifest()
        {
            FileManifest manifest = new()
            {
                ProposedContentHash = [1, 2, 3],
                ComputedContentHash = [4, 5, 6],
                ContentType = "text/plain",
                SizeBytes = 123,
                SmallFilePreviewHashEncrypted = [7, 8, 9],
                SmallFilePreviewHash = [10, 11, 12],
                LargeFilePreviewHash = [13, 14, 15],
            };
            using CottonDbContext dbContext = CreateDbContext();
            dbContext.Entry(manifest).Property(entity => entity.Id).CurrentValue =
                Guid.Parse("90000000-0000-0000-0000-000000000001");
            return manifest;
        }

        private static NodeFile CreateVersionedNodeFile()
        {
            NodeFile file = new()
            {
                OwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                NodeId = Guid.Parse("60000000-0000-0000-0000-000000000001"),
                FileManifestId = Guid.Parse("90000000-0000-0000-0000-000000000001"),
                OriginalNodeFileId = Guid.Parse("80000000-0000-0000-0000-000000000001"),
                Metadata = new Dictionary<string, string> { ["label"] = "Document" },
            };
            file.SetName("report.txt");
            using CottonDbContext dbContext = CreateDbContext();
            dbContext.Entry(file).Property(entity => entity.Id).CurrentValue =
                Guid.Parse("80000000-0000-0000-0000-000000000001");
            return file;
        }
    }
}
