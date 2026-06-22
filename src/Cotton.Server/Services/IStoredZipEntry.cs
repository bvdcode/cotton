// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Describes an archive entry whose final byte length is known before the response starts.
    /// </summary>
    public interface IStoredZipEntry
    {
        /// <summary>
        /// Gets the ZIP entry path using forward slashes.
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Gets the uncompressed entry size in bytes.
        /// </summary>
        long SizeBytes { get; }

        /// <summary>
        /// Indicates whether the ZIP entry represents a directory marker.
        /// </summary>
        bool IsDirectory { get; }
    }
}
