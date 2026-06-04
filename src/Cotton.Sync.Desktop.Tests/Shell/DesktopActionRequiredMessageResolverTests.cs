// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Sdk;
using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Shell;

public sealed class DesktopActionRequiredMessageResolverTests
{
    [Test]
    public void FromStatus_ReturnsFirstPairError()
    {
        var status = new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Idle", null),
            new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Error", "Remote folder is unavailable."),
            new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Error", "Local folder is unavailable."),
        ]);

        string message = DesktopActionRequiredMessageResolver.FromStatus(status);

        Assert.That(message, Is.EqualTo("Remote folder is unavailable."));
    }

    [Test]
    public void FromStatus_ExplainsMissingDesktopSyncChangesApi()
    {
        var status = new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(
                Guid.NewGuid(),
                "Error",
                "Cotton API request GET /api/v1/sync/changes?since=0&limit=500 returned invalid JSON "
                + "with content type 'text/html' and status 200 (OK). Response: <!doctype html><html>App</html>"),
        ]);

        string message = DesktopActionRequiredMessageResolver.FromStatus(status);

        Assert.That(
            message,
            Is.EqualTo("This Cotton server does not expose the desktop sync changes API yet. Deploy the latest Cotton backend and retry sync."));
    }

    [Test]
    public void FromStatus_ReturnsEmptyWhenNoPairHasError()
    {
        var status = new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Idle", null),
            new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Syncing", string.Empty),
        ]);

        string message = DesktopActionRequiredMessageResolver.FromStatus(status);

        Assert.That(message, Is.Empty);
    }

    [Test]
    public void FromSelfTest_ReturnsFirstFailedCheckDetails()
    {
        var result = new DesktopSelfTestSnapshot(
        [
            new DesktopSelfTestItemSnapshot("Database", true, "Ready"),
            new DesktopSelfTestItemSnapshot("Server", false, "Cotton server not found."),
            new DesktopSelfTestItemSnapshot("Local root", false, "Missing folder."),
        ]);

        string message = DesktopActionRequiredMessageResolver.FromSelfTest(result);

        Assert.That(message, Is.EqualTo("Cotton server not found."));
    }

    [Test]
    public void FromSelfTest_ReturnsEmptyWhenSelfTestPassed()
    {
        var result = new DesktopSelfTestSnapshot(
        [
            new DesktopSelfTestItemSnapshot("Database", true, "Ready"),
        ]);

        string message = DesktopActionRequiredMessageResolver.FromSelfTest(result);

        Assert.That(message, Is.Empty);
    }

    [Test]
    public void FromException_ExplainsHtmlApiResponse()
    {
        var exception = new CottonApiException(
            HttpStatusCode.OK,
            "<!doctype html><html>App</html>",
            "Cotton API request GET /api/v1/settings returned invalid JSON with content type 'text/html' and status 200 (OK).");

        string message = DesktopActionRequiredMessageResolver.FromException(exception);

        Assert.That(
            message,
            Is.EqualTo("Cotton API returned a web page instead of JSON. Check the server URL or backend deployment and retry."));
    }
}
