// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Common;

internal static class TestDirectoryCleanup
{
    public static void DeleteDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        Directory.Delete(directoryPath, recursive: true);
    }
}
