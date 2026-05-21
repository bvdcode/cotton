// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Helpers;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cotton.Server.Services;

public class AppVersionTrackerService(
    IServiceProvider _serviceProvider,
    IHttpClientFactory _httpClientFactory,
    IConfiguration _configuration,
    IHostEnvironment _environment,
    ILogger<AppVersionTrackerService> _logger) : BackgroundService
{
    public const string GitHubHttpClientName = "Cotton.GitHub";

    private const string LatestReleasePath = "repos/bvdcode/cotton/releases/latest";
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            await TrackVersionAndCheckLatestReleaseAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track app version.");
        }
    }

    private async Task TrackVersionAndCheckLatestReleaseAsync(CancellationToken cancellationToken)
    {
        string? currentVersion = AppVersionHelpers.GetAppVersion();
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return;
        }

        using IServiceScope scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

        AppVersion currentVersionRecord = await TrackVersionAsync(
            dbContext,
            currentVersion,
            cancellationToken);

        if (!IsReleaseCheckEnabled())
        {
            return;
        }

        await CheckLatestReleaseAsync(
            scope.ServiceProvider,
            dbContext,
            currentVersionRecord,
            currentVersion,
            cancellationToken);
    }

    private bool IsReleaseCheckEnabled()
    {
        if (_environment.IsEnvironment("Testing") || _environment.IsEnvironment("IntegrationTests"))
        {
            return false;
        }

        return _configuration.GetValue("AppVersionTracker:ReleaseCheckEnabled", true);
    }

    private async Task<AppVersion> TrackVersionAsync(
        CottonDbContext dbContext,
        string currentVersion,
        CancellationToken cancellationToken)
    {
        AppVersion? latestVersion = await dbContext.AppVersions
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestVersion?.Version == currentVersion)
        {
            return latestVersion;
        }

        if (latestVersion is not null && SemanticVersionComparer.IsDowngrade(currentVersion, latestVersion.Version))
        {
            _logger.LogWarning(
                "Detected app downgrade: current version {CurrentVersion} is lower than latest recorded {LatestVersion}. This scenario is not supported and may be unstable.",
                currentVersion,
                latestVersion.Version);
        }

        var currentVersionRecord = new AppVersion
        {
            Version = currentVersion,
        };

        dbContext.AppVersions.Add(currentVersionRecord);
        await dbContext.SaveChangesAsync(cancellationToken);
        return currentVersionRecord;
    }

    private async Task CheckLatestReleaseAsync(
        IServiceProvider serviceProvider,
        CottonDbContext dbContext,
        AppVersion currentVersionRecord,
        string currentVersion,
        CancellationToken cancellationToken)
    {
        LatestRelease? latestRelease = await FetchLatestReleaseAsync(cancellationToken);
        if (latestRelease is null)
        {
            return;
        }

        bool alreadyNotified = currentVersionRecord.LatestReleaseNotifiedAt.HasValue
            && string.Equals(
                currentVersionRecord.LatestReleaseVersion,
                latestRelease.Version,
                StringComparison.OrdinalIgnoreCase);

        DateTime checkedAt = DateTime.UtcNow;
        currentVersionRecord.LatestReleaseCheckedAt = checkedAt;
        currentVersionRecord.LatestReleaseVersion = latestRelease.Version;
        currentVersionRecord.LatestReleaseUrl = latestRelease.Url;

        if (!SemanticVersionComparer.IsNewer(latestRelease.Version, currentVersion))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (alreadyNotified)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var adminIds = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Role == UserRole.Admin)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (adminIds.Count == 0)
        {
            _logger.LogInformation(
                "Cotton release {LatestReleaseVersion} is newer than current {CurrentVersion}, but no admin users exist for notification.",
                latestRelease.Version,
                currentVersion);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var notifications = serviceProvider.GetRequiredService<INotificationsProvider>();
        string releaseNotes = NotificationTemplates.FormatReleaseNotes(latestRelease.Notes);
        var metadata = new Dictionary<string, string>
        {
            ["kind"] = "app-update-available",
            ["currentVersion"] = currentVersion,
            ["latestVersion"] = latestRelease.Version,
            ["releaseUrl"] = latestRelease.Url,
            ["releaseNotes"] = releaseNotes,
        };
        Dictionary<string, string> templateMetadata = NotificationTemplateMetadata.Create(
            NotificationTemplateKeys.AppUpdateAvailableTitle,
            NotificationTemplateKeys.AppUpdateAvailableContent,
            metadata);

        foreach (Guid adminId in adminIds)
        {
            await notifications.SendNotificationAsync(
                adminId,
                NotificationTemplates.AppUpdateAvailableTitle,
                NotificationTemplates.AppUpdateAvailableContent(
                    currentVersion,
                    latestRelease.Version,
                    latestRelease.Url,
                    latestRelease.Notes),
                NotificationPriority.Medium,
                templateMetadata);
        }

        currentVersionRecord.LatestReleaseNotifiedAt = checkedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<LatestRelease?> FetchLatestReleaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(GitHubHttpClientName);
            using var response = await client.GetAsync(LatestReleasePath, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to check latest Cotton release. GitHub returned {StatusCode}.",
                    response.StatusCode);
                return null;
            }

            var release = await response.Content.ReadFromJsonAsync<GitHubReleaseResponse>(cancellationToken);
            if (release is null || release.Draft || release.Prerelease || string.IsNullOrWhiteSpace(release.TagName))
            {
                return null;
            }

            string version = NormalizeVersionTag(release.TagName);
            string url = string.IsNullOrWhiteSpace(release.HtmlUrl)
                ? $"https://github.com/bvdcode/cotton/releases/tag/{Uri.EscapeDataString(release.TagName)}"
                : release.HtmlUrl;

            return new LatestRelease(version, url, release.Body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
        {
            _logger.LogWarning(ex, "Failed to check latest Cotton release.");
            return null;
        }
    }

    private static string NormalizeVersionTag(string tagName)
    {
        string normalized = tagName.Trim();
        return normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? normalized[1..]
            : normalized;
    }

    private sealed record LatestRelease(string Version, string Url, string? Notes);

    private sealed record GitHubReleaseResponse(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease);
}
