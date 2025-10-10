using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Cotton.Crypto.Internals
{
    // Internal representation of the file-level header
    internal readonly struct FileHeader(long totalLength, int keyId, uint noncePrefix, byte[] nonce, Tag128 tag, byte[] encryptedKey)
    {
        public long TotalPlaintextLength { get; } = totalLength;
        public int KeyId { get; } = keyId;
        public uint NoncePrefix { get; } = noncePrefix;
        public byte[] Nonce { get; } = nonce;
        public Tag128 Tag { get; } = tag;
        public byte[] EncryptedKey { get; } = encryptedKey;
    }

    // Internal representation of a per-chunk header (nonce validated on read; not stored)
    internal readonly struct ChunkHeader(long length, int keyId, Tag128 tag)
    {
        public long PlaintextLength { get; } = length;
        public int KeyId { get; } = keyId;
        public Tag128 Tag { get; } = tag;
    }

    internal static class AesGcmStreamFormat
    {
        private static ReadOnlySpan<byte> MagicBytes => "CTN1"u8;

        public static void ComposeNonce(Span<byte> destination, uint fileNoncePrefix, long chunkIndex)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, fileNoncePrefix);
            BinaryPrimitives.WriteUInt64LittleEndian(destination[4..], unchecked((ulong)chunkIndex));
        }

        public static void InitAadPrefix(Span<byte> aad32, int keyId)
        {
            if (aad32.Length < 32) throw new ArgumentException("AAD buffer must be at least 32 bytes", nameof(aad32));
            MagicBytes.CopyTo(aad32[..4]);
            BinaryPrimitives.WriteInt32LittleEndian(aad32.Slice(4, 4), 1);
            BinaryPrimitives.WriteInt32LittleEndian(aad32.Slice(8, 4), keyId);
        }

        public static void FillAadMutable(Span<byte> aad32, long chunkIndex, long plainLength)
        {
            if (aad32.Length < 32) throw new ArgumentException("AAD buffer must be at least 32 bytes", nameof(aad32));
            BinaryPrimitives.WriteInt64LittleEndian(aad32.Slice(12, 8), chunkIndex);
            BinaryPrimitives.WriteInt64LittleEndian(aad32.Slice(20, 8), plainLength);
            BinaryPrimitives.WriteInt32LittleEndian(aad32.Slice(28, 4), 0);
        }

        public static int ComputeFileHeaderLength(int nonceSize, int tagSize, int keySize)
            => 4 + 4 + 8 + 4 + 4 + nonceSize + tagSize + keySize;

        public static void BuildFileHeader(Span<byte> header, int keyId, uint noncePrefix, ReadOnlySpan<byte> fileKeyNonce, ReadOnlySpan<byte> fileKeyTag, ReadOnlySpan<byte> encryptedFileKey, long totalPlaintextLength, int nonceSize, int tagSize, int keySize)
        {
            int required = ComputeFileHeaderLength(nonceSize, tagSize, keySize);
            if (header.Length < required) throw new ArgumentException("Header buffer too small", nameof(header));
            int offset = 0;
            MagicBytes.CopyTo(header[offset..]);
            offset += MagicBytes.Length;
            BinaryPrimitives.WriteInt32LittleEndian(header[offset..], required);
            offset += sizeof(int);
            BinaryPrimitives.WriteInt64LittleEndian(header[offset..], totalPlaintextLength);
            offset += sizeof(long);
            BinaryPrimitives.WriteInt32LittleEndian(header[offset..], keyId);
            offset += sizeof(int);
            BinaryPrimitives.WriteUInt32LittleEndian(header[offset..], noncePrefix);
            offset += sizeof(uint);
            fileKeyNonce.CopyTo(header[offset..]);
            offset += nonceSize;
            fileKeyTag.CopyTo(header[offset..]);
            offset += tagSize;
            encryptedFileKey.CopyTo(header[offset..]);
        }

        public static int ComputeChunkHeaderLength(int nonceSize, int tagSize)
            => 4 + 4 + 8 + 4 + nonceSize + tagSize;

        public static void BuildChunkHeader(Span<byte> header, int keyId, long chunkIndex, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag, int textLength, int nonceSize, int tagSize)
        {
            int required = ComputeChunkHeaderLength(nonceSize, tagSize);
            if (header.Length < required) throw new ArgumentException("Header buffer too small", nameof(header));
            int offset = 0;
            MagicBytes.CopyTo(header[offset..]);
            offset += MagicBytes.Length;
            BinaryPrimitives.WriteInt32LittleEndian(header[offset..], required);
            offset += sizeof(int);
            BinaryPrimitives.WriteInt64LittleEndian(header[offset..], (long)textLength);
            offset += sizeof(long);
            BinaryPrimitives.WriteInt32LittleEndian(header[offset..], keyId);
            offset += sizeof(int);
            nonce.CopyTo(header[offset..]);
            offset += nonceSize;
            tag.CopyTo(header[offset..]);
        }

        public static async Task<FileHeader> ReadFileHeaderAsync(Stream input, int nonceSize, int tagSize, int keySize, CancellationToken ct)
        {
            byte[] headerPrefix = ArrayPool<byte>.Shared.Rent(8);
            try
            {
                await ReadExactlyAsync(input, headerPrefix, 8, ct).ConfigureAwait(false);
                if (!headerPrefix.AsSpan(0, 4).SequenceEqual(MagicBytes))
                {
                    throw new InvalidDataException("Invalid file format: magic header not found.");
                }
                int headerLength = BinaryPrimitives.ReadInt32LittleEndian(headerPrefix.AsSpan(4));
                if (headerLength != ComputeFileHeaderLength(nonceSize, tagSize, keySize))
                {
                    throw new InvalidDataException("Unsupported file header format (unexpected header length).");
                }
                int remainingHeader = headerLength - 8;
                byte[] headerData = ArrayPool<byte>.Shared.Rent(remainingHeader);
                try
                {
                    await ReadExactlyAsync(input, headerData, remainingHeader, ct).ConfigureAwait(false);
                    long totalLength = BinaryPrimitives.ReadInt64LittleEndian(headerData.AsSpan(0));
                    int keyId = BinaryPrimitives.ReadInt32LittleEndian(headerData.AsSpan(8));
                    uint noncePrefix = BinaryPrimitives.ReadUInt32LittleEndian(headerData.AsSpan(12));
                    byte[] nonce = new byte[nonceSize];
                    Array.Copy(headerData, 16, nonce, 0, nonceSize);
                    Tag128 tag = Tag128.FromSpan(headerData.AsSpan(16 + nonceSize, tagSize));
                    byte[] encryptedKey = new byte[keySize];
                    Array.Copy(headerData, 16 + nonceSize + tagSize, encryptedKey, 0, keySize);
                    return new FileHeader(totalLength, keyId, noncePrefix, nonce, tag, encryptedKey);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(headerData);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerPrefix);
            }
        }

        public static async Task<ChunkHeader> ReadChunkHeaderAsync(Stream input, int nonceSize, int tagSize, uint fileNoncePrefix, long expectedIndex, CancellationToken ct)
        {
            int headerLen = ComputeChunkHeaderLength(nonceSize, tagSize);
            byte[] header = ArrayPool<byte>.Shared.Rent(headerLen);
            try
            {
                await ReadExactlyAsync(input, header, headerLen, ct).ConfigureAwait(false);
                if (!header.AsSpan(0, 4).SequenceEqual(MagicBytes))
                {
                    throw new InvalidDataException("Chunk magic bytes missing or corrupted.");
                }
                int readHeaderLen = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
                if (readHeaderLen != headerLen)
                {
                    throw new InvalidDataException("Invalid chunk header length.");
                }
                long plaintextLength = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8));
                int keyId = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16));
                Span<byte> expectedNonce = stackalloc byte[nonceSize];
                ComposeNonce(expectedNonce, fileNoncePrefix, expectedIndex);
                if (!expectedNonce.SequenceEqual(header.AsSpan(20, nonceSize)))
                {
                    throw new AuthenticationTagMismatchException("Chunk nonce mismatch.");
                }
                Tag128 tag = Tag128.FromSpan(header.AsSpan(20 + nonceSize, tagSize));
                return new ChunkHeader(plaintextLength, keyId, tag);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header);
            }
        }

        public static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
        {
            int offset = 0;
            while (offset < count)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream.");
                }
                offset += bytesRead;
            }
        }
    }
}
