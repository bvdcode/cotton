// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Sync.Desktop.Auth;

internal interface ISecretToolProcessRunner
{
    Task RunAsync(ProcessStartInfo startInfo, string? standardInput, CancellationToken cancellationToken);

    Task<string> ReadAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken);
}

internal sealed class SecretToolProcessRunner : ISecretToolProcessRunner
{
    public async Task RunAsync(
        ProcessStartInfo startInfo,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        await RunCoreAsync(startInfo, standardInput, captureOutput: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ReadAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        return await RunCoreAsync(startInfo, null, captureOutput: true, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> RunCoreAsync(
        ProcessStartInfo startInfo,
        string? standardInput,
        bool captureOutput,
        CancellationToken cancellationToken)
    {
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardOutput = captureOutput;
        startInfo.RedirectStandardInput = standardInput is not null;

        using Process process = Process.Start(startInfo)
            ?? throw new CryptographicException("Failed to start Secret Service helper.");
        try
        {
            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            Task<string>? outputTask = captureOutput
                ? process.StandardOutput.ReadToEndAsync(cancellationToken)
                : null;
            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            string output = outputTask is null ? string.Empty : await outputTask.ConfigureAwait(false);
            string error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new CryptographicException("Secret Service helper failed: " + error.Trim());
            }

            return output;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (IsProcessKillException(exception))
        {
            Trace.TraceWarning("Secret Service helper cancellation cleanup failed: {0}", exception);
        }
    }

    private static bool IsProcessKillException(Exception exception)
    {
        return exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException;
    }
}
