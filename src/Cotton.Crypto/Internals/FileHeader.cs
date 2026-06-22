// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using System.Buffers.Binary;

namespace Cotton.Crypto.Internals
{
    internal readonly struct FileHeader(int keyId, uint noncePrefix, byte[] nonce, Tag128 tag, byte[] encryptedKey, long totalLength, int formatVersion = FormatConstants.CurrentVersion)
    {
        public int FormatVersion { get; } = formatVersion;
        public int KeyId { get; } = keyId;
        public uint NoncePrefix { get; } = noncePrefix;
        public byte[] Nonce { get; } = nonce;
        public Tag128 Tag { get; } = tag;
        public byte[] EncryptedKey { get; } = encryptedKey;
        public long TotalPlaintextLength { get; } = totalLength;

        public static int ComputeLength(int nonceSize, int tagSize, int keySize)
            => 4 + 4 + 8 + 4 + 4 + nonceSize + tagSize + keySize;

        public static bool TryWrite(Span<byte> destination, in FileHeader header, int nonceSize, int tagSize, int keySize)
        {
            if (tagSize != 16) return false; // only 16-byte tags supported for file header
            int required = ComputeLength(nonceSize, tagSize, keySize);
            if (destination.Length < required) return false;
            int offset = 0;
            FormatConstants.GetMagicBytes(header.FormatVersion).CopyTo(destination[offset..]); offset += 4;
            BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], required); offset += 4;
            BinaryPrimitives.WriteInt64LittleEndian(destination[offset..], header.TotalPlaintextLength); offset += 8;
            BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], header.KeyId); offset += 4;
            BinaryPrimitives.WriteUInt32LittleEndian(destination[offset..], header.NoncePrefix); offset += 4;
            header.Nonce.AsSpan(0, nonceSize).CopyTo(destination[offset..]); offset += nonceSize;
            header.Tag.CopyTo(destination[offset..(offset + tagSize)]); offset += tagSize;
            header.EncryptedKey.AsSpan(0, keySize).CopyTo(destination[offset..]);
            return true;
        }

        public static bool TryRead(ReadOnlySpan<byte> source, int nonceSize, int tagSize, int keySize, out FileHeader header)
        {
            header = default;
            if (tagSize != 16) return false; // only 16-byte tags supported for file header
            if (source.Length < 8) return false;
            if (!FormatConstants.TryGetVersion(source[..4], out int formatVersion)) return false;
            int expected = ComputeLength(nonceSize, tagSize, keySize);
            int len = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(4, 4));
            if (len != expected || source.Length < expected) return false;
            int offset = 8;
            long total = BinaryPrimitives.ReadInt64LittleEndian(source[offset..]); offset += 8;
            int keyId = BinaryPrimitives.ReadInt32LittleEndian(source[offset..]); offset += 4;
            uint prefix = BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]); offset += 4;
            byte[] nonce = source.Slice(offset, nonceSize).ToArray(); offset += nonceSize;
            Tag128 tag = Tag128.FromSpan(source.Slice(offset, tagSize)); offset += tagSize;
            byte[] encKey = source.Slice(offset, keySize).ToArray();
            header = new FileHeader(keyId, prefix, nonce, tag, encKey, total, formatVersion);
            return true;
        }
    }
}
