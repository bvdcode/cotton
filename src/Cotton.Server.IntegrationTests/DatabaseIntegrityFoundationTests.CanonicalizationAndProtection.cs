// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cotton.Server.IntegrationTests
{
    public partial class DatabaseIntegrityFoundationTests
    {        [Test]
        public void IntegrityModel_UsesMacAsConcurrencyTokenForEveryProtectedEntity()
        {
            using CottonDbContext dbContext = CreateDbContext();
            List<IEntityType> protectedEntityTypes = dbContext.Model
                .GetEntityTypes()
                .Where(entityType => entityType.FindProperty(DatabaseIntegrityColumns.MacProperty) is not null)
                .ToList();

            Assert.That(protectedEntityTypes, Is.Not.Empty);
            Assert.That(
                protectedEntityTypes.All(entityType =>
                    entityType.FindProperty(DatabaseIntegrityColumns.MacProperty)!.IsConcurrencyToken),
                Is.True,
                "Every integrity-protected entity must reject stale writes through its persisted MAC.");
        }

        [Test]
        public void CanonicalWriter_SortsDictionaryKeys()
        {
            IntegrityTestEntity first = new IntegrityTestEntity
            {
                Name = "file.txt",
                Metadata = new Dictionary<string, string>
                {
                    ["z"] = "last",
                    ["a"] = "first"
                }
            };
            IntegrityTestEntity second = first with
            {
                Metadata = new Dictionary<string, string>
                {
                    ["a"] = "first",
                    ["z"] = "last"
                }
            };
            IntegrityTestEntityDescriptor descriptor = new IntegrityTestEntityDescriptor();

            byte[] firstPayload = descriptor.BuildCanonicalPayload(first);
            byte[] secondPayload = descriptor.BuildCanonicalPayload(second);

            Assert.That(firstPayload, Is.EqualTo(secondPayload));
        }

        [Test]
        public void CanonicalWriter_PreservesArrayOrder()
        {
            IntegrityTestEntityDescriptor descriptor = new IntegrityTestEntityDescriptor();
            IntegrityTestEntity first = new IntegrityTestEntity
            {
                Name = "file.txt",
                Transports = ["usb", "nfc"]
            };
            IntegrityTestEntity second = first with
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
            IntegrityTestEntityDescriptor descriptor = new IntegrityTestEntityDescriptor();
            IntegrityTestEntity first = CreateEntity() with
            {
                SeenAt = new DateTime(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc).AddTicks(1)
            };
            IntegrityTestEntity second = first with
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
            DatabaseIntegrityProtector protector = CreateProtector();
            IntegrityTestEntityDescriptor descriptor = new IntegrityTestEntityDescriptor();
            IntegrityTestEntity entity = CreateEntity();

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
            DatabaseIntegrityProtector protector = CreateProtector();
            IntegrityTestEntityDescriptor descriptor = new IntegrityTestEntityDescriptor();
            IntegrityTestEntity entity = CreateEntity();
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
            IntegrityTestEntityDescriptor descriptor = new IntegrityTestEntityDescriptor();
            IntegrityTestEntity entity = CreateEntity();
            DatabaseIntegrityProtector firstProtector = CreateProtector("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            DatabaseIntegrityProtector secondProtector = CreateProtector("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            byte[] firstMac = firstProtector.Sign(entity, descriptor);
            byte[] secondMac = secondProtector.Sign(entity, descriptor);

            Assert.That(firstMac, Is.Not.EqualTo(secondMac));
        }

        [Test]
        public void Verifier_AcceptsSignedProtectedEntity()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            UserIntegrityDescriptor descriptor = new UserIntegrityDescriptor();
            User user = CreateUser();

            using CottonDbContext dbContext = CreateDbContext();
            dbContext.Users.Add(user);
            DatabaseIntegrityChangeSigner signer = new DatabaseIntegrityChangeSigner(
                protector,
                new DatabaseIntegrityDescriptorRegistry([descriptor]),
                NullDatabaseIntegrityFailureReporter.Instance);
            signer.SignPendingChanges(dbContext);
            DatabaseIntegrityVerifier verifier = CreateVerifier(protector, descriptor);

            Assert.DoesNotThrow(() => verifier.RequireValid(dbContext, user, "test.signed"));
        }

        [Test]
        public void Verifier_ReportsRequiredTransitionVersionForUnsignedProtectedEntity()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            UserIntegrityDescriptor descriptor = new UserIntegrityDescriptor();
            User user = CreateUser();

            using CottonDbContext dbContext = CreateDbContext();
            dbContext.Attach(user);
            DatabaseIntegrityVerifier verifier = CreateVerifier(protector, descriptor);

#pragma warning disable CS0618 // OBSOLETE TRANSITION: pin the operator-facing unsigned-row cutover error.
            DatabaseIntegritySignatureMissingException? exception = Assert.Throws<DatabaseIntegritySignatureMissingException>(() =>
                verifier.RequireValid(dbContext, user, "test.unsigned"));
#pragma warning restore CS0618

            Assert.That(exception!.Message, Does.Contain("Cotton 0.4.35"));
        }

        [Test]
        public void Verifier_RejectsInvalidSignatureAsIntegrityFailure()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            UserIntegrityDescriptor descriptor = new UserIntegrityDescriptor();
            User user = CreateUser();

            using CottonDbContext dbContext = CreateDbContext();
            dbContext.Users.Add(user);
            DatabaseIntegrityChangeSigner signer = new DatabaseIntegrityChangeSigner(
                protector,
                new DatabaseIntegrityDescriptorRegistry([descriptor]),
                NullDatabaseIntegrityFailureReporter.Instance);
            signer.SignPendingChanges(dbContext);
            user.Role = UserRole.Admin;
            DatabaseIntegrityVerifier verifier = CreateVerifier(protector, descriptor);

            Assert.Throws<DatabaseIntegrityException>(() =>
                verifier.RequireValid(dbContext, user, "test.invalid-signature"));
        }

        [Test]
        public void ChangeSigner_RejectsModifiedEntityWhenOriginalMacDoesNotMatch()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            UserIntegrityDescriptor descriptor = new UserIntegrityDescriptor();
            User tamperedUser = CreateUser();

            using CottonDbContext dbContext = CreateDbContext();
            EntityEntry<User> entry = dbContext.Attach(tamperedUser);
            entry.State = EntityState.Unchanged;
            byte[] originalMac = protector.Sign(tamperedUser, descriptor);
            tamperedUser.Role = UserRole.Admin;
            entry.Property(nameof(User.Role)).OriginalValue = UserRole.Admin;
            entry.Property(nameof(User.Role)).CurrentValue = UserRole.Admin;
            entry.Property(DatabaseIntegrityColumns.VersionProperty).OriginalValue = descriptor.SchemaVersion;
            entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = descriptor.SchemaVersion;
            entry.Property(DatabaseIntegrityColumns.MacProperty).OriginalValue = originalMac;
            entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue = originalMac;

            tamperedUser.FirstName = "Legitimate edit";
            dbContext.ChangeTracker.DetectChanges();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(entry.State, Is.EqualTo(EntityState.Modified));
                Assert.That(entry.OriginalValues[nameof(User.Role)], Is.EqualTo(UserRole.Admin));
                Assert.That(
                    protector.Verify(entry.OriginalValues.ToObject(), descriptor, originalMac),
                    Is.False);
            }
            DatabaseIntegrityChangeSigner signer = new DatabaseIntegrityChangeSigner(
                protector,
                new DatabaseIntegrityDescriptorRegistry([descriptor]),
                NullDatabaseIntegrityFailureReporter.Instance);

            Assert.Throws<DatabaseIntegrityException>(() => signer.SignPendingChanges(dbContext));
        }

        [Test]
        public void ChangeSigner_ReportsRequiredTransitionVersionWhenOriginalIntegrityMetadataIsMissing()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            UserIntegrityDescriptor descriptor = new UserIntegrityDescriptor();
            User user = CreateUser();

            using CottonDbContext dbContext = CreateDbContext();
            EntityEntry<User> entry = dbContext.Attach(user);
            entry.State = EntityState.Unchanged;
            user.Email = "alice.changed@example.test";
            dbContext.ChangeTracker.DetectChanges();

            DatabaseIntegrityChangeSigner signer = new DatabaseIntegrityChangeSigner(
                protector,
                new DatabaseIntegrityDescriptorRegistry([descriptor]),
                NullDatabaseIntegrityFailureReporter.Instance);

#pragma warning disable CS0618 // OBSOLETE TRANSITION: pin the operator-facing unsigned-row cutover error.
            DatabaseIntegritySignatureMissingException? exception =
                Assert.Throws<DatabaseIntegritySignatureMissingException>(() => signer.SignPendingChanges(dbContext));
#pragma warning restore CS0618

            Assert.That(exception!.Message, Does.Contain("Cotton 0.4.35"));
        }

        [Test]
        public void ChangeSigner_AcceptsModifiedEntityWhenOriginalMacMatches()
        {
            DatabaseIntegrityProtector protector = CreateProtector();
            UserIntegrityDescriptor descriptor = new UserIntegrityDescriptor();
            User user = CreateUser();

            using CottonDbContext dbContext = CreateDbContext();
            EntityEntry<User> entry = dbContext.Attach(user);
            entry.State = EntityState.Unchanged;
            byte[] originalMac = protector.Sign(user, descriptor);
            entry.Property(DatabaseIntegrityColumns.VersionProperty).OriginalValue = descriptor.SchemaVersion;
            entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = descriptor.SchemaVersion;
            entry.Property(DatabaseIntegrityColumns.MacProperty).OriginalValue = originalMac;
            entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue = originalMac;

            user.Email = "alice.changed@example.test";
            dbContext.ChangeTracker.DetectChanges();
            DatabaseIntegrityChangeSigner signer = new DatabaseIntegrityChangeSigner(
                protector,
                new DatabaseIntegrityDescriptorRegistry([descriptor]),
                NullDatabaseIntegrityFailureReporter.Instance);

            Assert.DoesNotThrow(() => signer.SignPendingChanges(dbContext));

            byte[] newMac = (byte[])entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(newMac, Is.Not.EqualTo(originalMac));
                Assert.That(protector.Verify(user, descriptor, newMac), Is.True);
                Assert.That(protector.Verify(user, descriptor, originalMac), Is.False);
            }
        }

    }
}
