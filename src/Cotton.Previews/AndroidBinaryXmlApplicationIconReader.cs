// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    internal static class AndroidBinaryXmlApplicationIconReader
    {
        private const ushort ResXmlTreeType = 0x0003;
        private const ushort ResStringPoolType = 0x0001;
        private const ushort ResXmlResourceMapType = 0x0180;
        private const ushort ResXmlStartElementType = 0x0102;
        private const uint AndroidIconAttributeResourceId = 0x01010002;
        private const byte ResValueDataTypeReference = 0x01;
        private const uint ResStringPoolRefNone = 0xFFFFFFFF;

        public static bool TryReadApplicationIconResourceId(byte[] manifestBytes, out uint resourceId)
        {
            resourceId = 0;
            if (!AndroidBinaryResourceReader.HasRange(manifestBytes, 0, 8)
                || AndroidBinaryResourceReader.ReadUInt16(manifestBytes, 0) != ResXmlTreeType)
            {
                return false;
            }

            int xmlHeaderSize = AndroidBinaryResourceReader.ReadUInt16(manifestBytes, 2);
            int xmlSize = AndroidBinaryResourceReader.ClampChunkSize(manifestBytes, 0);
            if (xmlSize == 0 || xmlHeaderSize <= 0 || xmlHeaderSize >= xmlSize)
            {
                return false;
            }

            int offset = xmlHeaderSize;
            string[] strings = [];
            uint[] resourceMap = [];
            while (AndroidBinaryResourceReader.HasRange(manifestBytes, offset, 8) && offset < xmlSize)
            {
                ushort chunkType = AndroidBinaryResourceReader.ReadUInt16(manifestBytes, offset);
                int chunkSize = AndroidBinaryResourceReader.ClampChunkSize(manifestBytes, offset);
                if (chunkSize == 0)
                {
                    return false;
                }

                if (chunkType == ResStringPoolType)
                {
                    strings = AndroidBinaryResourceReader.ReadStringPool(manifestBytes, offset, out _);
                }
                else if (chunkType == ResXmlResourceMapType)
                {
                    resourceMap = ReadResourceMap(manifestBytes, offset, chunkSize);
                }
                else if (chunkType == ResXmlStartElementType
                    && TryReadStartElementIconResourceId(manifestBytes, offset, strings, resourceMap, out resourceId))
                {
                    return true;
                }

                offset += chunkSize;
            }

            return false;
        }

        private static uint[] ReadResourceMap(byte[] manifestBytes, int chunkOffset, int chunkSize)
        {
            int headerSize = AndroidBinaryResourceReader.ReadUInt16(manifestBytes, chunkOffset + 2);
            int byteCount = chunkSize - headerSize;
            if (headerSize <= 0 || byteCount < 0 || byteCount % sizeof(uint) != 0)
            {
                return [];
            }

            int count = byteCount / sizeof(uint);
            var map = new uint[count];
            int offset = chunkOffset + headerSize;
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = AndroidBinaryResourceReader.ReadUInt32(manifestBytes, offset + (i * sizeof(uint)));
            }

            return map;
        }

        private static bool TryReadStartElementIconResourceId(
            byte[] manifestBytes,
            int chunkOffset,
            IReadOnlyList<string> strings,
            IReadOnlyList<uint> resourceMap,
            out uint resourceId)
        {
            resourceId = 0;
            if (!AndroidBinaryResourceReader.HasRange(manifestBytes, chunkOffset, 36))
            {
                return false;
            }

            uint elementNameIndex = AndroidBinaryResourceReader.ReadUInt32(manifestBytes, chunkOffset + 20);
            if (!TryGetString(strings, elementNameIndex, out string elementName)
                || !string.Equals(elementName, "application", StringComparison.Ordinal))
            {
                return false;
            }

            int attributeStart = AndroidBinaryResourceReader.ReadUInt16(manifestBytes, chunkOffset + 24);
            int attributeSize = AndroidBinaryResourceReader.ReadUInt16(manifestBytes, chunkOffset + 26);
            int attributeCount = AndroidBinaryResourceReader.ReadUInt16(manifestBytes, chunkOffset + 28);
            if (attributeSize < 20 || attributeCount <= 0)
            {
                return false;
            }

            int attributeBase = chunkOffset + 16 + attributeStart;
            if (!AndroidBinaryResourceReader.HasRange(manifestBytes, attributeBase, attributeCount * attributeSize))
            {
                return false;
            }

            for (int i = 0; i < attributeCount; i++)
            {
                int attributeOffset = attributeBase + (i * attributeSize);
                uint attributeNameIndex = AndroidBinaryResourceReader.ReadUInt32(manifestBytes, attributeOffset + 4);
                if (!IsAndroidIconAttribute(attributeNameIndex, resourceMap))
                {
                    continue;
                }

                byte dataType = manifestBytes[attributeOffset + 15];
                uint data = AndroidBinaryResourceReader.ReadUInt32(manifestBytes, attributeOffset + 16);
                if (dataType == ResValueDataTypeReference && data != 0)
                {
                    resourceId = data;
                    return true;
                }
            }

            return false;
        }

        private static bool IsAndroidIconAttribute(
            uint attributeNameIndex,
            IReadOnlyList<uint> resourceMap)
            => attributeNameIndex != ResStringPoolRefNone
                && attributeNameIndex < resourceMap.Count
                && resourceMap[(int)attributeNameIndex] == AndroidIconAttributeResourceId;

        private static bool TryGetString(IReadOnlyList<string> strings, uint index, out string value)
        {
            if (index == ResStringPoolRefNone || index >= strings.Count)
            {
                value = string.Empty;
                return false;
            }

            value = strings[(int)index];
            return true;
        }
    }
}
