// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Benchmark.Infrastructure
{
    internal static class BenchmarkPathDefaults
    {
        private static readonly Lazy<string> Root = new(ResolveRoot);

        public static string BaselineDirectory => Path.Combine(Root.Value, "performance", "baselines");

        public static string ResultsDirectory => Path.Combine(Root.Value, "performance", "results");

        private static string ResolveRoot()
        {
            foreach (string startPath in GetStartPaths())
            {
                string? root = FindRoot(startPath);
                if (root is not null)
                {
                    return root;
                }
            }

            return Directory.GetCurrentDirectory();
        }

        private static IEnumerable<string> GetStartPaths()
        {
            yield return Directory.GetCurrentDirectory();
            yield return AppContext.BaseDirectory;
        }

        private static string? FindRoot(string startPath)
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "performance", "baselines"))
                    || File.Exists(Path.Combine(directory.FullName, "performance", "README.md")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
