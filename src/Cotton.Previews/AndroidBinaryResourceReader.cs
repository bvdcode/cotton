// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers.Binary;
using System.Text;

namespace Cotton.Previews
{
    internal static class AndroidBinaryResourceReader
    {
        private const ushort ResStringPoolType = 0x0001;
        private const int StringPoolUtf8Flag = 0x00000100;

        public static string[] ReadStringPool(byte[] data, int chunkOffset, out int chunkSize)
        {
            chunkSize = 0;
            if (!HasRange(data, chunkOffset, 28) || ReadUInt16(data, chunkOffset) != ResStringPoolType)
            {
                return [];
            }

            int headerSize = ReadUInt16(data, chunkOffset + 2);
            chunkSize = ClampChunkSize(data, chunkOffset);
            if (chunkSize == 0 || headerSize <= 0 || headerSize > chunkSize)
            {
                return [];
            }

            uint stringCountValue = ReadUInt32(data, chunkOffset + 8);
            if (stringCountValue > int.MaxValue / sizeof(uint))
            {
                return [];
            }

            int stringCount = (int)stringCountValue;
            int flags = (int)ReadUInt32(data, chunkOffset + 16);
            uint stringsStartValue = ReadUInt32(data, chunkOffset + 20);
            if (stringsStartValue > int.MaxValue || stringsStartValue >= chunkSize)
            {
                return [];
            }

            int stringsStart = (int)stringsStartValue;
            int offsetsStart = chunkOffset + headerSize;
            if (!HasRange(data, offsetsStart, stringCount * sizeof(uint)))
            {
                return [];
            }

            var strings = new string[stringCount];
            bool isUtf8 = (flags & StringPoolUtf8Flag) != 0;
            int stringsBase = chunkOffset + stringsStart;
            int chunkEnd = chunkOffset + chunkSize;
            for (int i = 0; i < stringCount; i++)
            {
                uint offsetValue = ReadUInt32(data, offsetsStart + (i * sizeof(uint)));
                if (offsetValue > int.MaxValue)
                {
                    strings[i] = string.Empty;
                    continue;
                }

                int stringOffset = stringsBase + (int)offsetValue;
                strings[i] = ReadStringPoolString(data, stringOffset, chunkEnd, isUtf8);
            }

            return strings;
        }

        public static int ClampChunkSize(byte[] data, int offset)
        {
            if (!HasRange(data, offset, 8))
            {
                return 0;
            }

            uint chunkSize = ReadUInt32(data, offset + 4);
            return chunkSize == 0 || chunkSize > int.MaxValue || chunkSize > data.Length - offset
                ? 0
                : (int)chunkSize;
        }

        public static bool HasRange(byte[] data, int offset, int length) =>
            offset >= 0 && length >= 0 && offset <= data.Length - length;

        public static ushort ReadUInt16(byte[] data, int offset) =>
            BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, sizeof(ushort)));

        public static uint ReadUInt32(byte[] data, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)));

        private static string ReadStringPoolString(byte[] data, int offset, int limit, bool isUtf8)
        {
            if (offset < 0 || offset >= limit || offset >= data.Length)
            {
                return string.Empty;
            }

            int current = offset;
            if (isUtf8)
            {
                if (!TryReadStringPoolUtf8Length(data, ref current, limit, out _)
                    || !TryReadStringPoolUtf8Length(data, ref current, limit, out int byteLength)
                    || byteLength < 0
                    || current > limit - byteLength
                    || current > data.Length - byteLength)
                {
                    return string.Empty;
                }

                return Encoding.UTF8.GetString(data, current, byteLength);
            }

            if (!TryReadStringPoolUtf16Length(data, ref current, limit, out int charLength)
                || charLength < 0
                || charLength > (limit - current) / sizeof(char)
                || charLength > (data.Length - current) / sizeof(char))
            {
                return string.Empty;
            }

            return Encoding.Unicode.GetString(data, current, charLength * sizeof(char));
        }

        private static bool TryReadStringPoolUtf8Length(byte[] data, ref int offset, int limit, out int length)
        {
            length = 0;
            if (offset >= limit || offset >= data.Length)
            {
                return false;
            }

            int first = data[offset++];
            if ((first & 0x80) == 0)
            {
                length = first;
                return true;
            }

            if (offset >= limit || offset >= data.Length)
            {
                return false;
            }

            length = ((first & 0x7F) << 8) | data[offset++];
            return true;
        }

        private static bool TryReadStringPoolUtf16Length(byte[] data, ref int offset, int limit, out int length)
        {
            length = 0;
            if (!HasRange(data, offset, sizeof(ushort)) || offset > limit - sizeof(ushort))
            {
                return false;
            }

            int first = ReadUInt16(data, offset);
            offset += sizeof(ushort);
            if ((first & 0x8000) == 0)
            {
                length = first;
                return true;
            }

            if (!HasRange(data, offset, sizeof(ushort)) || offset > limit - sizeof(ushort))
            {
                return false;
            }

            length = ((first & 0x7FFF) << 16) | ReadUInt16(data, offset);
            offset += sizeof(ushort);
            return true;
        }
    }
}
