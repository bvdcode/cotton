// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    internal static class AndroidAdaptiveIconXmlReader
    {
        private const ushort ResXmlTreeType = 0x0003;
        private const ushort ResStringPoolType = 0x0001;
        private const ushort ResXmlResourceMapType = 0x0180;
        private const ushort ResXmlStartElementType = 0x0102;
        private const uint AndroidDrawableAttributeResourceId = 0x01010199;
        private const byte ResValueDataTypeReference = 0x01;
        private const uint ResStringPoolRefNone = 0xFFFFFFFF;

        public static bool TryReadLayerResourceIds(
            byte[] xmlBytes,
            out uint? backgroundResourceId,
            out uint? foregroundResourceId)
        {
            backgroundResourceId = null;
            foregroundResourceId = null;
            if (!AndroidBinaryResourceReader.HasRange(xmlBytes, 0, 8)
                || AndroidBinaryResourceReader.ReadUInt16(xmlBytes, 0) != ResXmlTreeType)
            {
                return false;
            }

            int xmlHeaderSize = AndroidBinaryResourceReader.ReadUInt16(xmlBytes, 2);
            int xmlSize = AndroidBinaryResourceReader.ClampChunkSize(xmlBytes, 0);
            if (xmlSize == 0 || xmlHeaderSize <= 0 || xmlHeaderSize >= xmlSize)
            {
                return false;
            }

            int offset = xmlHeaderSize;
            string[] strings = [];
            uint[] resourceMap = [];
            while (AndroidBinaryResourceReader.HasRange(xmlBytes, offset, 8) && offset < xmlSize)
            {
                ushort chunkType = AndroidBinaryResourceReader.ReadUInt16(xmlBytes, offset);
                int chunkSize = AndroidBinaryResourceReader.ClampChunkSize(xmlBytes, offset);
                if (chunkSize == 0)
                {
                    return false;
                }

                if (chunkType == ResStringPoolType)
                {
                    strings = AndroidBinaryResourceReader.ReadStringPool(xmlBytes, offset, out _);
                }
                else if (chunkType == ResXmlResourceMapType)
                {
                    resourceMap = ReadResourceMap(xmlBytes, offset, chunkSize);
                }
                else if (chunkType == ResXmlStartElementType)
                {
                    ReadStartElementLayerResourceId(
                        xmlBytes,
                        offset,
                        strings,
                        resourceMap,
                        ref backgroundResourceId,
                        ref foregroundResourceId);
                }

                offset += chunkSize;
            }

            return backgroundResourceId.HasValue || foregroundResourceId.HasValue;
        }

        private static uint[] ReadResourceMap(byte[] xmlBytes, int chunkOffset, int chunkSize)
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

        private static void ReadStartElementLayerResourceId(
            byte[] xmlBytes,
            int chunkOffset,
            IReadOnlyList<string> strings,
            IReadOnlyList<uint> resourceMap,
            ref uint? backgroundResourceId,
            ref uint? foregroundResourceId)
        {
            if (!AndroidBinaryResourceReader.HasRange(xmlBytes, chunkOffset, 36))
            {
                return;
            }

            uint elementNameIndex = AndroidBinaryResourceReader.ReadUInt32(xmlBytes, chunkOffset + 20);
            if (!TryGetString(strings, elementNameIndex, out string elementName)
                || elementName is not ("background" or "foreground"))
            {
                return;
            }

            uint? layerResourceId = TryReadDrawableAttributeResourceId(xmlBytes, chunkOffset, resourceMap);
            if (!layerResourceId.HasValue)
            {
                return;
            }

            if (elementName == "background")
            {
                backgroundResourceId = layerResourceId.Value;
            }
            else
            {
                foregroundResourceId = layerResourceId.Value;
            }
        }

        private static uint? TryReadDrawableAttributeResourceId(
            byte[] xmlBytes,
            int chunkOffset,
            IReadOnlyList<uint> resourceMap)
        {
            int attributeStart = AndroidBinaryResourceReader.ReadUInt16(xmlBytes, chunkOffset + 24);
            int attributeSize = AndroidBinaryResourceReader.ReadUInt16(xmlBytes, chunkOffset + 26);
            int attributeCount = AndroidBinaryResourceReader.ReadUInt16(xmlBytes, chunkOffset + 28);
            if (attributeSize < 20 || attributeCount <= 0)
            {
                return null;
            }

            int attributeBase = chunkOffset + 16 + attributeStart;
            if (!AndroidBinaryResourceReader.HasRange(xmlBytes, attributeBase, attributeCount * attributeSize))
            {
                return null;
            }

            for (int i = 0; i < attributeCount; i++)
            {
                int attributeOffset = attributeBase + (i * attributeSize);
                uint attributeNameIndex = AndroidBinaryResourceReader.ReadUInt32(xmlBytes, attributeOffset + 4);
                if (attributeNameIndex == ResStringPoolRefNone
                    || attributeNameIndex >= resourceMap.Count
                    || resourceMap[(int)attributeNameIndex] != AndroidDrawableAttributeResourceId)
                {
                    continue;
                }

                byte dataType = xmlBytes[attributeOffset + 15];
                uint data = AndroidBinaryResourceReader.ReadUInt32(xmlBytes, attributeOffset + 16);
                if (dataType == ResValueDataTypeReference && data != 0)
                {
                    return data;
                }
            }

            return null;
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
    }
}
