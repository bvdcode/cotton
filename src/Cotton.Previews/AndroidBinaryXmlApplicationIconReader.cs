// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    internal static class AndroidBinaryXmlApplicationIconReader
    {
        private const uint AndroidIconAttributeResourceId = 0x01010002;

        public static bool TryReadApplicationIconResourceId(byte[] manifestBytes, out uint resourceId)
        {
            resourceId = 0;
            if (!AndroidBinaryXmlReader.TryReadTreeHeader(manifestBytes, out int xmlHeaderSize, out int xmlSize))
            {
                return false;
            }

            int offset = xmlHeaderSize;
            string[] strings = [];
            uint[] resourceMap = [];
            while (AndroidBinaryResourceReader.HasRange(manifestBytes, offset, 8) && offset < xmlSize)
            {
                if (!AndroidBinaryXmlReader.TryReadChunkHeader(
                    manifestBytes,
                    offset,
                    out ushort chunkType,
                    out int chunkSize))
                {
                    return false;
                }

                if (chunkType == AndroidBinaryXmlReader.ResStringPoolType)
                {
                    strings = AndroidBinaryResourceReader.ReadStringPool(manifestBytes, offset, out _);
                }
                else if (chunkType == AndroidBinaryXmlReader.ResXmlResourceMapType)
                {
                    resourceMap = AndroidBinaryXmlReader.ReadResourceMap(manifestBytes, offset, chunkSize);
                }
                else if (chunkType == AndroidBinaryXmlReader.ResXmlStartElementType
                    && TryReadStartElementIconResourceId(manifestBytes, offset, strings, resourceMap, out resourceId))
                {
                    return true;
                }

                offset += chunkSize;
            }

            return false;
        }

        private static bool TryReadStartElementIconResourceId(
            byte[] manifestBytes,
            int chunkOffset,
            IReadOnlyList<string> strings,
            IReadOnlyList<uint> resourceMap,
            out uint resourceId)
        {
            resourceId = 0;
            if (!AndroidBinaryXmlReader.TryReadStartElementName(manifestBytes, chunkOffset, strings, out string elementName)
                || !string.Equals(elementName, "application", StringComparison.Ordinal))
            {
                return false;
            }

            return AndroidBinaryXmlReader.TryReadReferenceAttributeResourceId(
                manifestBytes,
                chunkOffset,
                resourceMap,
                AndroidIconAttributeResourceId,
                out resourceId);
        }
    }
}
