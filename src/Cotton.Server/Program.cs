// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Startup;

namespace Cotton.Server
{
    public class Program
    {
        public static Task Main(string[] args) => ApplicationStartup.RunAsync(args);
    }
}
