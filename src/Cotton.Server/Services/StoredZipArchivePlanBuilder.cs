// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;

namespace Cotton.Server.Services
{
    internal static class StoredZipArchivePlanBuilder
    {
        public static StoredZipArchiveWriter.ZipPlan Build<TEntry>(IReadOnlyList<TEntry> entries)
            where TEntry : IStoredZipEntry
        {
            long offset = 0;
            StoredZipArchiveWriter.ZipEntryPlan[] plans = new StoredZipArchiveWriter.ZipEntryPlan[entries.Count];

            for (int i = 0; i < entries.Count; i++)
            {
                TEntry entry = entries[i];
                if (entry.SizeBytes < 0)
                {
                    throw new InvalidOperationException($"Archive entry '{entry.Path}' has a negative size.");
                }

                byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
                if (pathBytes.Length == 0 || pathBytes.Length > ushort.MaxValue)
                {
                    throw new InvalidOperationException($"Archive entry path has invalid UTF-8 length: '{entry.Path}'.");
                }

                bool usesZip64DataDescriptor = !entry.IsDirectory
                    && StoredZipArchiveWriter.RequiresZip64UInt32(entry.SizeBytes);
                long localHeaderLength = 30 + pathBytes.Length;
                long dataDescriptorLength = entry.IsDirectory
                    ? 0
                    : usesZip64DataDescriptor ? 24 : 16;

                plans[i] = new StoredZipArchiveWriter.ZipEntryPlan(
                    entry.Path,
                    pathBytes,
                    entry.SizeBytes,
                    entry.IsDirectory,
                    usesZip64DataDescriptor,
                    offset);
                offset += localHeaderLength + entry.SizeBytes + dataDescriptorLength;
            }

            long centralDirectoryOffset = offset;
            long centralDirectoryLength = 0;
            for (int i = 0; i < plans.Length; i++)
            {
                StoredZipArchiveWriter.ZipEntryPlan entry = plans[i];
                long centralExtraLength = StoredZipArchiveWriter.GetCentralZip64ExtraLength(
                    entry.SizeBytes,
                    entry.LocalHeaderOffset);
                plans[i] = entry with { CentralExtraLength = centralExtraLength };
                centralDirectoryLength += 46 + entry.PathBytes.Length + centralExtraLength;
            }

            bool needsZip64End = StoredZipArchiveWriter.RequiresZip64UInt16(entries.Count)
                || StoredZipArchiveWriter.RequiresZip64UInt32(centralDirectoryOffset)
                || StoredZipArchiveWriter.RequiresZip64UInt32(centralDirectoryLength);
            long endLength = (needsZip64End ? 76 : 0) + 22;
            return new StoredZipArchiveWriter.ZipPlan(
                plans,
                centralDirectoryOffset,
                centralDirectoryLength,
                needsZip64End,
                centralDirectoryOffset + centralDirectoryLength + endLength);
        }
    }
}
