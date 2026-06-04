// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk;

namespace Cotton.Sync.Desktop.Shell;

internal static class DesktopActionRequiredMessageResolver
{
    private const string MissingDesktopSyncChangesApiMessage =
        "This Cotton server does not expose the desktop sync changes API yet. Deploy the latest Cotton backend and retry sync.";

    private const string HtmlInsteadOfJsonMessage =
        "Cotton API returned a web page instead of JSON. Check the server URL or backend deployment and retry.";

    public static string FromStatus(DesktopSyncStatusSnapshot status)
    {
        ArgumentNullException.ThrowIfNull(status);
        DesktopSyncPairStatusSnapshot? failedPair = status.SyncPairs
            .FirstOrDefault(static pair => !string.IsNullOrWhiteSpace(pair.LastError));
        return Normalize(failedPair?.LastError) ?? string.Empty;
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
            return Normalize(apiException.Message, apiException.ResponseBody)
                ?? "Cotton API request failed. Check diagnostics and retry.";
        }

        return Normalize(exception.Message) ?? "Action failed. Check diagnostics and retry.";
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

        return message;
    }

    private static bool LooksLikeMissingDesktopSyncChangesApi(string message, string? responseBody)
    {
        return message.Contains("GET /api/v1/sync/changes", StringComparison.Ordinal)
            && LooksLikeHtmlInsteadOfJson(message, responseBody);
    }

    private static bool LooksLikeHtmlInsteadOfJson(string message, string? responseBody)
    {
        return message.Contains("invalid JSON", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("text/html", StringComparison.OrdinalIgnoreCase)
                || LooksLikeHtml(responseBody)
                || LooksLikeHtml(message));
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
