// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics;

namespace Cotton.Previews
{
    internal static class PreviewProcess
    {
        public static async Task<bool> WaitForExitAsync(
            Process process,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            TimeSpan effectiveTimeout = timeout ?? TimeSpan.FromSeconds(60);
            Task exitTask = process.WaitForExitAsync(CancellationToken.None);
            Task timeoutTask = Task.Delay(effectiveTimeout, cancellationToken);
            Task completedTask = await Task.WhenAny(exitTask, timeoutTask).ConfigureAwait(false);
            if (completedTask == exitTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await exitTask.ConfigureAwait(false);
                return true;
            }

            await TerminateAsync(process).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }

        public static async Task TerminateAsync(Process process)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
