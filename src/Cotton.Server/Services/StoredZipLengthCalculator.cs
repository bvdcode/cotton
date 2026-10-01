// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;

namespace Cotton.Server.Services
{
    public class StoredZipLengthCalculator
    {
        public int EntryCount { get; private set; }
        public long CentralDirectoryOffset { get; private set; }
        public long CentralDirectoryLength { get; private set; }
        public bool NeedsZip64End => EntryCount >= ushort.MaxValue
            || CentralDirectoryOffset >= uint.MaxValue || CentralDirectoryLength >= uint.MaxValue;
        public long TotalLength => checked(CentralDirectoryOffset + CentralDirectoryLength + 22 + (NeedsZip64End ? 76 : 0));

        public void Add(IStoredZipEntry entry)
        {
            AddEntry(entry);
        }

        internal ZipEntryPlan AddEntry(IStoredZipEntry entry)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(entry.SizeBytes);
            byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
            if (pathBytes.Length == 0 || pathBytes.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException("Archive entry path has invalid UTF-8 length.");
            }

            bool zip64 = !entry.IsDirectory && entry.SizeBytes >= uint.MaxValue;
            ZipEntryPlan plan = new(entry.Path, pathBytes, entry.SizeBytes, entry.IsDirectory, zip64, CentralDirectoryOffset)
            {
                CentralExtraLength = StoredZipHeaders.GetCentralZip64ExtraLength(entry.SizeBytes, CentralDirectoryOffset),
            };
            int descriptorLength = entry.IsDirectory ? 0 : zip64 ? 24 : 16;
            CentralDirectoryOffset = checked(CentralDirectoryOffset + 30 + pathBytes.Length + entry.SizeBytes + descriptorLength);
            CentralDirectoryLength = checked(CentralDirectoryLength + 46 + pathBytes.Length + plan.CentralExtraLength);
            EntryCount = checked(EntryCount + 1);
            return plan;
        }
    }
}
