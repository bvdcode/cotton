// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local
{
    internal class LocalDirectoryScanFrame : IDisposable
    {
        private readonly EnumerationOptions _enumerationOptions;
        private readonly IEnumerator<string> _directoryEnumerator;
        private IEnumerator<string>? _fileEnumerator;
        private bool _filesDrained;

        public LocalDirectoryScanFrame(string directoryPath, EnumerationOptions enumerationOptions)
        {
            DirectoryPath = directoryPath;
            _enumerationOptions = enumerationOptions;
            _directoryEnumerator = Directory
                .EnumerateDirectories(directoryPath, "*", _enumerationOptions)
                .GetEnumerator();
        }

        public string DirectoryPath { get; }

        public bool TryReadNextDirectoryPath(out string? directoryPath)
        {
            if (_directoryEnumerator.MoveNext())
            {
                directoryPath = _directoryEnumerator.Current;
                return true;
            }

            directoryPath = null;
            return false;
        }

        public bool TryReadNextFilePath(out string? filePath)
        {
            if (_filesDrained)
            {
                filePath = null;
                return false;
            }

            _fileEnumerator ??= Directory
                .EnumerateFiles(DirectoryPath, "*", _enumerationOptions)
                .GetEnumerator();
            if (_fileEnumerator.MoveNext())
            {
                filePath = _fileEnumerator.Current;
                return true;
            }

            _filesDrained = true;
            filePath = null;
            return false;
        }

        public void Dispose()
        {
            _directoryEnumerator.Dispose();
            _fileEnumerator?.Dispose();
        }
    }
}
