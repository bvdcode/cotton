// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using System.Diagnostics;

namespace Cotton.Sync.Cli
{
    internal static class SyncCliSoakCommandRunner
    {
        public static async Task<int> RunAsync(
            IReadOnlyList<string> args,
            TextWriter output,
            TextWriter error,
            HttpClient? injectedHttpClient,
            CancellationToken cancellationToken)
        {
            SyncCliConnectionOptions? options = SyncCliOptionsReader.ReadConnectionOptions(
                args,
                error,
                "sync-soak",
                allowBrowserLogin: true);
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

            SyncCliConnectionOptions? secondClientOptions = ReadSecondClientOptions(args, options, error);
            if (HasSecondClientOption(args) && secondClientOptions is null)
            {
                return 2;
            }

            using HttpClient? ownedHttpClient = injectedHttpClient is null ? new HttpClient() : null;
            HttpClient httpClient = injectedHttpClient ?? ownedHttpClient!;
            try
            {
                await using SyncCliRuntime runtime = await CreateRuntimeAsync(options, httpClient, output, cancellationToken)
                    .ConfigureAwait(false);
                if (secondClientOptions is null)
                {
                    return await RunLoopAsync(
                        options,
                        runtime,
                        secondClientOptions,
                        null,
                        output,
                        iterations,
                        durationSeconds,
                        intervalSeconds ?? 30,
                        normalizedProbeFile,
                        cancellationToken).ConfigureAwait(false);
                }

                await using SyncCliRuntime secondRuntime = await CreateRuntimeAsync(
                    secondClientOptions,
                    httpClient,
                    output,
                    cancellationToken).ConfigureAwait(false);
                return await RunLoopAsync(
                    options,
                    runtime,
                    secondClientOptions,
                    secondRuntime,
                    output,
                    iterations,
                    durationSeconds,
                    intervalSeconds ?? 30,
                    normalizedProbeFile,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (AppCodeBrowserSignInException exception)
            {
                await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(exception.Error))
                {
                    await error.WriteLineAsync("Error: " + exception.Error).ConfigureAwait(false);
                }

                return 1;
            }
        }

        private static Task<SyncCliRuntime> CreateRuntimeAsync(
            SyncCliConnectionOptions options,
            HttpClient httpClient,
            TextWriter output,
            CancellationToken cancellationToken)
        {
            return options.UseBrowserLogin
                ? SyncCliRuntimeFactory.CreateWithBrowserAuthAsync(
                    options,
                    httpClient,
                    new SyncCliApprovalUrlWriter(output),
                    cancellationToken)
                : SyncCliRuntimeFactory.CreateAsync(options, httpClient, cancellationToken);
        }

        private static async Task<int> RunLoopAsync(
            SyncCliConnectionOptions options,
            SyncCliRuntime runtime,
            SyncCliConnectionOptions? secondClientOptions,
            SyncCliRuntime? secondRuntime,
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
            long startedWorkingSetBytes = GetWorkingSetBytes(process);
            long startedManagedMemoryBytes = GC.GetTotalMemory(forceFullCollection: false);
            DateTime? stopAtUtc = durationSeconds.HasValue
                ? startedAtUtc.AddSeconds(durationSeconds.Value)
                : null;
            int completedIterations = 0;
            int totalActivities = 0;
            int syncErrors = 0;
            int? finalConvergenceActivities = null;
            int? finalStateEntries = null;
            TimeSpan totalIterationElapsed = TimeSpan.Zero;
            TimeSpan longestIterationElapsed = TimeSpan.Zero;
            long peakWorkingSetBytes = startedWorkingSetBytes;
            long peakManagedMemoryBytes = startedManagedMemoryBytes;
            await output.WriteLineAsync("Cotton Sync soak run").ConfigureAwait(false);
            await output.WriteLineAsync("Sync pair: " + options.SyncPairId).ConfigureAwait(false);
            if (secondClientOptions is not null)
            {
                await output.WriteLineAsync("Second sync pair: " + secondClientOptions.SyncPairId).ConfigureAwait(false);
            }

            await output.WriteLineAsync("Started UTC: " + SyncCliFormat.FormatUtc(startedAtUtc)).ConfigureAwait(false);

            try
            {
                while (ShouldRunNextSoakIteration(completedIterations, iterations, stopAtUtc))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int iteration = completedIterations + 1;
                    long iterationStartedTimestamp = Stopwatch.GetTimestamp();
                    if (normalizedProbeFile is not null)
                    {
                        await WriteProbeFileAsync(options.LocalRoot, normalizedProbeFile, iteration, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    SyncCliPassResult pass = await SyncCliRuntimeFactory
                        .RunSinglePassAsync(runtime, cancellationToken)
                        .ConfigureAwait(false);
                    SyncCliPassResult? secondPass = secondRuntime is null
                        ? null
                        : await SyncCliRuntimeFactory
                            .RunSinglePassAsync(secondRuntime, cancellationToken)
                            .ConfigureAwait(false);
                    TimeSpan iterationElapsed = Stopwatch.GetElapsedTime(iterationStartedTimestamp);
                    totalIterationElapsed += iterationElapsed;
                    if (iterationElapsed > longestIterationElapsed)
                    {
                        longestIterationElapsed = iterationElapsed;
                    }

                    completedIterations++;
                    totalActivities += pass.Result.Activities.Count + (secondPass?.Result.Activities.Count ?? 0);
                    peakWorkingSetBytes = Math.Max(peakWorkingSetBytes, GetWorkingSetBytes(process));
                    peakManagedMemoryBytes = Math.Max(peakManagedMemoryBytes, GC.GetTotalMemory(forceFullCollection: false));
                    await WriteIterationAsync(output, iteration, pass, secondPass, process, iterationElapsed).ConfigureAwait(false);

                    if (!ShouldRunNextSoakIteration(completedIterations, iterations, stopAtUtc))
                    {
                        break;
                    }

                    await Task.Delay(GetNextSoakDelay(intervalSeconds, stopAtUtc), cancellationToken).ConfigureAwait(false);
                }

                SyncCliPassResult convergencePass = await SyncCliRuntimeFactory
                    .RunSinglePassAsync(runtime, cancellationToken)
                    .ConfigureAwait(false);
                SyncCliPassResult? secondConvergencePass = secondRuntime is null
                    ? null
                    : await SyncCliRuntimeFactory
                        .RunSinglePassAsync(secondRuntime, cancellationToken)
                        .ConfigureAwait(false);
                peakWorkingSetBytes = Math.Max(peakWorkingSetBytes, GetWorkingSetBytes(process));
                peakManagedMemoryBytes = Math.Max(peakManagedMemoryBytes, GC.GetTotalMemory(forceFullCollection: false));
                finalConvergenceActivities = convergencePass.Result.Activities.Count
                    + (secondConvergencePass?.Result.Activities.Count ?? 0);
                finalStateEntries = convergencePass.StateEntries.Count + (secondConvergencePass?.StateEntries.Count ?? 0);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                syncErrors++;
                peakWorkingSetBytes = Math.Max(peakWorkingSetBytes, GetWorkingSetBytes(process));
                peakManagedMemoryBytes = Math.Max(peakManagedMemoryBytes, GC.GetTotalMemory(forceFullCollection: false));
                await output.WriteLineAsync("Sync error: " + FormatException(exception)).ConfigureAwait(false);
            }

            await WriteSummaryAsync(
                output,
                startedAtUtc,
                startedCpu,
                process,
                startedWorkingSetBytes,
                startedManagedMemoryBytes,
                peakWorkingSetBytes,
                peakManagedMemoryBytes,
                completedIterations,
                totalIterationElapsed,
                longestIterationElapsed,
                totalActivities,
                syncErrors,
                finalConvergenceActivities,
                finalStateEntries).ConfigureAwait(false);
            return syncErrors == 0 && finalConvergenceActivities == 0 ? 0 : 1;
        }

        private static SyncCliConnectionOptions? ReadSecondClientOptions(
            IReadOnlyList<string> args,
            SyncCliConnectionOptions firstClientOptions,
            TextWriter error)
        {
            string? localRoot = SyncCliOptionsReader.ReadOption(args, "--second-local-root");
            string? syncPairId = SyncCliOptionsReader.ReadOption(args, "--second-sync-pair");
            string? databasePath = SyncCliOptionsReader.ReadOption(args, "--second-database");
            if (string.IsNullOrWhiteSpace(localRoot)
                && string.IsNullOrWhiteSpace(syncPairId)
                && string.IsNullOrWhiteSpace(databasePath))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(localRoot)
                || string.IsNullOrWhiteSpace(syncPairId)
                || string.IsNullOrWhiteSpace(databasePath))
            {
                error.WriteLine(
                    "Two-client sync-soak requires --second-local-root, --second-sync-pair, and --second-database together.");
                return null;
            }

            if (IsSameOrNestedPath(firstClientOptions.LocalRoot, localRoot))
            {
                error.WriteLine("Two-client sync-soak local roots must be different and non-nested.");
                return null;
            }

            if (string.Equals(firstClientOptions.SyncPairId, syncPairId.Trim(), StringComparison.Ordinal))
            {
                error.WriteLine("Two-client sync-soak sync pair ids must be different.");
                return null;
            }

            if (IsSamePath(firstClientOptions.DatabasePath, databasePath))
            {
                error.WriteLine("Two-client sync-soak databases must be different.");
                return null;
            }

            return firstClientOptions with
            {
                LocalRoot = localRoot,
                SyncPairId = syncPairId.Trim(),
                DatabasePath = databasePath,
            };
        }

        private static bool HasSecondClientOption(IReadOnlyList<string> args)
        {
            return SyncCliOptionsReader.ReadOption(args, "--second-local-root") is not null
                || SyncCliOptionsReader.ReadOption(args, "--second-sync-pair") is not null
                || SyncCliOptionsReader.ReadOption(args, "--second-database") is not null;
        }

        private static bool IsSameOrNestedPath(string firstPath, string secondPath)
        {
            string first = NormalizeFullPath(firstPath);
            string second = NormalizeFullPath(secondPath);
            StringComparison comparison = GetPathComparison();
            return string.Equals(first, second, comparison)
                || second.StartsWith(EnsureTrailingSeparator(first), comparison)
                || first.StartsWith(EnsureTrailingSeparator(second), comparison);
        }

        private static bool IsSamePath(string firstPath, string secondPath)
        {
            return string.Equals(NormalizeFullPath(firstPath), NormalizeFullPath(secondPath), GetPathComparison());
        }

        private static string NormalizeFullPath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string? root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root) && string.Equals(fullPath, root, GetPathComparison()))
            {
                return root;
            }

            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static StringComparison GetPathComparison()
        {
            return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
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

        private static async Task WriteIterationAsync(
            TextWriter output,
            int iteration,
            SyncCliPassResult pass,
            SyncCliPassResult? secondPass,
            Process process,
            TimeSpan iterationElapsed)
        {
            string metrics = ", workingSetBytes=" + GetWorkingSetBytes(process).ToStringInvariant()
                + ", managedMemoryBytes=" + GC.GetTotalMemory(forceFullCollection: false).ToStringInvariant()
                + ", elapsedSeconds=" + iterationElapsed.TotalSeconds.ToStringInvariant();
            if (secondPass is null)
            {
                await output
                    .WriteLineAsync(
                        "Iteration " + iteration.ToStringInvariant()
                        + ": activities=" + pass.Result.Activities.Count.ToStringInvariant()
                        + ", stateEntries=" + pass.StateEntries.Count.ToStringInvariant()
                        + metrics)
                    .ConfigureAwait(false);
                return;
            }

            await output
                .WriteLineAsync(
                    "Iteration " + iteration.ToStringInvariant()
                    + ": clientAActivities=" + pass.Result.Activities.Count.ToStringInvariant()
                    + ", clientBActivities=" + secondPass.Result.Activities.Count.ToStringInvariant()
                    + ", clientAStateEntries=" + pass.StateEntries.Count.ToStringInvariant()
                    + ", clientBStateEntries=" + secondPass.StateEntries.Count.ToStringInvariant()
                    + metrics)
                .ConfigureAwait(false);
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
            long startedWorkingSetBytes,
            long startedManagedMemoryBytes,
            long peakWorkingSetBytes,
            long peakManagedMemoryBytes,
            int completedIterations,
            TimeSpan totalIterationElapsed,
            TimeSpan longestIterationElapsed,
            int totalActivities,
            int syncErrors,
            int? finalConvergenceActivities,
            int? finalStateEntries)
        {
            DateTime completedAtUtc = DateTime.UtcNow;
            TimeSpan elapsed = completedAtUtc - startedAtUtc;
            TimeSpan cpu = GetTotalProcessorTime(process) - startedCpu;
            double cpuUtilizationPercent = CalculateCpuUtilizationPercent(cpu, elapsed);
            long completedWorkingSetBytes = GetWorkingSetBytes(process);
            long completedManagedMemoryBytes = GC.GetTotalMemory(forceFullCollection: false);
            peakWorkingSetBytes = Math.Max(peakWorkingSetBytes, completedWorkingSetBytes);
            peakManagedMemoryBytes = Math.Max(peakManagedMemoryBytes, completedManagedMemoryBytes);
            bool converged = syncErrors == 0 && finalConvergenceActivities == 0;
            int failures = syncErrors;
            if (syncErrors == 0 && finalConvergenceActivities.GetValueOrDefault() > 0)
            {
                failures++;
            }

            await output.WriteLineAsync("Completed UTC: " + SyncCliFormat.FormatUtc(completedAtUtc)).ConfigureAwait(false);
            await output.WriteLineAsync("Elapsed seconds: " + elapsed.TotalSeconds.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("CPU seconds: " + cpu.TotalSeconds.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("CPU utilization percent: " + cpuUtilizationPercent.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Start working set bytes: " + startedWorkingSetBytes.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("End working set bytes: " + completedWorkingSetBytes.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Working set growth bytes: " + (completedWorkingSetBytes - startedWorkingSetBytes).ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Peak working set bytes: " + peakWorkingSetBytes.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Peak working set growth bytes: " + (peakWorkingSetBytes - startedWorkingSetBytes).ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Start managed memory bytes: " + startedManagedMemoryBytes.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("End managed memory bytes: " + completedManagedMemoryBytes.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Managed memory growth bytes: " + (completedManagedMemoryBytes - startedManagedMemoryBytes).ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Peak managed memory bytes: " + peakManagedMemoryBytes.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Peak managed memory growth bytes: " + (peakManagedMemoryBytes - startedManagedMemoryBytes).ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Iterations completed: " + completedIterations.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Iteration seconds total: " + totalIterationElapsed.TotalSeconds.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Iteration seconds average: " + CalculateAverageIterationSeconds(totalIterationElapsed, completedIterations).ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Iteration seconds max: " + longestIterationElapsed.TotalSeconds.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Total activities: " + totalActivities.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Sync errors: " + syncErrors.ToStringInvariant()).ConfigureAwait(false);
            await output.WriteLineAsync("Final convergence activities: " + FormatOptionalInt(finalConvergenceActivities)).ConfigureAwait(false);
            await output.WriteLineAsync("Final state entries: " + FormatOptionalInt(finalStateEntries)).ConfigureAwait(false);
            await output.WriteLineAsync("Converged: " + (converged ? "yes" : "no")).ConfigureAwait(false);
            await output.WriteLineAsync("Failures: " + failures.ToStringInvariant()).ConfigureAwait(false);
        }

        private static string FormatOptionalInt(int? value)
        {
            return value.HasValue ? value.Value.ToStringInvariant() : "not run";
        }

        private static double CalculateAverageIterationSeconds(TimeSpan totalIterationElapsed, int completedIterations)
        {
            return completedIterations > 0
                ? totalIterationElapsed.TotalSeconds / completedIterations
                : 0;
        }

        private static double CalculateCpuUtilizationPercent(TimeSpan cpu, TimeSpan elapsed)
        {
            return elapsed.TotalSeconds > 0
                ? cpu.TotalSeconds / elapsed.TotalSeconds * 100
                : 0;
        }

        private static string FormatException(Exception exception)
        {
            string message = string.IsNullOrWhiteSpace(exception.Message)
                ? "No details."
                : exception.Message.ReplaceLineEndings(" ");
            return exception.GetType().Name + ": " + message;
        }
    }
}
