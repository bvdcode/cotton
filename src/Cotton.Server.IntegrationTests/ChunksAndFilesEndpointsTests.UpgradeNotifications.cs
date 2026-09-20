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
                HotfixBackfillContentTypeJob job = ActivatorUtilities.CreateInstance<HotfixBackfillContentTypeJob>(scope.ServiceProvider);
                await job.Execute(null!);
                Assert.That(await db.Notifications.CountAsync(), Is.Zero);
                db.Users.Add(CreateUpgradeNotificationUser("lateadmin", UserRole.Admin));
                await db.SaveChangesAsync();
            }

            for (int run = 0; run < 2; run++)
            {
                await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
                HotfixBackfillContentTypeJob job = ActivatorUtilities.CreateInstance<HotfixBackfillContentTypeJob>(scope.ServiceProvider);
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

            HotfixBackfillContentTypeJob job = ActivatorUtilities.CreateInstance<HotfixBackfillContentTypeJob>(scope.ServiceProvider);
            Assert.ThrowsAsync<DatabaseIntegrityException>(() => job.Execute(null!));
            string title = NotificationTemplates.UpgradePreparationCompletedTitle("0.6");
            Assert.That(await db.Notifications.AnyAsync(notification => notification.Title == title), Is.False);
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
