// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Models.Enums;
using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cotton.Server.IntegrationTests
{
    public partial class DatabaseIntegrityFoundationTests
    {

        private static DatabaseIntegrityProtector CreateProtector(
            string rootMasterKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
        {
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(rootMasterKey);
            return new DatabaseIntegrityProtector(new DatabaseIntegrityKeyProvider(settings));
        }

        private static CottonDbContext CreateDbContext()
        {
            DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql("Host=localhost;Database=cotton_dev;Username=postgres;Password=postgres")
                .Options;
            return new CottonDbContext(options);
        }

        private static DatabaseIntegrityVerifier CreateVerifier(
            IDatabaseIntegrityProtector protector,
            IDatabaseIntegrityDescriptor descriptor)
        {
            return new DatabaseIntegrityVerifier(
                protector,
                new DatabaseIntegrityDescriptorRegistry([descriptor]),
                NullDatabaseIntegrityFailureReporter.Instance,
                NullLogger<DatabaseIntegrityVerifier>.Instance);
        }

        private static User CreateUser()
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

        private static IntegrityTestEntity CreateEntity()
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

        private class NoopDatabaseIntegrityVerifier : IDatabaseIntegrityVerifier
        {
            public void RequireValid<TEntity>(CottonDbContext dbContext, TEntity entity, string boundary)
                where TEntity : class
            {
            }
        }

        private record IntegrityTestEntity
        {
            public Guid Id { get; init; }
            public Guid? OwnerId { get; init; }
            public string? Name { get; init; }
            public long SizeBytes { get; init; }
            public bool IsEnabled { get; init; }
            public DateTime? SeenAt { get; init; }
            public string[]? Transports { get; init; }
            public Dictionary<string, string>? Metadata { get; init; }
        }

        private class IntegrityTestEntityDescriptor : DatabaseIntegrityDescriptor<IntegrityTestEntity>
        {
            public override string EntityName => "test_entity";
            public override int SchemaVersion => 1;

            public override string GetEntityKey(IntegrityTestEntity entity)
            {
                return entity.Id.ToString("D");
            }

            public override void WriteCanonicalData(
                DatabaseIntegrityCanonicalWriter writer,
                IntegrityTestEntity entity)
            {
                writer.WriteGuidField(nameof(entity.Id), entity.Id);
                writer.WriteNullableGuidField(nameof(entity.OwnerId), entity.OwnerId);
                writer.WriteStringField(nameof(entity.Name), entity.Name);
                writer.WriteInt64Field(nameof(entity.SizeBytes), entity.SizeBytes);
                writer.WriteBooleanField(nameof(entity.IsEnabled), entity.IsEnabled);
                writer.WriteNullableDateTimeField(nameof(entity.SeenAt), entity.SeenAt);
                writer.WriteStringArrayField(nameof(entity.Transports), entity.Transports);
                writer.WriteStringDictionaryField(nameof(entity.Metadata), entity.Metadata);
            }
        }
    }
}
