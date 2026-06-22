// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    internal static class AndroidAdaptiveIconXmlReader
    {
        private const uint AndroidDrawableAttributeResourceId = 0x01010199;

        public static bool TryReadLayerResourceIds(
            byte[] xmlBytes,
            out uint? backgroundResourceId,
            out uint? foregroundResourceId)
        {
            backgroundResourceId = null;
            foregroundResourceId = null;
            if (!AndroidBinaryXmlReader.TryReadTreeHeader(xmlBytes, out int xmlHeaderSize, out int xmlSize))
            {
                return false;
            }

            int offset = xmlHeaderSize;
            string[] strings = [];
            uint[] resourceMap = [];
            while (AndroidBinaryResourceReader.HasRange(xmlBytes, offset, 8) && offset < xmlSize)
            {
                if (!AndroidBinaryXmlReader.TryReadChunkHeader(
                    xmlBytes,
                    offset,
                    out ushort chunkType,
                    out int chunkSize))
                {
                    return false;
                }

                if (chunkType == AndroidBinaryXmlReader.ResStringPoolType)
                {
                    strings = AndroidBinaryResourceReader.ReadStringPool(xmlBytes, offset, out _);
                }
                else if (chunkType == AndroidBinaryXmlReader.ResXmlResourceMapType)
                {
                    resourceMap = AndroidBinaryXmlReader.ReadResourceMap(xmlBytes, offset, chunkSize);
                }
                else if (chunkType == AndroidBinaryXmlReader.ResXmlStartElementType)
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

        private static void ReadStartElementLayerResourceId(
            byte[] xmlBytes,
            int chunkOffset,
            IReadOnlyList<string> strings,
            IReadOnlyList<uint> resourceMap,
            ref uint? backgroundResourceId,
            ref uint? foregroundResourceId)
        {
            if (!AndroidBinaryXmlReader.TryReadStartElementName(xmlBytes, chunkOffset, strings, out string elementName)
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
            return AndroidBinaryXmlReader.TryReadReferenceAttributeResourceId(
                xmlBytes,
                chunkOffset,
                resourceMap,
                AndroidDrawableAttributeResourceId,
                out uint resourceId)
                ? resourceId
                : null;
        }
    }
}
