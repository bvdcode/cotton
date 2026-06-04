// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;

namespace Cotton.Sync.Cli;

internal static class SyncCliSoakCommandRunner
{
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        HttpClient? injectedHttpClient,
        CancellationToken cancellationToken)
    {
        SyncCliConnectionOptions? options = SyncCliOptionsReader.ReadConnectionOptions(args, error, "sync-soak");
        if (options is null)
        {
            return 2;
        }

        if (!SyncCliOptionsReader.TryReadOptionalPositiveInt(args, "--iterations", error, out int? iterations)
            || !SyncCliOptionsReader.TryReadOptionalPositiveInt(args, "--duration-seconds", error, out int? durationSeconds)
            || !SyncCliOptionsReader.TryReadOptionalPositiveInt(args, "--interval-seconds", error, out int? intervalSeconds))
        {
            return 2;
        }

        if (!iterations.HasValue && !durationSeconds.HasValue)
        {
            await error.WriteLineAsync("sync-soak requires --iterations or --duration-seconds.").ConfigureAwait(false);
            return 2;
        }

        string? probeFile = SyncCliOptionsReader.ReadOption(args, "--probe-file");
        string? normalizedProbeFile = null;
        if (!string.IsNullOrWhiteSpace(probeFile)
            && !SyncCliOptionsReader.TryNormalizeProbeFile(
                options.LocalRoot,
                probeFile,
                out normalizedProbeFile,
                out string probeError))
        {
            await error.WriteLineAsync(probeError).ConfigureAwait(false);
            return 2;
        }

        using HttpClient? ownedHttpClient = injectedHttpClient is null ? new HttpClient() : null;
        HttpClient httpClient = injectedHttpClient ?? ownedHttpClient!;
        SyncCliRuntime runtime = await SyncCliRuntimeFactory.CreateAsync(options, httpClient, cancellationToken)
            .ConfigureAwait(false);
        return await RunLoopAsync(
            options,
            runtime,
            output,
            iterations,
            durationSeconds,
            intervalSeconds ?? 30,
            normalizedProbeFile,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> RunLoopAsync(
        SyncCliConnectionOptions options,
        SyncCliRuntime runtime,
        TextWriter output,
        int? iterations,
        int? durationSeconds,
        int intervalSeconds,
        string? normalizedProbeFile,
        CancellationToken cancellationToken)
    {
        using Process process = Process.GetCurrentProcess();
        DateTime startedAtUtc = DateTime.UtcNow;
        TimeSpan startedCpu = process.TotalProcessorTime;
        DateTime? stopAtUtc = durationSeconds.HasValue
            ? startedAtUtc.AddSeconds(durationSeconds.Value)
            : null;
        int completedIterations = 0;
        int totalActivities = 0;
        long peakWorkingSetBytes = GetWorkingSetBytes(process);
        long peakManagedMemoryBytes = GC.GetTotalMemory(forceFullCollection: false);
        await output.WriteLineAsync("Cotton Sync soak run").ConfigureAwait(false);
        await output.WriteLineAsync("Sync pair: " + options.SyncPairId).ConfigureAwait(false);
        await output.WriteLineAsync("Started UTC: " + SyncCliFormat.FormatUtc(startedAtUtc)).ConfigureAwait(false);

        while (ShouldRunNextSoakIteration(completedIterations, iterations, stopAtUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            int iteration = completedIterations + 1;
            if (normalizedProbeFile is not null)
            {
                await WriteProbeFileAsync(options.LocalRoot, normalizedProbeFile, iteration, cancellationToken)
                    .ConfigureAwait(false);
            }

            SyncCliPassResult pass = await SyncCliRuntimeFactory
                .RunSinglePassAsync(runtime, cancellationToken)
                .ConfigureAwait(false);
            completedIterations++;
            totalActivities += pass.Result.Activities.Count;
            peakWorkingSetBytes = Math.Max(peakWorkingSetBytes, GetWorkingSetBytes(process));
            peakManagedMemoryBytes = Math.Max(peakManagedMemoryBytes, GC.GetTotalMemory(forceFullCollection: false));
            await output
                .WriteLineAsync(
                    "Iteration " + iteration.ToStringInvariant()
                    + ": activities=" + pass.Result.Activities.Count.ToStringInvariant()
                    + ", stateEntries=" + pass.StateEntries.Count.ToStringInvariant()
                    + ", workingSetBytes=" + GetWorkingSetBytes(process).ToStringInvariant()
                    + ", managedMemoryBytes=" + GC.GetTotalMemory(forceFullCollection: false).ToStringInvariant())
                .ConfigureAwait(false);

            if (!ShouldRunNextSoakIteration(completedIterations, iterations, stopAtUtc))
            {
                break;
            }

            await Task.Delay(GetNextSoakDelay(intervalSeconds, stopAtUtc), cancellationToken).ConfigureAwait(false);
        }

        SyncCliPassResult convergencePass = await SyncCliRuntimeFactory
            .RunSinglePassAsync(runtime, cancellationToken)
            .ConfigureAwait(false);
        peakWorkingSetBytes = Math.Max(peakWorkingSetBytes, GetWorkingSetBytes(process));
        peakManagedMemoryBytes = Math.Max(peakManagedMemoryBytes, GC.GetTotalMemory(forceFullCollection: false));
        int finalConvergenceActivities = convergencePass.Result.Activities.Count;
        await WriteSummaryAsync(
            output,
            startedAtUtc,
            startedCpu,
            process,
            peakWorkingSetBytes,
            peakManagedMemoryBytes,
            completedIterations,
            totalActivities,
            finalConvergenceActivities,
            convergencePass.StateEntries.Count).ConfigureAwait(false);
        return finalConvergenceActivities == 0 ? 0 : 1;
    }

    private static bool ShouldRunNextSoakIteration(
        int completedIterations,
        int? maxIterations,
        DateTime? stopAtUtc)
    {
        if (maxIterations.HasValue && completedIterations >= maxIterations.Value)
        {
            return false;
        }

        return !stopAtUtc.HasValue || DateTime.UtcNow < stopAtUtc.Value || completedIterations == 0;
    }

    private static TimeSpan GetNextSoakDelay(int intervalSeconds, DateTime? stopAtUtc)
    {
        TimeSpan interval = TimeSpan.FromSeconds(intervalSeconds);
        if (!stopAtUtc.HasValue)
        {
            return interval;
        }

        TimeSpan remaining = stopAtUtc.Value - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return remaining >= interval ? interval : remaining;
    }

    private static long GetWorkingSetBytes(Process process)
    {
        process.Refresh();
        return process.WorkingSet64;
    }

    private static TimeSpan GetTotalProcessorTime(Process process)
    {
        process.Refresh();
        return process.TotalProcessorTime;
    }

    private static async Task WriteProbeFileAsync(
        string localRoot,
        string relativePath,
        int iteration,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.Combine(localRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string content = "Cotton Sync soak probe" + Environment.NewLine
            + "Iteration: " + iteration.ToStringInvariant() + Environment.NewLine
            + "UTC: " + SyncCliFormat.FormatUtc(DateTime.UtcNow) + Environment.NewLine;
        await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteSummaryAsync(
        TextWriter output,
        DateTime startedAtUtc,
        TimeSpan startedCpu,
        Process process,
        long peakWorkingSetBytes,
        long peakManagedMemoryBytes,
        int completedIterations,
        int totalActivities,
        int finalConvergenceActivities,
        int finalStateEntries)
    {
        DateTime completedAtUtc = DateTime.UtcNow;
        TimeSpan elapsed = completedAtUtc - startedAtUtc;
        TimeSpan cpu = GetTotalProcessorTime(process) - startedCpu;
        await output.WriteLineAsync("Completed UTC: " + SyncCliFormat.FormatUtc(completedAtUtc)).ConfigureAwait(false);
        await output.WriteLineAsync("Elapsed seconds: " + elapsed.TotalSeconds.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("CPU seconds: " + cpu.TotalSeconds.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("Peak working set bytes: " + peakWorkingSetBytes.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("Peak managed memory bytes: " + peakManagedMemoryBytes.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("Iterations completed: " + completedIterations.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("Total activities: " + totalActivities.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("Final convergence activities: " + finalConvergenceActivities.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("Final state entries: " + finalStateEntries.ToStringInvariant()).ConfigureAwait(false);
        await output.WriteLineAsync("Converged: " + (finalConvergenceActivities == 0 ? "yes" : "no")).ConfigureAwait(false);
        await output.WriteLineAsync("Failures: " + (finalConvergenceActivities == 0 ? "0" : "1")).ConfigureAwait(false);
    }
}
