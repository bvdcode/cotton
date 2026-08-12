// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Cotton.Server.Services
{
    public interface IStoredZipEntry
    {
        string Path { get; }

        long SizeBytes { get; }

        bool IsDirectory { get; }
    }
}
