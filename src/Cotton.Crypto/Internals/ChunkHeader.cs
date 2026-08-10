// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using System.Buffers.Binary;

namespace Cotton.Crypto.Internals
{
    internal readonly struct ChunkHeader(long length, int keyId, Tag128 tag)
    {
        public long PlaintextLength { get; } = length;
        public int KeyId { get; } = keyId;
        public Tag128 Tag { get; } = tag;

        public static int ComputeLength(int tagSize) => 4 + 4 + 8 + 4 + tagSize;

        public static bool TryWrite(Span<byte> destination, in ChunkHeader header, int tagSize)
        {
            int required = ComputeLength(tagSize);
            if (destination.Length < required) return false;
            int offset = 0;
            FormatConstants.MagicBytes.CopyTo(destination[offset..]); offset += 4;
            BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], required); offset += 4;
            BinaryPrimitives.WriteInt64LittleEndian(destination[offset..], header.PlaintextLength); offset += 8;
            BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], header.KeyId); offset += 4;
            header.Tag.CopyTo(destination[offset..(offset + tagSize)]);
            return true;
        }

        public static bool TryRead(ReadOnlySpan<byte> source, int tagSize, out ChunkHeader header)
        {
            header = default;
            int required = ComputeLength(tagSize);
            if (source.Length < required)
            {
                return false;
            }
            if (!source[..4].SequenceEqual(FormatConstants.MagicBytes))
            {
                return false;
            }
            int len = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(4, 4));
            if (len != required)
            {
                return false;
            }
            long pt = BinaryPrimitives.ReadInt64LittleEndian(source.Slice(8, 8));
            int kid = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(16, 4));
            Tag128 tag = Tag128.FromSpan(source.Slice(20, tagSize));
            header = new ChunkHeader(pt, kid, tag);
            return true;
        }
    }
}
