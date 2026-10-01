// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public static class DatabaseIntegrityTestData
    {
        public static DatabaseIntegrityProtector CreateProtector(
            string rootMasterKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
        {
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(rootMasterKey);
            return new DatabaseIntegrityProtector(new DatabaseIntegrityKeyProvider(settings));
        }

        public static CottonDbContext CreateDbContext()
        {
            DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql("Host=localhost;Database=cotton_dev;Username=postgres;Password=postgres")
                .Options;
            return new CottonDbContext(options);
        }

        public static DatabaseIntegrityVerifier CreateVerifier(
            IDatabaseIntegrityProtector protector,
            IDatabaseIntegrityDescriptor descriptor)
        {
            return new DatabaseIntegrityVerifier(
                protector,
                new DatabaseIntegrityDescriptorRegistry([descriptor]),
                NullDatabaseIntegrityFailureReporter.Instance,
                NullLogger<DatabaseIntegrityVerifier>.Instance);
        }

        public static User CreateUser()
        {
            return new User
            {
                Username = "alice",
                PasswordPhc = "password",
                WebDavTokenPhc = "webdav",
                Role = UserRole.User,
                Email = "alice@example.test",
                IsEmailVerified = true
            };
        }

        public static IntegrityTestEntity CreateEntity()
        {
            return new IntegrityTestEntity
            {
                OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "file.txt",
                SizeBytes = 12345,
                IsEnabled = true,
                SeenAt = new DateTime(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc),
                Transports = ["usb", "nfc"],
                Metadata = new Dictionary<string, string>
                {
                    ["purpose"] = "test",
                    ["kind"] = "fixture"
                }
            };
        }
        public const string LegacyManifestMac = "DC91915DAB9B91702A3900E1B5D3462BBE3B0A70026569BA19CCA122D2C7183C";
        public const string LegacyNodeFileMac = "4D7DDD8629D423731E862FFD94C2F57483A564A753FC9DDD2FED25E8B2021E04";
        public const string Version2ManifestMac = "039CDB85366DD5D84DAB2D1A21D8B59F55A28432B185C35DF44FD7C89D81AE3B";
        public const string Version2NodeFileMac = "3C17F8E3247C45218300F030E388E8E6C91CF581E9C6CF3B9FC1A946CD5B051D";

        public static DatabaseIntegrityDescriptorRegistry CreateVersionedRegistry()
        {
            return new DatabaseIntegrityDescriptorRegistry([new FileManifestIntegrityDescriptor(), new NodeFileIntegrityDescriptor()]);
        }

        public static (object Entity, IDatabaseIntegrityDescriptor Descriptor, byte[] Mac) CreateLegacyRow(bool nodeFile)
        {
            if (nodeFile)
            {
                return (CreateVersionedNodeFile(), new NodeFileIntegrityDescriptor(), Convert.FromHexString(LegacyNodeFileMac));
            }
            return (CreateVersionedManifest(), new FileManifestIntegrityDescriptor(), Convert.FromHexString(LegacyManifestMac));
        }

        public static FileManifest CreateVersionedManifest()
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

        public static NodeFile CreateVersionedNodeFile()
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

        public static EntityEntry AttachLegacyRow(CottonDbContext dbContext, object entity, byte[] mac)
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
