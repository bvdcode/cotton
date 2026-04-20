// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

public class AppVersionTrackerService(
    IServiceProvider _serviceProvider,
    ILogger<AppVersionTrackerService> _logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            await TrackVersionAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track app version.");
        }
    }

    private async Task TrackVersionAsync(CancellationToken cancellationToken)
    {
        string? currentVersion = Environment.GetEnvironmentVariable("APP_VERSION");
        if (string.IsNullOrEmpty(currentVersion))
        {
            return;
        }

        using IServiceScope scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

        AppVersion? latestVersion = await dbContext.AppVersions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestVersion?.Version == currentVersion)
        {
            return;
        }

        if (latestVersion is not null && IsDowngrade(currentVersion, latestVersion.Version))
        {
            _logger.LogWarning(
                "Detected app downgrade: current version {CurrentVersion} is lower than latest recorded {LatestVersion}. This scenario is not supported and may be unstable.",
                currentVersion,
                latestVersion.Version);
        }

        dbContext.AppVersions.Add(new AppVersion
        {
            Version = currentVersion,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsDowngrade(string currentVersion, string latestVersion)
    {
        if (!TryParseSemanticVersion(currentVersion, out var current) ||
            !TryParseSemanticVersion(latestVersion, out var latest))
        {
            return false;
        }

        return CompareSemanticVersions(latest, current) > 0;
    }

    private static bool TryParseSemanticVersion(string value, out ParsedVersion parsed)
    {
        parsed = default;
        string normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        int buildMetaIndex = normalized.IndexOf('+');
        if (buildMetaIndex >= 0)
        {
            normalized = normalized[..buildMetaIndex];
        }

        string corePart = normalized;
        string[] prereleaseParts = [];
        int prereleaseIndex = normalized.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            corePart = normalized[..prereleaseIndex];
            string prerelease = normalized[(prereleaseIndex + 1)..];
            prereleaseParts = string.IsNullOrWhiteSpace(prerelease)
                ? []
                : prerelease.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        string[] numberParts = corePart.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (numberParts.Length == 0)
        {
            return false;
        }

        var numbers = new int[numberParts.Length];
        for (int i = 0; i < numberParts.Length; i++)
        {
            if (!int.TryParse(numberParts[i], out numbers[i]))
            {
                return false;
            }
        }

        parsed = new ParsedVersion(numbers, prereleaseParts);
        return true;
    }

    private static int CompareSemanticVersions(ParsedVersion left, ParsedVersion right)
    {
        int numberLength = Math.Max(left.Numbers.Length, right.Numbers.Length);
        for (int i = 0; i < numberLength; i++)
        {
            int l = i < left.Numbers.Length ? left.Numbers[i] : 0;
            int r = i < right.Numbers.Length ? right.Numbers[i] : 0;
            int cmp = l.CompareTo(r);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        bool leftHasPrerelease = left.Prerelease.Length > 0;
        bool rightHasPrerelease = right.Prerelease.Length > 0;

        if (!leftHasPrerelease && !rightHasPrerelease)
        {
            return 0;
        }

        if (!leftHasPrerelease)
        {
            return 1;
        }

        if (!rightHasPrerelease)
        {
            return -1;
        }

        int prereleaseLength = Math.Max(left.Prerelease.Length, right.Prerelease.Length);
        for (int i = 0; i < prereleaseLength; i++)
        {
            if (i >= left.Prerelease.Length)
            {
                return -1;
            }

            if (i >= right.Prerelease.Length)
            {
                return 1;
            }

            string l = left.Prerelease[i];
            string r = right.Prerelease[i];

            bool lIsNumber = int.TryParse(l, out int lNumber);
            bool rIsNumber = int.TryParse(r, out int rNumber);

            int cmp;
            if (lIsNumber && rIsNumber)
            {
                cmp = lNumber.CompareTo(rNumber);
            }
            else if (lIsNumber)
            {
                cmp = -1;
            }
            else if (rIsNumber)
            {
                cmp = 1;
            }
            else
            {
                cmp = string.CompareOrdinal(l, r);
            }

            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    private readonly record struct ParsedVersion(int[] Numbers, string[] Prerelease);
}
