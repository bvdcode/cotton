// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Text.Json;
using Cotton.Sdk;
using Cotton.Sync.Local;

namespace Cotton.Sync.Desktop.Shell
{

    internal static class DesktopActionRequiredMessageResolver
    {
        internal const string MissingDesktopSyncChangesApiMessage =
            "This Cotton server does not expose the desktop sync changes API yet. Deploy the latest Cotton backend and retry sync.";

        private const string HtmlInsteadOfJsonMessage =
            "Cotton API returned a web page instead of JSON. Check the server URL or backend deployment and retry.";

        private const string GenericSyncErrorMessage =
            "One or more sync folders reported an error. Check diagnostics and retry.";

        private const int InsufficientStorageStatusCode = 507;

        private const string DiskFullMessage =
            "This computer does not have enough free disk space for sync. Free space and retry.";

        private const string RemoteQuotaExceededMessage =
            "Remote storage quota exceeded. Free space in Cotton Cloud or choose a smaller sync folder.";

        private const string RemoteUploadTooLargeMessage =
            "Remote upload was rejected because it is larger than the server limit.";

        private const string RemoteUploadRejectedMessage =
            "Remote upload was rejected by Cotton Cloud. Check diagnostics and retry.";

        private const string CottonApiRejectedRequestMessage =
            "Cotton API rejected the request. Check diagnostics and retry.";

        private const string LocalPermissionDeniedMessage =
            "Cotton Sync cannot access one of the local files. Grant file permissions and retry sync.";

        private const string LocalFileUnavailableMessage =
            "Cotton Sync cannot read one of the local files yet. Close the app using it or wait for it to finish saving, then retry sync.";

        private const string LocalSyncStateDatabaseUnavailableMessage =
            "Local sync state database is unavailable. Run diagnostics and restart Cotton Sync.";

        public static string FromStatus(DesktopSyncStatusSnapshot status)
        {
            ArgumentNullException.ThrowIfNull(status);
            DesktopSyncPairStatusSnapshot? failedPair = status.SyncPairs
                .FirstOrDefault(static pair => !string.IsNullOrWhiteSpace(pair.LastError));
            if (failedPair is not null)
            {
                return Normalize(failedPair.LastError) ?? GenericSyncErrorMessage;
            }

            return status.SyncPairs.Any(static pair => string.Equals(pair.Status, "Error", StringComparison.Ordinal))
                ? GenericSyncErrorMessage
                : string.Empty;
        }

        public static string FromSyncPairStatus(DesktopSyncPairStatusSnapshot pair)
        {
            return string.Equals(pair.Status, "Error", StringComparison.Ordinal)
                ? Normalize(pair.LastError) ?? GenericSyncErrorMessage
                : string.Empty;
        }

        public static string FromSelfTest(DesktopSelfTestSnapshot selfTest)
        {
            ArgumentNullException.ThrowIfNull(selfTest);
            if (selfTest.Passed)
            {
                return string.Empty;
            }

            return Normalize(selfTest.Items.FirstOrDefault(static item => !item.Passed)?.Details)
                ?? "Self-test failed.";
        }

        public static string FromException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            if (exception is CottonApiException apiException)
            {
                return NormalizeApiException(apiException)
                    ?? "Cotton API request failed. Check diagnostics and retry.";
            }

            if (exception is LocalFilePermissionDeniedException permissionDeniedException)
            {
                return CreateLocalPermissionDeniedMessage(permissionDeniedException.RelativePath);
            }

            if (exception is LocalFileUnavailableException unavailableException)
            {
                return CreateLocalFileUnavailableMessage(unavailableException.RelativePath);
            }

            if (exception is IOException && LooksLikeDiskFull(exception.Message))
            {
                return DiskFullMessage;
            }

            return Normalize(exception.Message) ?? "Operation could not be completed. Check diagnostics and retry.";
        }

        internal static bool IsMissingDesktopSyncChangesApi(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return string.Equals(message, MissingDesktopSyncChangesApiMessage, StringComparison.Ordinal)
                || LooksLikeMissingDesktopSyncChangesApi(message, responseBody: null);
        }

        private static string? NormalizeApiException(CottonApiException exception)
        {
            if (IsQuotaExceededStatus(exception.StatusCode))
            {
                return RemoteQuotaExceededMessage;
            }

            if (exception.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge)
            {
                return RemoteUploadTooLargeMessage;
            }

            string? responseMessage = ExtractResponseMessage(exception.ResponseBody);
            string? authMessage = NormalizeAuthMessage(responseMessage);
            if (authMessage is not null)
            {
                return authMessage;
            }

            return Normalize(responseMessage)
                ?? Normalize(exception.Message, exception.ResponseBody);
        }

        private static bool IsQuotaExceededStatus(System.Net.HttpStatusCode? statusCode)
        {
            return statusCode.HasValue && (int)statusCode.Value == InsufficientStorageStatusCode;
        }

        private static string? Normalize(string? message, string? responseBody = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            if (LooksLikeMissingDesktopSyncChangesApi(message, responseBody))
            {
                return MissingDesktopSyncChangesApiMessage;
            }

            if (LooksLikeHtmlInsteadOfJson(message, responseBody))
            {
                return HtmlInsteadOfJsonMessage;
            }

            if (LooksLikeDiskFull(message))
            {
                return DiskFullMessage;
            }

            if (LooksLikeLocalPermissionDenied(message))
            {
                return CreateLocalPermissionDeniedMessage(ExtractSingleQuotedPath(message));
            }

            if (LooksLikeLocalFileUnavailable(message))
            {
                return CreateLocalFileUnavailableMessage(ExtractSingleQuotedPath(message));
            }

            if (LooksLikeLocalSyncStateDatabaseUnavailable(message))
            {
                return LocalSyncStateDatabaseUnavailableMessage;
            }

            string? apiFailureMessage = NormalizeEmbeddedApiFailureMessage(message);
            if (apiFailureMessage is not null)
            {
                return apiFailureMessage;
            }

            return DesktopUserMessageFormatter.Compact(message);
        }

        private static string CreateLocalPermissionDeniedMessage(string? relativePath)
        {
            string message = string.IsNullOrWhiteSpace(relativePath)
                ? LocalPermissionDeniedMessage
                : "Cotton Sync cannot access '" + relativePath + "'. Grant file permissions and retry sync.";
            return DesktopUserMessageFormatter.Compact(message);
        }

        private static string CreateLocalFileUnavailableMessage(string? relativePath)
        {
            string message = string.IsNullOrWhiteSpace(relativePath)
                ? LocalFileUnavailableMessage
                : "Cotton Sync cannot read '" + relativePath + "' yet. Close the app using it or wait for it to finish saving, then retry sync.";
            return DesktopUserMessageFormatter.Compact(message);
        }

        private static string? NormalizeAuthMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            string normalized = message.Trim();
            if (string.Equals(normalized, "Two-factor authentication code is required", StringComparison.OrdinalIgnoreCase))
            {
                return "Enter the 2FA code for this account.";
            }

            if (string.Equals(normalized, "Invalid two-factor authentication code", StringComparison.OrdinalIgnoreCase))
            {
                return "Invalid 2FA code.";
            }

            if (string.Equals(normalized, "Maximum number of TOTP verification attempts exceeded", StringComparison.OrdinalIgnoreCase))
            {
                return "Too many invalid 2FA attempts. Try again later or sign in from the web app.";
            }

            if (string.Equals(normalized, "User not found", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Invalid password", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Invalid username or password", StringComparison.OrdinalIgnoreCase))
            {
                return "Invalid username or password.";
            }

            return normalized;
        }

        private static string? ExtractResponseMessage(string? responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody) || LooksLikeHtml(responseBody))
            {
                return null;
            }

            string trimmed = responseBody.Trim();
            if (!trimmed.StartsWith('{'))
            {
                return trimmed;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(trimmed);
                JsonElement root = document.RootElement;
                if (TryGetStringProperty(root, "message", out string? message)
                    || TryGetStringProperty(root, "detail", out message)
                    || TryGetStringProperty(root, "title", out message))
                {
                    return message;
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        private static string? NormalizeEmbeddedApiFailureMessage(string message)
        {
            if (!message.Contains("Cotton API request", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (LooksLikeCreateFileFromChunksBadRequest(message))
            {
                return RemoteUploadRejectedMessage;
            }

            string? responseMessage = ExtractResponseMessage(ExtractEmbeddedResponseBody(message));
            string? authMessage = NormalizeAuthMessage(responseMessage);
            if (authMessage is not null)
            {
                return authMessage;
            }

            if (IsGenericBadRequestMessage(responseMessage))
            {
                return CottonApiRejectedRequestMessage;
            }

            return responseMessage;
        }

        private static string? ExtractEmbeddedResponseBody(string message)
        {
            const string marker = "Response:";
            int markerIndex = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return null;
            }

            string responseBody = message[(markerIndex + marker.Length)..].Trim();
            if (!responseBody.StartsWith('{'))
            {
                return responseBody;
            }

            int endIndex = responseBody.LastIndexOf('}');
            return endIndex >= 0 ? responseBody[..(endIndex + 1)] : responseBody;
        }

        private static bool LooksLikeCreateFileFromChunksBadRequest(string message)
        {
            return message.Contains("POST /api/v1/files/from-chunks", StringComparison.OrdinalIgnoreCase)
                && message.Contains("status 400", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGenericBadRequestMessage(string? message)
        {
            return !string.IsNullOrWhiteSpace(message)
                && string.Equals(message.Trim(), "Bad request", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetStringProperty(JsonElement element, string propertyName, out string? value)
        {
            value = null;
            if (!element.TryGetProperty(propertyName, out JsonElement property)
                || property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = property.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool LooksLikeMissingDesktopSyncChangesApi(string message, string? responseBody)
        {
            return message.Contains("GET /api/v1/sync/changes", StringComparison.Ordinal)
                && LooksLikeHtmlInsteadOfJson(message, responseBody);
        }

        private static bool LooksLikeHtmlInsteadOfJson(string message, string? responseBody)
        {
            return (message.Contains("invalid JSON", StringComparison.OrdinalIgnoreCase)
                    && (message.Contains("text/html", StringComparison.OrdinalIgnoreCase)
                        || LooksLikeHtml(responseBody)
                        || LooksLikeHtml(message)))
                || LooksLikeJsonParserHtmlStartMessage(message);
        }

        private static bool LooksLikeJsonParserHtmlStartMessage(string message)
        {
            return message.Contains("'<' is an invalid start of a value", StringComparison.OrdinalIgnoreCase)
                || message.Contains("\"<\" is an invalid start of a value", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeDiskFull(string message)
        {
            return message.Contains("no space left on device", StringComparison.OrdinalIgnoreCase)
                || message.Contains("not enough space", StringComparison.OrdinalIgnoreCase)
                || message.Contains("not enough disk space", StringComparison.OrdinalIgnoreCase)
                || message.Contains("disk full", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeLocalPermissionDenied(string message)
        {
            return (message.Contains("local file", StringComparison.OrdinalIgnoreCase)
                    && message.Contains("permission was denied", StringComparison.OrdinalIgnoreCase))
                || (message.Contains("access to the path", StringComparison.OrdinalIgnoreCase)
                    && message.Contains("is denied", StringComparison.OrdinalIgnoreCase));
        }

        private static bool LooksLikeLocalFileUnavailable(string message)
        {
            return message.Contains("local file", StringComparison.OrdinalIgnoreCase)
                && message.Contains("could not be scanned safely", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeLocalSyncStateDatabaseUnavailable(string message)
        {
            return message.Contains("SQLite Error", StringComparison.OrdinalIgnoreCase)
                && message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
                && (message.Contains("sync_change_cursors", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("sync_entries", StringComparison.OrdinalIgnoreCase));
        }

        private static string? ExtractSingleQuotedPath(string message)
        {
            int start = message.IndexOf('\'');
            if (start < 0)
            {
                return null;
            }

            int end = message.IndexOf('\'', start + 1);
            return end > start + 1 ? message[(start + 1)..end] : null;
        }

        private static bool LooksLikeHtml(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.TrimStart();
            return trimmed.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
        }
    }
}
