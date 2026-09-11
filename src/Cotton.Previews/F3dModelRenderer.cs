// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics;

namespace Cotton.Previews
{
    internal static class F3dModelRenderer
    {
        public static async Task<F3dRenderResult> RenderAsync(
            string modelFilePath,
            string outputPngPath,
            int size)
        {
            F3dRenderResult primaryResult = await RunAsync(
                modelFilePath,
                outputPngPath,
                size,
                includeMaxSizeArgument: true,
                includeNoBackgroundArgument: true,
                includeVerboseArgument: true).ConfigureAwait(false);

            if (primaryResult.Success)
            {
                return primaryResult;
            }

            F3dRenderResult fallbackResult = await RunAsync(
                modelFilePath,
                outputPngPath,
                size,
                includeMaxSizeArgument: false,
                includeNoBackgroundArgument: false,
                includeVerboseArgument: false).ConfigureAwait(false);

            if (fallbackResult.Success)
            {
                return new F3dRenderResult(
                    true,
                    $"f3d fallback rendering succeeded after primary failure. Primary diagnostics: {primaryResult.Diagnostics}");
            }

            return new F3dRenderResult(
                false,
                $"Primary f3d render failed. {primaryResult.Diagnostics} | " +
                $"Fallback f3d render failed. {fallbackResult.Diagnostics}");
        }

        private static async Task<F3dRenderResult> RunAsync(
            string modelFilePath,
            string outputPngPath,
            int size,
            bool includeMaxSizeArgument,
            bool includeNoBackgroundArgument,
            bool includeVerboseArgument)
        {
            const int renderTimeoutSeconds = 20;

            try
            {
                PreviewTemporaryFile.TryDelete(outputPngPath);

                bool useXvfb = ShouldUseXvfb();
                using Process process = new();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = useXvfb ? "xvfb-run" : "f3d",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                process.StartInfo.Environment["LIBGL_ALWAYS_SOFTWARE"] = "1";
                process.StartInfo.Environment["MESA_LOADER_DRIVER_OVERRIDE"] = "llvmpipe";
                process.StartInfo.Environment["GALLIUM_DRIVER"] = "llvmpipe";

                if (useXvfb)
                {
                    process.StartInfo.ArgumentList.Add("-a");
                    process.StartInfo.ArgumentList.Add("-s");
                    process.StartInfo.ArgumentList.Add($"-screen 0 {size}x{size}x24");
                    process.StartInfo.ArgumentList.Add("f3d");
                }

                process.StartInfo.ArgumentList.Add("--dry-run");
                if (includeVerboseArgument)
                {
                    process.StartInfo.ArgumentList.Add("--verbose");
                }

                process.StartInfo.ArgumentList.Add($"--input={modelFilePath}");
                process.StartInfo.ArgumentList.Add($"--output={outputPngPath}");
                process.StartInfo.ArgumentList.Add($"--resolution={size},{size}");
                process.StartInfo.ArgumentList.Add($"--color={PreviewColorPalette.AccentGreenF3dRgb}");
                if (includeMaxSizeArgument)
                {
                    process.StartInfo.ArgumentList.Add($"--max-size={PreviewGeneratorProvider.DefaultSmallPreviewSize}");
                }

                if (includeNoBackgroundArgument)
                {
                    process.StartInfo.ArgumentList.Add("--no-background");
                }

                process.Start();

                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();

                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(renderTimeoutSeconds));
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                string stdout = await stdoutTask.ConfigureAwait(false);
                string stderr = await stderrTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    return new F3dRenderResult(
                        false,
                        $"f3d exited with code {process.ExitCode} (xvfb={useXvfb}, " +
                        $"max-size={includeMaxSizeArgument}, no-background={includeNoBackgroundArgument}, " +
                        $"verbose={includeVerboseArgument}). stdout: {LimitDiagnostic(stdout)} " +
                        $"stderr: {LimitDiagnostic(stderr)}");
                }

                bool hasOutput = File.Exists(outputPngPath) && new FileInfo(outputPngPath).Length > 0;
                return hasOutput
                    ? new F3dRenderResult(true, null)
                    : new F3dRenderResult(
                        false,
                        $"f3d finished successfully but did not produce output file (xvfb={useXvfb}, " +
                        $"max-size={includeMaxSizeArgument}, no-background={includeNoBackgroundArgument}, " +
                        $"verbose={includeVerboseArgument}). stdout: {LimitDiagnostic(stdout)} " +
                        $"stderr: {LimitDiagnostic(stderr)}");
            }
            catch (OperationCanceledException)
            {
                return new F3dRenderResult(
                    false,
                    $"f3d render timed out after {renderTimeoutSeconds} seconds " +
                    $"(max-size={includeMaxSizeArgument}, no-background={includeNoBackgroundArgument}, " +
                    $"verbose={includeVerboseArgument}).");
            }
            catch (Exception exception)
            {
                return new F3dRenderResult(
                    false,
                    $"f3d render failed (max-size={includeMaxSizeArgument}, " +
                    $"no-background={includeNoBackgroundArgument}, verbose={includeVerboseArgument}): " +
                    exception.Message);
            }
        }

        private static bool ShouldUseXvfb()
        {
            return OperatingSystem.IsLinux()
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
                && IsExecutableOnPath("xvfb-run");
        }

        private static bool IsExecutableOnPath(string fileName)
        {
            string? path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (File.Exists(Path.Combine(directory, fileName)))
                {
                    return true;
                }
            }

            return false;
        }

        private static string LimitDiagnostic(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "<empty>";
            }

            const int maxLength = 1000;
            string normalized = text.Trim();
            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength] + "...";
        }
    }
}
