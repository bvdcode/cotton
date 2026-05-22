// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Database.Models;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Models.Enums;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public sealed class DatabaseIntegrityFoundationTests
{
    [Test]
    public void CanonicalWriter_SortsDictionaryKeys()
    {
        var first = new IntegrityTestEntity
        {
            Name = "file.txt",
            Metadata = new Dictionary<string, string>
            {
                ["z"] = "last",
                ["a"] = "first"
            }
        };
        var second = first with
        {
            Metadata = new Dictionary<string, string>
            {
                ["a"] = "first",
                ["z"] = "last"
            }
        };
        var descriptor = new IntegrityTestEntityDescriptor();

        byte[] firstPayload = descriptor.BuildCanonicalPayload(first);
        byte[] secondPayload = descriptor.BuildCanonicalPayload(second);

        Assert.That(firstPayload, Is.EqualTo(secondPayload));
    }

    [Test]
    public void CanonicalWriter_PreservesArrayOrder()
    {
        var descriptor = new IntegrityTestEntityDescriptor();
        var first = new IntegrityTestEntity
        {
            Name = "file.txt",
            Transports = ["usb", "nfc"]
        };
        var second = first with
        {
            Transports = ["nfc", "usb"]
        };

        byte[] firstPayload = descriptor.BuildCanonicalPayload(first);
        byte[] secondPayload = descriptor.BuildCanonicalPayload(second);

        Assert.That(firstPayload, Is.Not.EqualTo(secondPayload));
    }

    [Test]
    public void CanonicalWriter_NormalizesDateTimeToDatabasePrecision()
    {
        var descriptor = new IntegrityTestEntityDescriptor();
        var first = CreateEntity() with
        {
            SeenAt = new DateTime(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc).AddTicks(1)
        };
        var second = first with
        {
            SeenAt = first.SeenAt!.Value.AddTicks(TimeSpan.TicksPerMicrosecond - 2)
        };

        byte[] firstPayload = descriptor.BuildCanonicalPayload(first);
        byte[] secondPayload = descriptor.BuildCanonicalPayload(second);

        Assert.That(firstPayload, Is.EqualTo(secondPayload));
    }

    [Test]
    public void Protector_VerifiesSignedEntity()
    {
        var protector = CreateProtector();
        var descriptor = new IntegrityTestEntityDescriptor();
        var entity = CreateEntity();

        byte[] mac = protector.Sign(entity, descriptor);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mac, Has.Length.EqualTo(32));
            Assert.That(protector.Verify(entity, descriptor, mac), Is.True);
        }
    }

    [Test]
    public void Protector_DetectsTamperedEntity()
    {
        var protector = CreateProtector();
        var descriptor = new IntegrityTestEntityDescriptor();
        var entity = CreateEntity();
        byte[] mac = protector.Sign(entity, descriptor);

        IntegrityTestEntity tampered = entity with { Name = "evil.txt" };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(protector.Verify(tampered, descriptor, mac), Is.False);
            Assert.Throws<DatabaseIntegrityException>(() => protector.RequireValid(tampered, descriptor, mac));
        }
    }

    [Test]
    public void Protector_UsesPurposeSeparatedMasterDerivedKey()
    {
        var descriptor = new IntegrityTestEntityDescriptor();
        var entity = CreateEntity();
        var firstProtector = CreateProtector("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var secondProtector = CreateProtector("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        byte[] firstMac = firstProtector.Sign(entity, descriptor);
        byte[] secondMac = secondProtector.Sign(entity, descriptor);

        Assert.That(firstMac, Is.Not.EqualTo(secondMac));
    }

    [Test]
    public void UserDescriptor_DetectsRoleTampering()
    {
        var protector = CreateProtector();
        var descriptor = new UserIntegrityDescriptor();
        var user = new User
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
        var protector = CreateProtector();
        var descriptor = new UserPasskeyCredentialIntegrityDescriptor();
        var credential = new UserPasskeyCredential
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
    public void ServerSettingsDescriptor_DetectsStorageCredentialTampering()
    {
        var protector = CreateProtector();
        var descriptor = new CottonServerSettingsIntegrityDescriptor();
        var settings = new CottonServerSettings
        {
            InstanceId = Guid.Parse("30000000-0000-0000-0000-000000000002"),
            PublicBaseUrl = "https://cloud.example.test",
            S3AccessKeyId = "access-key",
            S3BucketName = "bucket",
            S3Region = "auto",
            S3EndpointUrl = "https://s3.example.test",
            S3SecretAccessKeyEncrypted = "encrypted-secret"
        };
        byte[] mac = protector.Sign(settings, descriptor);

        settings.S3SecretAccessKeyEncrypted = "other-secret";

        Assert.That(protector.Verify(settings, descriptor, mac), Is.False);
    }

    [Test]
    public void DownloadTokenDescriptor_DetectsNodeFileTampering()
    {
        var protector = CreateProtector();
        var descriptor = new DownloadTokenIntegrityDescriptor();
        var token = new DownloadToken
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
        var protector = CreateProtector();
        var descriptor = new NodeShareTokenIntegrityDescriptor();
        var token = new NodeShareToken
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
        var protector = CreateProtector();
        var descriptor = new ExtendedRefreshTokenIntegrityDescriptor();
        var token = new ExtendedRefreshToken
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

    private static DatabaseIntegrityProtector CreateProtector(
        string rootMasterKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
    {
        var settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(rootMasterKey);
        return new DatabaseIntegrityProtector(new DatabaseIntegrityKeyProvider(settings));
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

    private sealed record IntegrityTestEntity
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

    private sealed class IntegrityTestEntityDescriptor : DatabaseIntegrityDescriptor<IntegrityTestEntity>
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
