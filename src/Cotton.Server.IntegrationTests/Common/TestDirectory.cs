// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Common
{
    internal static class TestDirectory
    {
        public static void Delete(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
    }
}
