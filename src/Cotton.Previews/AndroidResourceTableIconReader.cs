// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    internal static class AndroidResourceTableIconReader
    {
        private const ushort ResStringPoolType = 0x0001;
        private const ushort ResTableType = 0x0002;
        private const ushort ResTablePackageType = 0x0200;
        private const ushort ResTableTypeType = 0x0201;
        private const ushort ResourceEntryFlagComplex = 0x0001;
        private const uint ResourceEntryNoEntry = 0xFFFFFFFF;
        private const byte ResValueDataTypeString = 0x03;
        private const int DensityAny = 0xFFFE;
        private const int DensityNone = 0xFFFF;

        public static IReadOnlyList<AndroidResourcePathCandidate> ReadIconResourcePaths(
            byte[] resourceTableBytes,
            uint iconResourceId)
            => ReadResourcePaths(resourceTableBytes, iconResourceId, IsRasterCandidatePath);

        public static IReadOnlyList<AndroidResourcePathCandidate> ReadXmlResourcePaths(
            byte[] resourceTableBytes,
            uint resourceId)
            => ReadResourcePaths(resourceTableBytes, resourceId, IsXmlResourcePath);

        private static IReadOnlyList<AndroidResourcePathCandidate> ReadResourcePaths(
            byte[] resourceTableBytes,
            uint resourceId,
            Func<string, bool> pathFilter)
        {
            var candidates = new List<AndroidResourcePathCandidate>();
            if (!AndroidBinaryResourceReader.HasRange(resourceTableBytes, 0, 12)
                || AndroidBinaryResourceReader.ReadUInt16(resourceTableBytes, 0) != ResTableType)
            {
                return candidates;
            }

            int tableHeaderSize = AndroidBinaryResourceReader.ReadUInt16(resourceTableBytes, 2);
            int tableSize = AndroidBinaryResourceReader.ClampChunkSize(resourceTableBytes, 0);
            if (tableSize == 0 || tableHeaderSize <= 0 || tableHeaderSize >= tableSize)
            {
                return candidates;
            }

            int packageId = (int)((resourceId >> 24) & 0xFF);
            int typeId = (int)((resourceId >> 16) & 0xFF);
            int entryIndex = (int)(resourceId & 0xFFFF);
            if (packageId <= 0 || typeId <= 0 || entryIndex < 0)
            {
                return candidates;
            }

            int offset = tableHeaderSize;
            string[] globalStrings = [];
            if (AndroidBinaryResourceReader.HasRange(resourceTableBytes, offset, 8)
                && AndroidBinaryResourceReader.ReadUInt16(resourceTableBytes, offset) == ResStringPoolType)
            {
                globalStrings = AndroidBinaryResourceReader.ReadStringPool(resourceTableBytes, offset, out int stringPoolSize);
                offset += stringPoolSize;
            }

            while (AndroidBinaryResourceReader.HasRange(resourceTableBytes, offset, 8) && offset < tableSize)
            {
                ushort chunkType = AndroidBinaryResourceReader.ReadUInt16(resourceTableBytes, offset);
                int chunkSize = AndroidBinaryResourceReader.ClampChunkSize(resourceTableBytes, offset);
                if (chunkSize == 0)
                {
                    break;
                }

                if (chunkType == ResTablePackageType)
                {
                    AddPackageIconPaths(
                        resourceTableBytes,
                        offset,
                        chunkSize,
                        packageId,
                        typeId,
                        entryIndex,
                        globalStrings,
                        pathFilter,
                        candidates);
                }

                offset += chunkSize;
            }

            return [.. candidates.OrderByDescending(x => x.Score)];
        }

        private static void AddPackageIconPaths(
            byte[] resourceTableBytes,
            int packageOffset,
            int packageSize,
            int packageId,
            int typeId,
            int entryIndex,
            IReadOnlyList<string> globalStrings,
            Func<string, bool> pathFilter,
            List<AndroidResourcePathCandidate> candidates)
        {
            if (!AndroidBinaryResourceReader.HasRange(resourceTableBytes, packageOffset, 284))
            {
                return;
            }

            int currentPackageId = (int)AndroidBinaryResourceReader.ReadUInt32(resourceTableBytes, packageOffset + 8);
            if (currentPackageId != packageId)
            {
                return;
            }

            int packageHeaderSize = AndroidBinaryResourceReader.ReadUInt16(resourceTableBytes, packageOffset + 2);
            int packageEnd = packageOffset + packageSize;
            uint typeStringsOffsetValue = AndroidBinaryResourceReader.ReadUInt32(resourceTableBytes, packageOffset + 268);
            uint keyStringsOffsetValue = AndroidBinaryResourceReader.ReadUInt32(resourceTableBytes, packageOffset + 276);
            if (packageHeaderSize <= 0
                || packageHeaderSize > packageSize
                || typeStringsOffsetValue > int.MaxValue
                || keyStringsOffsetValue > int.MaxValue)
            {
                return;
            }

            int typeStringsOffset = packageOffset + (int)typeStringsOffsetValue;
            int keyStringsOffset = packageOffset + (int)keyStringsOffsetValue;
            string[] typeStrings = AndroidBinaryResourceReader.ReadStringPool(resourceTableBytes, typeStringsOffset, out _);
            string[] keyStrings = AndroidBinaryResourceReader.ReadStringPool(resourceTableBytes, keyStringsOffset, out _);

            int offset = packageOffset + packageHeaderSize;
            while (AndroidBinaryResourceReader.HasRange(resourceTableBytes, offset, 8) && offset < packageEnd)
            {
                ushort chunkType = AndroidBinaryResourceReader.ReadUInt16(resourceTableBytes, offset);
                int chunkSize = AndroidBinaryResourceReader.ClampChunkSize(resourceTableBytes, offset);
                if (chunkSize == 0)
                {
                    break;
                }

                if (chunkType == ResTableTypeType)
                {
                    AddTypeIconPath(
                        resourceTableBytes,
                        offset,
                        typeId,
                        entryIndex,
                        typeStrings,
                        keyStrings,
                        globalStrings,
                        pathFilter,
                        candidates);
                }

                offset += chunkSize;
            }
        }

        private static void AddTypeIconPath(
            byte[] resourceTableBytes,
            int typeOffset,
            int requestedTypeId,
            int requestedEntryIndex,
            IReadOnlyList<string> typeStrings,
            IReadOnlyList<string> keyStrings,
            IReadOnlyList<string> globalStrings,
            Func<string, bool> pathFilter,
            List<AndroidResourcePathCandidate> candidates)
        {
            (int HeaderSize, int TypeId, int EntryCount, int EntriesStart)? typeChunk =
                TryReadTypeChunk(resourceTableBytes, typeOffset, requestedTypeId);
            if (typeChunk is null)
            {
                return;
            }

            (int headerSize, int typeId, int entryCount, int entriesStart) = typeChunk.Value;
            int? entryPosition = TryReadEntryPosition(
                resourceTableBytes,
                typeOffset,
                headerSize,
                entryCount,
                entriesStart,
                requestedEntryIndex);
            if (!entryPosition.HasValue)
            {
                return;
            }

            string? path = TryReadResourcePath(
                resourceTableBytes,
                entryPosition.Value,
                keyStrings,
                globalStrings);
            if (path is null || !pathFilter(path))
            {
                return;
            }

            int density = ReadDensity(resourceTableBytes, typeOffset);
            int score = ScoreDensity(density) + ScoreResourcePath(path);
            if (typeId > 0 && typeId <= typeStrings.Count && typeStrings[typeId - 1] == "mipmap")
            {
                score += 1_000;
            }

            candidates.Add(new AndroidResourcePathCandidate(path, density, score));
        }

        private static (int HeaderSize, int TypeId, int EntryCount, int EntriesStart)? TryReadTypeChunk(
            byte[] resourceTableBytes,
            int typeOffset,
            int requestedTypeId)
        {
            if (!AndroidBinaryResourceReader.HasRange(resourceTableBytes, typeOffset, 20))
            {
                return null;
            }

            int headerSize = AndroidBinaryResourceReader.ReadUInt16(resourceTableBytes, typeOffset + 2);
            int chunkSize = AndroidBinaryResourceReader.ClampChunkSize(resourceTableBytes, typeOffset);
            int typeId = resourceTableBytes[typeOffset + 8];
            uint entryCountValue = AndroidBinaryResourceReader.ReadUInt32(resourceTableBytes, typeOffset + 12);
            uint entriesStartValue = AndroidBinaryResourceReader.ReadUInt32(resourceTableBytes, typeOffset + 16);
            if (typeId != requestedTypeId
                || chunkSize == 0
                || headerSize <= 0
                || headerSize > chunkSize
                || entriesStartValue > int.MaxValue
                || entryCountValue > int.MaxValue / sizeof(uint))
            {
                return null;
            }

            int entryCount = (int)entryCountValue;
            int entriesStart = (int)entriesStartValue;
            if (entriesStart <= 0
                || entriesStart > chunkSize
                || !AndroidBinaryResourceReader.HasRange(
                    resourceTableBytes,
                    typeOffset + headerSize,
                    entryCount * sizeof(uint)))
            {
                return null;
            }

            return (headerSize, typeId, entryCount, entriesStart);
        }

        private static int? TryReadEntryPosition(
            byte[] resourceTableBytes,
            int typeOffset,
            int headerSize,
            int entryCount,
            int entriesStart,
            int requestedEntryIndex)
        {
            if (requestedEntryIndex >= entryCount)
            {
                return null;
            }

            uint entryOffset = AndroidBinaryResourceReader.ReadUInt32(
                resourceTableBytes,
                typeOffset + headerSize + (requestedEntryIndex * sizeof(uint)));
            if (entryOffset == ResourceEntryNoEntry || entryOffset > int.MaxValue)
            {
                return null;
            }

            int entryPosition = typeOffset + entriesStart + (int)entryOffset;
            return AndroidBinaryResourceReader.HasRange(resourceTableBytes, entryPosition, 16)
                ? entryPosition
                : null;
        }

        private static string? TryReadResourcePath(
            byte[] resourceTableBytes,
            int entryPosition,
            IReadOnlyList<string> keyStrings,
            IReadOnlyList<string> globalStrings)
        {
            int entrySize = AndroidBinaryResourceReader.ReadUInt16(resourceTableBytes, entryPosition);
            ushort flags = AndroidBinaryResourceReader.ReadUInt16(resourceTableBytes, entryPosition + 2);
            int keyIndex = (int)AndroidBinaryResourceReader.ReadUInt32(resourceTableBytes, entryPosition + 4);
            int valuePosition = entryPosition + entrySize;
            if ((flags & ResourceEntryFlagComplex) != 0
                || entrySize <= 0
                || keyIndex < 0
                || keyIndex >= keyStrings.Count
                || !AndroidBinaryResourceReader.HasRange(resourceTableBytes, valuePosition, 8))
            {
                return null;
            }

            byte dataType = resourceTableBytes[valuePosition + 3];
            int stringIndex = (int)AndroidBinaryResourceReader.ReadUInt32(resourceTableBytes, valuePosition + 4);
            if (dataType != ResValueDataTypeString || stringIndex < 0 || stringIndex >= globalStrings.Count)
            {
                return null;
            }

            return NormalizeResourcePath(globalStrings[stringIndex]);
        }

        private static int ReadDensity(byte[] resourceTableBytes, int typeOffset)
        {
            int configOffset = typeOffset + 20;
            if (!AndroidBinaryResourceReader.HasRange(resourceTableBytes, configOffset, 16))
            {
                return 0;
            }

            uint configSize = AndroidBinaryResourceReader.ReadUInt32(resourceTableBytes, configOffset);
            return configSize >= 16
                ? AndroidBinaryResourceReader.ReadUInt16(resourceTableBytes, configOffset + 14)
                : 0;
        }

        private static bool IsRasterCandidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.EndsWith(".9.png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension is ".png" or ".webp" or ".jpg" or ".jpeg"
                || (extension.Length == 0 && path.StartsWith("res/", StringComparison.Ordinal));
        }

        private static bool IsXmlResourcePath(string path) =>
            Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase)
            && path.StartsWith("res/", StringComparison.Ordinal);

        private static int ScoreDensity(int density)
        {
            if (density is DensityAny or DensityNone)
            {
                return 250;
            }

            return density > 0 ? density * 10 : 100;
        }

        private static int ScoreResourcePath(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension is ".png" or ".webp" ? 1_000 : 500;
        }

        private static string NormalizeResourcePath(string path) =>
            path.Replace('\\', '/').TrimStart('/');
    }
}
