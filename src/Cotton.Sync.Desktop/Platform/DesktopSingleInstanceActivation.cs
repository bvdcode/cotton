// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Sync.Desktop.Platform
{
    internal static class DesktopSingleInstanceActivation
    {
        private const string PipePrefix = "cotton-sync-";
        private const string ShowCommand = "show";
        private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(2);

        public static DesktopSingleInstanceActivationServer StartServer(string lockFilePath, Action showWindow)
        {
            return DesktopSingleInstanceActivationServer.Start(CreatePipeName(lockFilePath), showWindow);
        }

        public static async Task<bool> TryRequestShowAsync(
            string lockFilePath,
            CancellationToken cancellationToken = default)
        {
            return await TryRequestShowAsync(lockFilePath, DefaultRequestTimeout, cancellationToken)
                .ConfigureAwait(false);
        }

        internal static async Task<bool> TryRequestShowAsync(
            string lockFilePath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

            using CancellationTokenSource timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(timeout);
            string pipeName = CreatePipeName(lockFilePath);
            try
            {
                await using var pipe = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);
                await pipe.ConnectAsync(timeoutCancellation.Token).ConfigureAwait(false);
                await using var writer = new StreamWriter(pipe, Encoding.UTF8)
                {
                    AutoFlush = true,
                };
                await writer.WriteLineAsync(ShowCommand.AsMemory(), timeoutCancellation.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceWarning("Cotton Sync single-instance activation timed out: {0}", exception.Message);
                return false;
            }
            catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Cotton Sync single-instance activation request failed: {0}", exception.Message);
                return false;
            }
        }

        internal static string CreatePipeName(string lockFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
            string fullPath = Path.GetFullPath(lockFilePath);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(fullPath));
            return PipePrefix + Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
        }

        internal static bool IsShowCommand(string? command)
        {
            return string.Equals(command, ShowCommand, StringComparison.OrdinalIgnoreCase);
        }
    }
}
