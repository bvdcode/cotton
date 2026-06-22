// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    internal static class AndroidBinaryXmlReader
    {
        public const ushort ResXmlTreeType = 0x0003;
        public const ushort ResStringPoolType = 0x0001;
        public const ushort ResXmlResourceMapType = 0x0180;
        public const ushort ResXmlStartElementType = 0x0102;

        private const byte ResValueDataTypeReference = 0x01;
        private const uint ResStringPoolRefNone = 0xFFFFFFFF;
        private const int ResXmlStartElementHeaderSize = 36;
        private const int ResXmlAttributeMinSize = 20;

        public static bool TryReadTreeHeader(byte[] xmlBytes, out int headerSize, out int xmlSize)
        {
            headerSize = 0;
            xmlSize = 0;
            if (!AndroidBinaryResourceReader.HasRange(xmlBytes, 0, 8)
                || AndroidBinaryResourceReader.ReadUInt16(xmlBytes, 0) != ResXmlTreeType)
            {
                return false;
            }

            headerSize = AndroidBinaryResourceReader.ReadUInt16(xmlBytes, 2);
            xmlSize = AndroidBinaryResourceReader.ClampChunkSize(xmlBytes, 0);
            return xmlSize != 0 && headerSize > 0 && headerSize < xmlSize;
        }

        public static bool TryReadChunkHeader(
            byte[] xmlBytes,
            int offset,
            out ushort chunkType,
            out int chunkSize)
        {
            chunkType = 0;
            chunkSize = 0;
            if (!AndroidBinaryResourceReader.HasRange(xmlBytes, offset, 8))
            {
                return false;
            }

            chunkType = AndroidBinaryResourceReader.ReadUInt16(xmlBytes, offset);
            chunkSize = AndroidBinaryResourceReader.ClampChunkSize(xmlBytes, offset);
            return chunkSize != 0;
        }

        public static uint[] ReadResourceMap(byte[] xmlBytes, int chunkOffset, int chunkSize)
        {
            int headerSize = AndroidBinaryResourceReader.ReadUInt16(xmlBytes, chunkOffset + 2);
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
                map[i] = AndroidBinaryResourceReader.ReadUInt32(xmlBytes, offset + (i * sizeof(uint)));
            }

            return map;
        }

        public static bool TryReadStartElementName(
            byte[] xmlBytes,
            int chunkOffset,
            IReadOnlyList<string> strings,
            out string elementName)
        {
            elementName = string.Empty;
            if (!AndroidBinaryResourceReader.HasRange(xmlBytes, chunkOffset, ResXmlStartElementHeaderSize))
            {
                return false;
            }

            uint elementNameIndex = AndroidBinaryResourceReader.ReadUInt32(xmlBytes, chunkOffset + 20);
            return TryGetString(strings, elementNameIndex, out elementName);
        }

        public static bool TryReadReferenceAttributeResourceId(
            byte[] xmlBytes,
            int chunkOffset,
            IReadOnlyList<uint> resourceMap,
            uint attributeResourceId,
            out uint resourceId)
        {
            resourceId = 0;
            if (!AndroidBinaryResourceReader.HasRange(xmlBytes, chunkOffset, ResXmlStartElementHeaderSize))
            {
                return false;
            }

            int attributeStart = AndroidBinaryResourceReader.ReadUInt16(xmlBytes, chunkOffset + 24);
            int attributeSize = AndroidBinaryResourceReader.ReadUInt16(xmlBytes, chunkOffset + 26);
            int attributeCount = AndroidBinaryResourceReader.ReadUInt16(xmlBytes, chunkOffset + 28);
            if (attributeSize < ResXmlAttributeMinSize
                || attributeCount <= 0
                || attributeCount > int.MaxValue / attributeSize)
            {
                return false;
            }

            long attributeBaseValue = (long)chunkOffset + 16 + attributeStart;
            if (attributeBaseValue > int.MaxValue)
            {
                return false;
            }

            int attributeBase = (int)attributeBaseValue;
            int attributeByteCount = attributeCount * attributeSize;
            if (!AndroidBinaryResourceReader.HasRange(xmlBytes, attributeBase, attributeByteCount))
            {
                return false;
            }

            for (int i = 0; i < attributeCount; i++)
            {
                int attributeOffset = attributeBase + (i * attributeSize);
                uint attributeNameIndex = AndroidBinaryResourceReader.ReadUInt32(xmlBytes, attributeOffset + 4);
                if (!IsResourceMapEntry(attributeNameIndex, resourceMap, attributeResourceId))
                {
                    continue;
                }

                byte dataType = xmlBytes[attributeOffset + 15];
                uint data = AndroidBinaryResourceReader.ReadUInt32(xmlBytes, attributeOffset + 16);
                if (dataType == ResValueDataTypeReference && data != 0)
                {
                    resourceId = data;
                    return true;
                }
            }

            return false;
        }

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

        private static bool IsResourceMapEntry(
            uint nameIndex,
            IReadOnlyList<uint> resourceMap,
            uint resourceId)
            => nameIndex != ResStringPoolRefNone
                && nameIndex < resourceMap.Count
                && resourceMap[(int)nameIndex] == resourceId;
    }
}
