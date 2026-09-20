// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Notifications;
using Cotton.Localization;
using EasyExtensions.Mediator;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task UpgradePreparation_NotifiesAdminsOnceAcrossScopes_AndSeparatesJobsAndVersions()
        {
            Guid[] adminIds;
            await using (AsyncServiceScope scope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                User first = CreateUpgradeNotificationUser("upgradeadminone", UserRole.Admin);
                User second = CreateUpgradeNotificationUser("upgradeadmintwo", UserRole.Admin);
                User member = CreateUpgradeNotificationUser("upgrademember", UserRole.User);
                db.Users.AddRange(first, second, member);
                await db.SaveChangesAsync();
                adminIds = [first.Id, second.Id];

                IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new NotifyUpgradePreparationCompletedRequest("FirstUpgradeJob", "1.2"), CancellationToken.None);
                List<Notification> notifications = await db.Notifications.ToListAsync();
                Assert.That(notifications.Select(notification => notification.UserId), Is.EquivalentTo(adminIds));
                foreach (Notification notification in notifications)
                {
                    Assert.That(notification.Metadata![NotificationTemplateMetadata.TitleKey],
                        Is.EqualTo(NotificationTemplateKeys.UpgradePreparationCompletedTitle));
                    notification.ReadAt = DateTime.UtcNow;
                }
                await db.SaveChangesAsync();
            }

            await using AsyncServiceScope nextScope = _factory!.Services.CreateAsyncScope();
            CottonDbContext nextDb = nextScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            IMediator nextMediator = nextScope.ServiceProvider.GetRequiredService<IMediator>();
            await nextMediator.Send(new NotifyUpgradePreparationCompletedRequest("FirstUpgradeJob", "1.2"), CancellationToken.None);
            Assert.That(await nextDb.Notifications.CountAsync(), Is.EqualTo(2));
            await nextMediator.Send(new NotifyUpgradePreparationCompletedRequest("SecondUpgradeJob", "1.2"), CancellationToken.None);
            await nextMediator.Send(new NotifyUpgradePreparationCompletedRequest("FirstUpgradeJob", "2.0"), CancellationToken.None);
            Assert.That(await nextDb.Notifications.CountAsync(), Is.EqualTo(6));
        }

        [Test]
        public async Task ContentTypeUpgrade_RetriesNotificationForNewAdmins_WhenNoRowsNeedChanges()
        {
            await using (AsyncServiceScope scope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                PrepareUpgradeTo06Job job = ActivatorUtilities.CreateInstance<PrepareUpgradeTo06Job>(scope.ServiceProvider);
                await job.Execute(null!);
                Assert.That(await db.Notifications.CountAsync(), Is.Zero);
                db.Users.Add(CreateUpgradeNotificationUser("lateadmin", UserRole.Admin));
                await db.SaveChangesAsync();
            }

            for (int run = 0; run < 2; run++)
            {
                await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
                PrepareUpgradeTo06Job job = ActivatorUtilities.CreateInstance<PrepareUpgradeTo06Job>(scope.ServiceProvider);
                await job.Execute(null!);
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                Assert.That(await db.Notifications.CountAsync(), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task ContentTypeUpgrade_DoesNotAnnounceCompletion_WhenManifestMigrationFails()
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            db.Users.Add(CreateUpgradeNotificationUser("upgradeadmin", UserRole.Admin));
            FileManifest manifest = new()
            {
                ProposedContentHash = SHA256.HashData("upgrade-manifest"u8),
                ContentType = "text/plain",
                SizeBytes = 10,
            };
            db.FileManifests.Add(manifest);
            await db.SaveChangesAsync();
            await db.FileManifests.Where(entry => entry.Id == manifest.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.SizeBytes, 20));
            db.ChangeTracker.Clear();

            PrepareUpgradeTo06Job job = ActivatorUtilities.CreateInstance<PrepareUpgradeTo06Job>(scope.ServiceProvider);
            Assert.ThrowsAsync<DatabaseIntegrityException>(() => job.Execute(null!));
            string title = NotificationTemplates.UpgradePreparationCompletedTitle("0.6");
            Assert.That(await db.Notifications.AnyAsync(notification => notification.Title == title), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ContentTypeUpgrade_NotifiesOnlyAfterEmptyPreviewCleanup(bool tampered)
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            db.Users.Add(CreateUpgradeNotificationUser("emptyfileadmin", UserRole.Admin));
            FileManifest manifest = new()
            {
                ProposedContentHash = Hasher.FromHexStringHash(Hasher.ZeroHashHexString),
                ContentType = string.Empty,
                SizeBytes = 0,
                SmallFilePreviewHash = SHA256.HashData("old small preview"u8),
                SmallFilePreviewHashEncrypted = [1, 2, 3],
                LargeFilePreviewHash = SHA256.HashData("old large preview"u8),
                PreviewGenerationError = "Previous attempt failed",
                PreviewGeneratorId = "android-package",
                PreviewGeneratorVersion = -1,
            };
            db.FileManifests.Add(manifest);
            await db.SaveChangesAsync();
            if (tampered)
            {
                byte[] corruptHash = SHA256.HashData("tampered preview"u8);
                await db.FileManifests.Where(entry => entry.Id == manifest.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.SmallFilePreviewHash, corruptHash));
            }
            db.ChangeTracker.Clear();

            PrepareUpgradeTo06Job job = ActivatorUtilities.CreateInstance<PrepareUpgradeTo06Job>(scope.ServiceProvider);
            string title = NotificationTemplates.UpgradePreparationCompletedTitle("0.6");
            if (tampered)
            {
                Assert.ThrowsAsync<DatabaseIntegrityException>(() => job.Execute(null!));
                Assert.That(await db.Notifications.AnyAsync(notification => notification.Title == title), Is.False);
                Assert.That(await db.FileManifests.Where(entry => entry.Id == manifest.Id)
                    .Select(entry => entry.SmallFilePreviewHash).SingleAsync(), Is.Not.Null);
                return;
            }

            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new NotifyUpgradePreparationCompletedRequest("HotfixBackfillContentTypeJob", "0.6"), CancellationToken.None);
            await job.Execute(null!);
            db.ChangeTracker.Clear();
            FileManifest cleaned = await db.FileManifests.SingleAsync(entry => entry.Id == manifest.Id);
            Assert.Multiple(() =>
            {
                Assert.That(cleaned.SmallFilePreviewHash, Is.Null);
                Assert.That(cleaned.SmallFilePreviewHashEncrypted, Is.Null);
                Assert.That(cleaned.LargeFilePreviewHash, Is.Null);
                Assert.That(cleaned.PreviewGenerationError, Is.Null);
                Assert.That(cleaned.PreviewGeneratorId, Is.Null);
                Assert.That(cleaned.PreviewGeneratorVersion, Is.EqualTo(PreviewGeneratorProvider.DefaultGeneratorVersion));
            });
            scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>()
                .RequireValid(db, cleaned, "test.upgrade-empty-preview");
            string content = NotificationTemplates.UpgradePreparationCompletedContent(nameof(PrepareUpgradeTo06Job), "0.6");
            Notification notification = await db.Notifications.SingleAsync(entry => entry.Title == title && entry.Content == content);
            Assert.That(notification.CreatedAt, Is.GreaterThanOrEqualTo(cleaned.UpdatedAt));
            DateTime? updatedAt = cleaned.UpdatedAt;
            await job.Execute(null!);
            Assert.That(await db.Notifications.CountAsync(entry => entry.Title == title), Is.EqualTo(2));
            Assert.That(await db.Notifications.CountAsync(entry => entry.Title == title && entry.Content == content), Is.EqualTo(1));
            Assert.That(await db.FileManifests.Where(entry => entry.Id == manifest.Id)
                .Select(entry => entry.UpdatedAt).SingleAsync(), Is.EqualTo(updatedAt));
        }

        private static User CreateUpgradeNotificationUser(string username, UserRole role)
        {
            return new User
            {
                Username = username,
                PasswordPhc = "unused",
                WebDavTokenPhc = "unused",
                Role = role,
            };
        }
    }
}
