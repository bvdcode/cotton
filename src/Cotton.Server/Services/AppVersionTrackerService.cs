// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Helpers;
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
        string? currentVersion = AppVersionHelpers.GetAppVersion();
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
        int numberCmp = CompareVersionNumbers(left.Numbers, right.Numbers);
        if (numberCmp != 0)
        {
            return numberCmp;
        }

        return ComparePrereleaseTags(left.Prerelease, right.Prerelease);
    }

    private static int CompareVersionNumbers(int[] left, int[] right)
    {
        int length = Math.Max(left.Length, right.Length);
        for (int i = 0; i < length; i++)
        {
            int l = i < left.Length ? left[i] : 0;
            int r = i < right.Length ? right[i] : 0;
            int cmp = l.CompareTo(r);
            if (cmp != 0)
            {
                return cmp;
            }
        }
        return 0;
    }

    private static int ComparePrereleaseTags(string[] left, string[] right)
    {
        bool leftHasPrerelease = left.Length > 0;
        bool rightHasPrerelease = right.Length > 0;

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

        int length = Math.Max(left.Length, right.Length);
        for (int i = 0; i < length; i++)
        {
            if (i >= left.Length)
            {
                return -1;
            }
            if (i >= right.Length)
            {
                return 1;
            }

            int cmp = ComparePrereleaseIdentifier(left[i], right[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    private static int ComparePrereleaseIdentifier(string left, string right)
    {
        bool leftIsNumber = int.TryParse(left, out int leftNumber);
        bool rightIsNumber = int.TryParse(right, out int rightNumber);

        if (leftIsNumber && rightIsNumber)
        {
            return leftNumber.CompareTo(rightNumber);
        }
        if (leftIsNumber)
        {
            return -1;
        }
        if (rightIsNumber)
        {
            return 1;
        }
        return string.CompareOrdinal(left, right);
    }

    private readonly record struct ParsedVersion(int[] Numbers, string[] Prerelease);
}
