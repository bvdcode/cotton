// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Cli
{

    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SyncCliCommandRunner
                .RunAsync(args, Console.Out, Console.Error)
                .ConfigureAwait(false);
        }
    }
}
