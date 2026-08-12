// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Security;

namespace Cotton.Server.Services
{
    public class TempDirectoryProbe
    {
        private readonly Func<string> _getTempPath;

        public TempDirectoryProbe()
            : this(Path.GetTempPath)
        {
        }

        internal TempDirectoryProbe(Func<string> getTempPath)
        {
            _getTempPath = getTempPath;
        }

        public TempDirectoryProbeResult Probe()
        {
            string tempPath;
            try
            {
                tempPath = _getTempPath();
            }
            catch (Exception ex) when (IsProbeException(ex))
            {
                return new TempDirectoryProbeResult(string.Empty, false, ex.Message);
            }

            if (string.IsNullOrWhiteSpace(tempPath))
            {
                return new TempDirectoryProbeResult(string.Empty, false, "The OS temp path is empty.");
            }

            string? probeFilePath = null;
            try
            {
                Directory.CreateDirectory(tempPath);
                probeFilePath = Path.Combine(tempPath, $".cotton-write-test-{Guid.NewGuid():N}.tmp");
                File.WriteAllBytes(probeFilePath, [0]);
                File.Delete(probeFilePath);
                probeFilePath = null;
                return new TempDirectoryProbeResult(tempPath, true, null);
            }
            catch (Exception ex) when (IsProbeException(ex))
            {
                return new TempDirectoryProbeResult(tempPath, false, ex.Message);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(probeFilePath))
                {
                    TryDelete(probeFilePath);
                }
            }
        }

        private static bool IsProbeException(Exception exception)
        {
            return exception is IOException
                or UnauthorizedAccessException
                or SecurityException
                or ArgumentException
                or NotSupportedException;
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (IsProbeException(ex))
            {
            }
        }
    }
}
