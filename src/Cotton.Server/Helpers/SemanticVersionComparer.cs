// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Helpers;

public static class SemanticVersionComparer
{
    public static bool IsDowngrade(string currentVersion, string latestVersion)
        => TryParse(currentVersion, out var current)
            && TryParse(latestVersion, out var latest)
            && Compare(latest, current) > 0;

    public static bool IsNewer(string candidateVersion, string currentVersion)
        => TryParse(candidateVersion, out var candidate)
            && TryParse(currentVersion, out var current)
            && Compare(candidate, current) > 0;

    private static bool TryParse(string value, out ParsedVersion parsed)
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

    private static int Compare(ParsedVersion left, ParsedVersion right)
    {
        int numberComparison = CompareNumbers(left.Numbers, right.Numbers);
        if (numberComparison != 0)
        {
            return numberComparison;
        }

        return ComparePrerelease(left.Prerelease, right.Prerelease);
    }

    private static int CompareNumbers(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        int numberLength = Math.Max(left.Count, right.Count);
        for (int i = 0; i < numberLength; i++)
        {
            int comparison = GetNumberPart(left, i).CompareTo(GetNumberPart(right, i));
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static int GetNumberPart(IReadOnlyList<int> numbers, int index)
    {
        return index < numbers.Count ? numbers[index] : 0;
    }

    private static int ComparePrerelease(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        int releaseComparison = CompareReleasePresence(left.Count > 0, right.Count > 0);
        if (releaseComparison != 0 || left.Count == 0)
        {
            return releaseComparison;
        }

        return ComparePrereleaseParts(left, right);
    }

    private static int CompareReleasePresence(bool leftHasPrerelease, bool rightHasPrerelease)
    {
        if (!leftHasPrerelease && !rightHasPrerelease)
        {
            return 0;
        }

        if (!leftHasPrerelease)
        {
            return 1;
        }

        return !rightHasPrerelease ? -1 : 0;
    }

    private static int ComparePrereleaseParts(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        int prereleaseLength = Math.Max(left.Count, right.Count);
        for (int i = 0; i < prereleaseLength; i++)
        {
            int comparison = ComparePrereleasePartAt(left, right, i);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static int ComparePrereleasePartAt(IReadOnlyList<string> left, IReadOnlyList<string> right, int index)
    {
        if (index >= left.Count)
        {
            return -1;
        }

        if (index >= right.Count)
        {
            return 1;
        }

        return ComparePrereleasePart(left[index], right[index]);
    }

    private static int ComparePrereleasePart(string left, string right)
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

        return rightIsNumber ? 1 : string.CompareOrdinal(left, right);
    }

    private readonly record struct ParsedVersion(int[] Numbers, string[] Prerelease);
}
