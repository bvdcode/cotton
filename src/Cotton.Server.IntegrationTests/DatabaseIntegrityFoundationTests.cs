// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Services.DatabaseIntegrity;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public sealed class DatabaseIntegrityFoundationTests
{
    [Test]
    public void CanonicalWriter_SortsDictionaryKeys()
    {
        var first = new IntegrityTestEntity
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
