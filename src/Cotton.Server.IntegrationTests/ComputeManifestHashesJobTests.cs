// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class ComputeManifestHashesJobTests
{
    [Test]
    public async Task ReportHashMismatchAsync_RetriesAfterNotificationFailure()
    {
        Guid manifestId = Guid.NewGuid();
        FailingOnceNotificationsProvider notifications = new();
        ComputeManifestHashesJob job = new(
            null!,
            null!,
            notifications,
            NullLogger<ComputeManifestHashesJob>.Instance,
            null!);
        ManifestHashMismatchNotificationTarget target = new(
            Guid.NewGuid(),
            "mismatched.bin");

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await job.ReportHashMismatchAsync(
                manifestId,
                [target],
                "proposed",
                "computed"));

        await job.ReportHashMismatchAsync(
            manifestId,
            [target],
            "proposed",
            "computed");
        await job.ReportHashMismatchAsync(
            manifestId,
            [target],
            "proposed",
            "computed");

        Assert.That(notifications.NotificationAttempts, Is.EqualTo(2));
    }

    private class FailingOnceNotificationsProvider : INotificationsProvider
    {
        public int NotificationAttempts { get; private set; }

        public Task<bool> SendEmailAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl,
            string? recipientEmail = null)
        {
            return Task.FromResult(true);
        }

        public Task SendSmtpTestEmailAsync(Guid userId, string serverBaseUrl)
        {
            return Task.CompletedTask;
        }

        public Task SendNotificationAsync(
            Guid userId,
            string title,
            string? content = null,
            NotificationPriority priority = NotificationPriority.None,
            Dictionary<string, string>? metadata = null)
        {
            NotificationAttempts++;
            return NotificationAttempts == 1
                ? Task.FromException(new InvalidOperationException("Notification delivery failed."))
                : Task.CompletedTask;
        }
    }
}
