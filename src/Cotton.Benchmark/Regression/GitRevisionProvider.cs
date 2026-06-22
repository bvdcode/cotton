// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics;

namespace Cotton.Benchmark.Regression
{
    internal class GitRevisionProvider
    {
        public string GetCurrentRevision()
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                process.StartInfo.ArgumentList.Add("rev-parse");
                process.StartInfo.ArgumentList.Add("--short=12");
                process.StartInfo.ArgumentList.Add("HEAD");

                process.Start();
                if (!process.WaitForExit(5000))
                {
                    process.Kill(entireProcessTree: true);
                    return "unknown";
                }

                string output = process.StandardOutput.ReadToEnd().Trim();
                return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                    ? output
                    : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
