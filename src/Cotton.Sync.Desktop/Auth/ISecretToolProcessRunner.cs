// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;

namespace Cotton.Sync.Desktop.Auth
{
    internal interface ISecretToolProcessRunner
    {
        Task RunAsync(ProcessStartInfo startInfo, string? standardInput, CancellationToken cancellationToken);

        Task<string> ReadAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken);
    }
}
