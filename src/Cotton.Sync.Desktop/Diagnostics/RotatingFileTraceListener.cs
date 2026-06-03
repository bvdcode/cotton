// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Cotton.Sync.Desktop.Diagnostics;

internal sealed class RotatingFileTraceListener : TraceListener
{
    private const int DefaultRetainedFileCount = 3;

    private readonly object _gate = new();
    private readonly string _path;
    private readonly int _retainedFileCount;
    private readonly long _maxFileSizeBytes;

    public RotatingFileTraceListener(string path, long maxFileSizeBytes)
        : this(path, maxFileSizeBytes, DefaultRetainedFileCount)
    {
    }

    internal RotatingFileTraceListener(string path, long maxFileSizeBytes, int retainedFileCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (maxFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes), "Maximum log file size must be positive.");
        }

        if (retainedFileCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retainedFileCount), "Retained file count must not be negative.");
        }

        _path = path;
        _maxFileSizeBytes = maxFileSizeBytes;
        _retainedFileCount = retainedFileCount;
    }

    public string Path => _path;

    public override void Write(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        WriteCore(message);
    }

    public override void WriteLine(string? message)
    {
        string timestamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
        WriteCore(timestamp + " " + message + Environment.NewLine);
    }

    private void WriteCore(string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        lock (_gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path) ?? ".");
            RotateIfNeeded(bytes.LongLength);
            File.AppendAllText(_path, message, Encoding.UTF8);
        }
    }

    private void RotateIfNeeded(long incomingBytes)
    {
        if (!File.Exists(_path))
        {
            return;
        }

        long currentSize = new FileInfo(_path).Length;
        if (currentSize + incomingBytes <= _maxFileSizeBytes)
        {
            return;
        }

        if (_retainedFileCount == 0)
        {
            File.Delete(_path);
            return;
        }

        string oldestPath = GetRetainedPath(_retainedFileCount);
        if (File.Exists(oldestPath))
        {
            File.Delete(oldestPath);
        }

        for (int index = _retainedFileCount - 1; index >= 1; index--)
        {
            string source = GetRetainedPath(index);
            if (File.Exists(source))
            {
                File.Move(source, GetRetainedPath(index + 1));
            }
        }

        File.Move(_path, GetRetainedPath(1));
    }

    private string GetRetainedPath(int index)
    {
        return _path + "." + index.ToString(CultureInfo.InvariantCulture);
    }
}
