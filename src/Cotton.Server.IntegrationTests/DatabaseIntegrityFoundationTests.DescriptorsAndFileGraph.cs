// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cotton.Server.IntegrationTests
{
    public partial class DatabaseIntegrityFoundationTests
    {
        [Test]
        public void UserDescriptor_DetectsRoleTampering()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            UserIntegrityDescriptor descriptor = new UserIntegrityDescriptor();
            User user = new User
            {
                Username = "alice",
                PasswordPhc = "password",
                WebDavTokenPhc = "webdav",
                Role = UserRole.User,
                Email = "alice@example.test",
                IsEmailVerified = true
            };
            byte[] mac = protector.Sign(user, descriptor);

            user.Role = UserRole.Admin;

            Assert.That(protector.Verify(user, descriptor, mac), Is.False);
        }

        [Test]
        public void PasskeyDescriptor_DetectsPublicKeyTampering()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            UserPasskeyCredentialIntegrityDescriptor descriptor = new UserPasskeyCredentialIntegrityDescriptor();
            UserPasskeyCredential credential = new UserPasskeyCredential
            {
                UserId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                CredentialId = [1, 2, 3],
                PublicKey = [4, 5, 6],
                UserHandle = [7, 8, 9],
                SignatureCounter = 10,
                Transports = ["usb"],
                AaGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
            };
            byte[] mac = protector.Sign(credential, descriptor);

            credential.PublicKey = [9, 9, 9];

            Assert.That(protector.Verify(credential, descriptor, mac), Is.False);
        }

        [Test]
        public void DownloadTokenDescriptor_DetectsNodeFileTampering()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            DownloadTokenIntegrityDescriptor descriptor = new DownloadTokenIntegrityDescriptor();
            DownloadToken token = new DownloadToken
            {
                Token = "share-token",
                NodeFileId = Guid.Parse("40000000-0000-0000-0000-000000000002"),
                CreatedByUserId = Guid.Parse("10000000-0000-0000-0000-000000000001")
            };
            byte[] mac = protector.Sign(token, descriptor);

            token.NodeFileId = Guid.Parse("40000000-0000-0000-0000-000000000003");

            Assert.That(protector.Verify(token, descriptor, mac), Is.False);
        }

        [Test]
        public void NodeShareTokenDescriptor_DetectsNodeTampering()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            NodeShareTokenIntegrityDescriptor descriptor = new NodeShareTokenIntegrityDescriptor();
            NodeShareToken token = new NodeShareToken
            {
                Token = "share-token",
                NodeId = Guid.Parse("50000000-0000-0000-0000-000000000002"),
                CreatedByUserId = Guid.Parse("10000000-0000-0000-0000-000000000001")
            };
            byte[] mac = protector.Sign(token, descriptor);

            token.NodeId = Guid.Parse("50000000-0000-0000-0000-000000000003");

            Assert.That(protector.Verify(token, descriptor, mac), Is.False);
        }

        [Test]
        public void RefreshTokenDescriptor_DetectsSessionTampering()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            ExtendedRefreshTokenIntegrityDescriptor descriptor = new ExtendedRefreshTokenIntegrityDescriptor();
            ExtendedRefreshToken token = new ExtendedRefreshToken
            {
                UserId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Token = "refresh-token-hash",
                SessionId = "session-a",
                IsTrusted = true,
                AuthType = AuthType.Credentials,
                IpAddress = System.Net.IPAddress.Loopback,
                UserAgent = "test",
                Device = "test",
                City = "test",
                Region = "test",
                Country = "test"
            };
            byte[] mac = protector.Sign(token, descriptor);

            token.SessionId = "session-b";

            Assert.That(protector.Verify(token, descriptor, mac), Is.False);
        }

        [Test]
        public void NodeDescriptor_DetectsParentTampering()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            NodeIntegrityDescriptor descriptor = new NodeIntegrityDescriptor();
            Node node = new Node
            {
                OwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                LayoutId = Guid.Parse("70000000-0000-0000-0000-000000000001"),
                ParentId = Guid.Parse("60000000-0000-0000-0000-000000000001"),
                Type = NodeType.Default
            };
            node.SetName("Documents");
            byte[] mac = protector.Sign(node, descriptor);

            node.ParentId = Guid.Parse("60000000-0000-0000-0000-000000000003");

            Assert.That(protector.Verify(node, descriptor, mac), Is.False);
        }

        [Test]
        public void NodeFileDescriptor_DetectsManifestTampering()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            NodeFileIntegrityDescriptor descriptor = new NodeFileIntegrityDescriptor();
            NodeFile file = new NodeFile
            {
                OwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                NodeId = Guid.Parse("60000000-0000-0000-0000-000000000002"),
                OriginalNodeFileId = Guid.Parse("80000000-0000-0000-0000-000000000002"),
                FileManifestId = Guid.Parse("90000000-0000-0000-0000-000000000001")
            };
            file.SetName("report.pdf");
            byte[] mac = protector.Sign(file, descriptor);

            file.FileManifestId = Guid.Parse("90000000-0000-0000-0000-000000000002");

            Assert.That(protector.Verify(file, descriptor, mac), Is.False);
        }

        [Test]
        public void FileManifestDescriptor_DetectsContentHashTampering()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            FileManifestIntegrityDescriptor descriptor = new FileManifestIntegrityDescriptor();
            FileManifest manifest = new FileManifest
            {
                ProposedContentHash = [1, 2, 3],
                ComputedContentHash = [1, 2, 3],
                ContentType = "text/plain",
                SizeBytes = 3,
                PreviewGeneratorVersion = 1
            };
            byte[] mac = protector.Sign(manifest, descriptor);

            manifest.ProposedContentHash = [9, 9, 9];

            Assert.That(protector.Verify(manifest, descriptor, mac), Is.False);
        }

        [Test]
        public void FileManifestDescriptor_UsesReleaseSchemaVersion()
        {
            Assert.That(new FileManifestIntegrityDescriptor().SchemaVersion, Is.EqualTo(1));
        }

        [Test]
        public void FileManifestDescriptor_IgnoresExtractedMetadata()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            FileManifestIntegrityDescriptor descriptor = new FileManifestIntegrityDescriptor();
            FileManifest manifest = new FileManifest
            {
                ProposedContentHash = [1, 2, 3],
                ComputedContentHash = [1, 2, 3],
                ContentType = "audio/flac",
                SizeBytes = 3,
                Metadata = new Dictionary<string, string>
                {
                    ["media.title"] = "Song",
                },
            };
            byte[] mac = protector.Sign(manifest, descriptor);

            manifest.Metadata["media.title"] = "Other";

            Assert.That(protector.Verify(manifest, descriptor, mac), Is.True);
        }

        [Test]
        public void FileManifestDescriptor_IgnoresOperationalPreviewState()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            FileManifestIntegrityDescriptor descriptor = new FileManifestIntegrityDescriptor();
            FileManifest manifest = new FileManifest
            {
                ProposedContentHash = [1, 2, 3],
                ComputedContentHash = [1, 2, 3],
                ContentType = "text/plain",
                SizeBytes = 3,
                PreviewGenerationError = "ffmpeg failed before the runtime image was fixed",
                PreviewGeneratorVersion = 1
            };
            byte[] mac = protector.Sign(manifest, descriptor);

            manifest.PreviewGenerationError = null;
            manifest.PreviewGeneratorVersion = 2;

            Assert.That(protector.Verify(manifest, descriptor, mac), Is.True);
        }

        [Test]
        public void FileManifestChunkDescriptor_DetectsOrderTampering()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            FileManifestChunkIntegrityDescriptor descriptor = new FileManifestChunkIntegrityDescriptor();
            FileManifestChunk mapping = new FileManifestChunk
            {
                FileManifestId = Guid.Parse("90000000-0000-0000-0000-000000000001"),
                ChunkOrder = 0,
                ChunkHash = [1, 2, 3]
            };
            byte[] mac = protector.Sign(mapping, descriptor);

            mapping.ChunkOrder = 1;

            Assert.That(protector.Verify(mapping, descriptor, mac), Is.False);
        }

        [Test]
        public void ChunkDescriptor_DetectsSizeTampering()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            ChunkIntegrityDescriptor descriptor = new ChunkIntegrityDescriptor();
            Chunk chunk = new Chunk
            {
                Hash = [1, 2, 3],
                PlainSizeBytes = 3,
                StoredSizeBytes = 4,
                CompressionAlgorithm = CompressionAlgorithm.Zstd
            };
            byte[] mac = protector.Sign(chunk, descriptor);

            chunk.PlainSizeBytes = 5;

            Assert.That(protector.Verify(chunk, descriptor, mac), Is.False);
        }

        [Test]
        public void FileGraphVerifier_RejectsNonContiguousChunkOrder()
        {
            DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql("Host=localhost;Database=cotton_dev;Username=postgres;Password=postgres")
                .Options;
            using CottonDbContext dbContext = new CottonDbContext(options);
            FileGraphIntegrityVerifier verifier = new FileGraphIntegrityVerifier(new NoopDatabaseIntegrityVerifier(), NullDatabaseIntegrityFailureReporter.Instance);

            Node node = new Node
            {
                OwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                LayoutId = Guid.Parse("70000000-0000-0000-0000-000000000001"),
                Type = NodeType.Default
            };
            node.SetName("Documents");

            FileManifest manifest = new FileManifest
            {
                ProposedContentHash = [1, 2, 3],
                ContentType = "text/plain",
                SizeBytes = 3
            };
            Chunk chunk = new Chunk
            {
                Hash = [1, 2, 3],
                PlainSizeBytes = 3,
                StoredSizeBytes = 4,
                CompressionAlgorithm = CompressionAlgorithm.Zstd
            };
            FileManifestChunk manifestChunk = new FileManifestChunk
            {
                FileManifestId = manifest.Id,
                ChunkOrder = 1,
                ChunkHash = chunk.Hash,
                Chunk = chunk
            };
            manifest.FileManifestChunks.Add(manifestChunk);

            NodeFile nodeFile = new NodeFile
            {
                OwnerId = node.OwnerId,
                NodeId = node.Id,
                Node = node,
                FileManifestId = manifest.Id,
                FileManifest = manifest,
                OriginalNodeFileId = Guid.Parse("80000000-0000-0000-0000-000000000002")
            };
            nodeFile.SetName("report.txt");

            Assert.Throws<DatabaseIntegrityException>(() =>
                verifier.RequireValidContent(dbContext, nodeFile, "test.file-graph"));
        }

    }
}
