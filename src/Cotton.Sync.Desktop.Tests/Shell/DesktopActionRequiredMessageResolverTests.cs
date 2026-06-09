// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Sdk;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Local;

namespace Cotton.Sync.Desktop.Tests.Shell
{
    public class DesktopActionRequiredMessageResolverTests
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
        public void FromStatus_ExplainsLocalPermissionDeniedMessage()
        {
            var status = new DesktopSyncStatusSnapshot(
            [
                new DesktopSyncPairStatusSnapshot(
                    Guid.NewGuid(),
                    "Error",
                    "Local file 'Locked/report.docx' cannot be read because permission was denied."),
            ]);

            string message = DesktopActionRequiredMessageResolver.FromStatus(status);

            Assert.That(
                message,
                Is.EqualTo("Cotton Sync cannot access 'Locked/report.docx'. Grant file permissions and retry sync."));
        }

        [Test]
        public void FromStatus_ExplainsDiskFullMessage()
        {
            var status = new DesktopSyncStatusSnapshot(
            [
                new DesktopSyncPairStatusSnapshot(Guid.NewGuid(), "Error", "There is not enough space on the disk."),
            ]);

            string message = DesktopActionRequiredMessageResolver.FromStatus(status);

            Assert.That(
                message,
                Is.EqualTo("This computer does not have enough free disk space for sync. Free space and retry."));
        }

        [Test]
        public void FromStatus_ExplainsLocalFileUnavailableMessage()
        {
            var status = new DesktopSyncStatusSnapshot(
            [
                new DesktopSyncPairStatusSnapshot(
                    Guid.NewGuid(),
                    "Error",
                    "Local file 'Drafts/report.docx' could not be scanned safely."),
            ]);

            string message = DesktopActionRequiredMessageResolver.FromStatus(status);

            Assert.That(
                message,
                Is.EqualTo("Cotton Sync cannot read 'Drafts/report.docx' yet. Close the app using it or wait for it to finish saving, then retry sync."));
        }

        [Test]
        public void FromStatus_ExplainsMissingSyncStateTable()
        {
            var status = new DesktopSyncStatusSnapshot(
            [
                new DesktopSyncPairStatusSnapshot(
                    Guid.NewGuid(),
                    "Error",
                    "SQLite Error 1: 'no such table: sync_change_cursors'."),
            ]);

            string message = DesktopActionRequiredMessageResolver.FromStatus(status);

            Assert.That(
                message,
                Is.EqualTo("Local sync state database is unavailable. Run diagnostics and restart Cotton Sync."));
        }

        [Test]
        public void FromStatus_NormalizesEmbeddedUploadBadRequestProblemDetails()
        {
            var status = new DesktopSyncStatusSnapshot(
            [
                new DesktopSyncPairStatusSnapshot(
                    Guid.NewGuid(),
                    "Error",
                    "Cotton API request POST /api/v1/files/from-chunks failed with status 400 (BadRequest). "
                    + "Response: {\"type\":\"https://tools.ietf.org/html/rfc7231#section-6.5.1\","
                    + "\"title\":\"Bad Request\",\"status\":400,\"detail\":\"Bad request\","
                    + "\"instance\":\"/api/v1/files/from-chunks\",\"traceId\":\"00-test\"}"),
            ]);

            string message = DesktopActionRequiredMessageResolver.FromStatus(status);

            Assert.That(
                message,
                Is.EqualTo("Remote upload was rejected by Cotton Cloud. Check diagnostics and retry."));
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
        public void FromException_ExplainsLocalPermissionDeniedException()
        {
            var exception = new LocalFilePermissionDeniedException(
                "Locked/report.docx",
                "/home/qa/Cotton/Locked/report.docx",
                "owner does not have read permission");

            string message = DesktopActionRequiredMessageResolver.FromException(exception);

            Assert.That(
                message,
                Is.EqualTo("Cotton Sync cannot access 'Locked/report.docx'. Grant file permissions and retry sync."));
        }

        [Test]
        public void FromException_ExplainsDiskFullException()
        {
            var exception = new IOException("No space left on device");

            string message = DesktopActionRequiredMessageResolver.FromException(exception);

            Assert.That(
                message,
                Is.EqualTo("This computer does not have enough free disk space for sync. Free space and retry."));
        }

        [Test]
        public void FromException_ExplainsLocalFileUnavailableException()
        {
            var exception = new LocalFileUnavailableException(
                "Drafts/report.docx",
                "/home/qa/Cotton/Drafts/report.docx",
                "the file changed during scanning.");

            string message = DesktopActionRequiredMessageResolver.FromException(exception);

            Assert.That(
                message,
                Is.EqualTo("Cotton Sync cannot read 'Drafts/report.docx' yet. Close the app using it or wait for it to finish saving, then retry sync."));
        }

        [Test]
        public void FromException_ExplainsRemoteQuotaExceeded()
        {
            var exception = new CottonApiException(
                (HttpStatusCode)507,
                null,
                "Cotton API request failed with status 507.");

            string message = DesktopActionRequiredMessageResolver.FromException(exception);

            Assert.That(
                message,
                Is.EqualTo("Remote storage quota exceeded. Free space in Cotton Cloud or choose a smaller sync folder."));
        }

        [Test]
        public void FromException_ExplainsRemoteUploadTooLarge()
        {
            var exception = new CottonApiException(
                HttpStatusCode.RequestEntityTooLarge,
                null,
                "Cotton API request failed with status 413.");

            string message = DesktopActionRequiredMessageResolver.FromException(exception);

            Assert.That(
                message,
                Is.EqualTo("Remote upload was rejected because it is larger than the server limit."));
        }

        [Test]
        public void FromException_ExplainsMissingSyncStateTable()
        {
            var exception = new InvalidOperationException("SQLite Error 1: 'no such table: sync_entries'.");

            string message = DesktopActionRequiredMessageResolver.FromException(exception);

            Assert.That(
                message,
                Is.EqualTo("Local sync state database is unavailable. Run diagnostics and restart Cotton Sync."));
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

        [Test]
        public void FromException_UsesReadableFallbackWhenExceptionHasNoMessage()
        {
            var exception = new InvalidOperationException(string.Empty);

            string message = DesktopActionRequiredMessageResolver.FromException(exception);

            Assert.That(message, Is.EqualTo("Operation could not be completed. Check diagnostics and retry."));
        }
    }
}
