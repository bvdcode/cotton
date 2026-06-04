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
    public void FromStatus_ExplainsRawJsonParserHtmlStartMessage()
    {
        var status = new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(
                Guid.NewGuid(),
                "Error",
                "'<' is an invalid start of a value. Path: $ | LineNumber: 0 | BytePositionInLine: 0."),
        ]);

        string message = DesktopActionRequiredMessageResolver.FromStatus(status);

        Assert.That(
            message,
            Is.EqualTo("Cotton API returned a web page instead of JSON. Check the server URL or backend deployment and retry."));
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
    public void FromStatus_ReturnsGenericMessageWhenPairErrorHasNoDetails()
    {
        var status = new DesktopSyncStatusSnapshot(
        [
            new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Error", null),
        ]);

        string message = DesktopActionRequiredMessageResolver.FromStatus(status);

        Assert.That(
            message,
            Is.EqualTo("One or more sync folders reported an error. Check diagnostics and retry."));
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

    [Test]
    public void FromException_ExplainsRawJsonParserHtmlStartMessage()
    {
        var exception = new InvalidOperationException(
            "'<' is an invalid start of a value. Path: $ | LineNumber: 0 | BytePositionInLine: 0.");

        string message = DesktopActionRequiredMessageResolver.FromException(exception);

        Assert.That(
            message,
            Is.EqualTo("Cotton API returned a web page instead of JSON. Check the server URL or backend deployment and retry."));
    }

    [Test]
    public void FromException_UsesHumanTotpRequiredMessage()
    {
        var exception = new CottonApiException(
            HttpStatusCode.Forbidden,
            "{\"success\":false,\"message\":\"Two-factor authentication code is required\"}",
            "Cotton API request POST /api/v1/auth/login failed with status 403 (Forbidden).");

        string message = DesktopActionRequiredMessageResolver.FromException(exception);

        Assert.That(message, Is.EqualTo("Enter the 2FA code for this account."));
    }

    [Test]
    public void FromException_UsesHumanInvalidCredentialsMessage()
    {
        var exception = new CottonApiException(
            HttpStatusCode.Unauthorized,
            "{\"success\":false,\"message\":\"User not found\"}",
            "Cotton API request POST /api/v1/auth/login failed with status 401 (Unauthorized).");

        string message = DesktopActionRequiredMessageResolver.FromException(exception);

        Assert.That(message, Is.EqualTo("Invalid username or password."));
    }

    [Test]
    public void FromException_UsesHumanInvalidPasswordMessageForForbiddenServerResponse()
    {
        var exception = new CottonApiException(
            HttpStatusCode.Forbidden,
            "{\"success\":false,\"message\":\"Invalid password\"}",
            "Cotton API request POST /api/v1/auth/login failed with status 403 (Forbidden).");

        string message = DesktopActionRequiredMessageResolver.FromException(exception);

        Assert.That(message, Is.EqualTo("Invalid username or password."));
    }

    [Test]
    public void FromException_UsesHumanInvalidTotpMessage()
    {
        var exception = new CottonApiException(
            HttpStatusCode.Forbidden,
            "{\"success\":false,\"message\":\"Invalid two-factor authentication code\"}",
            "Cotton API request POST /api/v1/auth/login failed with status 403 (Forbidden).");

        string message = DesktopActionRequiredMessageResolver.FromException(exception);

        Assert.That(message, Is.EqualTo("Invalid 2FA code."));
    }

    [Test]
    public void FromException_UsesHumanTotpLockoutMessage()
    {
        var exception = new CottonApiException(
            HttpStatusCode.Forbidden,
            "{\"success\":false,\"message\":\"Maximum number of TOTP verification attempts exceeded\"}",
            "Cotton API request POST /api/v1/auth/login failed with status 403 (Forbidden).");

        string message = DesktopActionRequiredMessageResolver.FromException(exception);

        Assert.That(message, Is.EqualTo("Too many invalid 2FA attempts. Try again later or sign in from the web app."));
    }
}
