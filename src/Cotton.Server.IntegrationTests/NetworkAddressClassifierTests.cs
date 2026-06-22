// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Models;
using Cotton.Server.Services;
using Microsoft.Extensions.Primitives;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests;

public class NetworkAddressClassifierTests
{
    [TestCase("10.0.0.101")]
    [TestCase("172.16.0.1")]
    [TestCase("172.31.255.255")]
    [TestCase("192.168.1.10")]
    [TestCase("169.254.10.20")]
    [TestCase("127.0.0.1")]
    [TestCase("::1")]
    [TestCase("::ffff:127.0.0.1")]
    [TestCase("fe80::1")]
    [TestCase("fd12:3456:789a::1")]
    [TestCase("::ffff:192.168.1.10")]
    public void IsLocalNetworkAddress_DetectsLocalRanges(string ipAddress)
    {
        Assert.That(
            NetworkAddressClassifier.IsLocalNetworkAddress(IPAddress.Parse(ipAddress)),
            Is.True);
    }

    [TestCase("8.8.8.8")]
    [TestCase("172.32.0.1")]
    [TestCase("2001:4860:4860::8888")]
    public void IsLocalNetworkAddress_IgnoresPublicRanges(string ipAddress)
    {
        Assert.That(
            NetworkAddressClassifier.IsLocalNetworkAddress(IPAddress.Parse(ipAddress)),
            Is.False);
    }

    [Test]
    public async Task SharedFileDownloadedNotification_UsesLocalNetworkLocationForPrivateIp()
    {
        var notifications = new RecordingNotificationsProvider();
        var geoLookup = new RecordingGeoLookupService();

        await notifications.SendSharedFileDownloadedNotificationAsync(
            geoLookup,
            Guid.NewGuid(),
            "Booklet R006262212.pdf",
            IPAddress.Parse("10.0.0.101"),
            new StringValues("Windows"));

        Assert.That(notifications.Sent, Has.Count.EqualTo(1));
        SentNotification sent = notifications.Sent.Single();
        Assert.Multiple(() =>
        {
            Assert.That(geoLookup.LookupCount, Is.Zero);
            Assert.That(sent.Metadata["location"], Is.EqualTo("local network"));
            Assert.That(sent.Content, Does.Contain("local network"));
            Assert.That(sent.Content, Does.Not.Contain("Unknown, Unknown, Unknown"));
        });
    }

    private class RecordingGeoLookupService : IGeoLookupService
    {
        public int LookupCount { get; private set; }

        public Task<GeoLookupResult?> TryLookupAsync(
            IPAddress ipAddress,
            CancellationToken cancellationToken = default)
        {
            LookupCount++;
            return Task.FromResult<GeoLookupResult?>(
                new GeoLookupResult("Country", "Region", "City"));
        }

        public Task<CustomGeoLookupTestResult> TestCustomLookupAsync(
            string serverBaseUrl,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private class RecordingNotificationsProvider : INotificationsProvider
    {
        public List<SentNotification> Sent { get; } = [];

        public Task<bool> SendEmailAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl)
        {
            return Task.FromResult(false);
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
            Sent.Add(new SentNotification(
                userId,
                title,
                content,
                priority,
                metadata ?? []));

            return Task.CompletedTask;
        }
    }

    private record SentNotification(
        Guid UserId,
        string Title,
        string? Content,
        NotificationPriority Priority,
        Dictionary<string, string> Metadata);
}
