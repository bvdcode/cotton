// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;

namespace Cotton.Sync.Desktop.Platform
{
    internal class DesktopSingleInstanceGuard : IDisposable
    {
        private readonly FileStream _lockStream;
        private bool _disposed;

        private DesktopSingleInstanceGuard(FileStream lockStream)
        {
            _lockStream = lockStream;
        }

        public static DesktopSingleInstanceGuard? TryAcquire(string lockFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
            string? directory = Path.GetDirectoryName(lockFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                FileStream stream = File.Open(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                WriteCurrentProcessId(stream);
                return new DesktopSingleInstanceGuard(stream);
            }
            catch (IOException exception)
            {
                Trace.TraceWarning("Cotton Sync single-instance lock is already held: {0}", exception.Message);
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _lockStream.Dispose();
            _disposed = true;
        }

        private static void WriteCurrentProcessId(FileStream stream)
        {
            stream.SetLength(0);
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.Flush();
            stream.Flush();
            stream.Position = 0;
        }
    }
}
