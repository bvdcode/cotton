// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cotton.Crypto.Internals
{
    // FileHeader and ChunkHeader moved to Headers.cs

    internal static class AesGcmStreamFormat
    {
        public static void ComposeNonce(Span<byte> destination, uint fileNoncePrefix, long chunkIndex)
        {
            // Guard: prevent wrapping the 64-bit counter in the nonce
            if (unchecked((ulong)chunkIndex) == ulong.MaxValue)
            {
                throw new InvalidOperationException("Maximum number of chunks per file is 2^64-1. Counter reached ulong.MaxValue.");
            }

            BinaryPrimitives.WriteUInt32LittleEndian(destination, fileNoncePrefix);
            BinaryPrimitives.WriteUInt64LittleEndian(destination[4..], unchecked((ulong)chunkIndex));
        }

        public static void InitAadPrefix(Span<byte> aad32, int keyId)
        {
            if (aad32.Length < 32) throw new ArgumentException("AAD buffer must be at least 32 bytes", nameof(aad32));
            FormatConstants.MagicBytes.CopyTo(aad32[..4]);
            BinaryPrimitives.WriteInt32LittleEndian(aad32.Slice(4, 4), FormatConstants.CurrentVersion);
            BinaryPrimitives.WriteInt32LittleEndian(aad32.Slice(8, 4), keyId);
        }

        public static void FillAadMutable(Span<byte> aad32, long chunkIndex, long plainLength)
        {
            if (aad32.Length < 32) throw new ArgumentException("AAD buffer must be at least 32 bytes", nameof(aad32));
            BinaryPrimitives.WriteInt64LittleEndian(aad32.Slice(12, 8), chunkIndex);
            BinaryPrimitives.WriteInt64LittleEndian(aad32.Slice(20, 8), plainLength);
            BinaryPrimitives.WriteInt32LittleEndian(aad32.Slice(28, 4), 0);
        }

        public static byte[] BuildKeyAad(int keyId, uint noncePrefix, ReadOnlySpan<byte> fileKeyNonce, long totalPlaintextLength, int nonceSize, int tagSize, int keySize)
        {
            if (fileKeyNonce.Length < nonceSize) throw new ArgumentException("Nonce span shorter than nonce size", nameof(fileKeyNonce));
            int headerLen = ComputeFileHeaderLength(nonceSize, tagSize, keySize);
            int aadLen = 4 + 4 + 8 + 4 + 4 + nonceSize;
            byte[] aad = new byte[aadLen];
            int offset = 0;
            FormatConstants.MagicBytes.CopyTo(aad.AsSpan(offset)); offset += 4;
            BinaryPrimitives.WriteInt32LittleEndian(aad.AsSpan(offset), headerLen); offset += 4;
            BinaryPrimitives.WriteInt64LittleEndian(aad.AsSpan(offset), totalPlaintextLength); offset += 8;
            BinaryPrimitives.WriteInt32LittleEndian(aad.AsSpan(offset), keyId); offset += 4;
            BinaryPrimitives.WriteUInt32LittleEndian(aad.AsSpan(offset), noncePrefix); offset += 4;
            fileKeyNonce[..nonceSize].CopyTo(aad.AsSpan(offset));
            return aad;
        }

        public static int ComputeFileHeaderLength(int nonceSize, int tagSize, int keySize)
            => FileHeader.ComputeLength(nonceSize, tagSize, keySize);

        public static void BuildFileHeader(Span<byte> header, int keyId, uint noncePrefix, ReadOnlySpan<byte> fileKeyNonce, Tag128 fileKeyTag, ReadOnlySpan<byte> encryptedFileKey, long totalPlaintextLength, int nonceSize, int tagSize, int keySize)
        {
            var fh = new FileHeader(keyId, noncePrefix, fileKeyNonce.ToArray(), fileKeyTag, encryptedFileKey.ToArray(), totalPlaintextLength);
            if (!FileHeader.TryWrite(header, fh, nonceSize, tagSize, keySize))
                throw new ArgumentException("Header buffer too small", nameof(header));
        }

        public static int ComputeChunkHeaderLength(int tagSize)
            => ChunkHeader.ComputeLength(tagSize); // magic + headerLen + plainLen + keyId + tag

        public static void BuildChunkHeader(Span<byte> header, int keyId, Tag128 tag, int textLength, int tagSize)
        {
            var chunkHeader = new ChunkHeader(textLength, keyId, tag);
            if (!ChunkHeader.TryWrite(header, chunkHeader, tagSize))
                throw new ArgumentException("Header buffer too small", nameof(header));
        }

        public static async Task<FileHeader> ReadFileHeaderAsync(Stream input, int nonceSize, int tagSize, int keySize, CancellationToken ct)
        {
            byte[] headerPrefix = ArrayPool<byte>.Shared.Rent(8);
            try
            {
                await ReadExactlyAsync(input, headerPrefix, 8, ct).ConfigureAwait(false);
                if (!headerPrefix.AsSpan(0, 4).SequenceEqual(FormatConstants.MagicBytes))
                {
                    if (headerPrefix.AsSpan(0, 4).SequenceEqual("CTN1"u8))
                    {
#pragma warning disable CS0618 // OBSOLETE TRANSITION: preserve a precise CTN1 upgrade error during the 0.5 cutover.
                        throw new Ctn1NotSupportedException();
#pragma warning restore CS0618
                    }

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
                    // Stitch full header then parse via internal TryRead
                    byte[] full = ArrayPool<byte>.Shared.Rent(headerLength);
                    try
                    {
                        headerPrefix.AsSpan(0, 8).CopyTo(full);
                        headerData.AsSpan(0, remainingHeader).CopyTo(full.AsSpan(8));
                        if (!FileHeader.TryRead(full, nonceSize, tagSize, keySize, out FileHeader fh))
                            throw new InvalidDataException("Invalid file header contents.");
                        return fh;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(full, clearArray: false);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(headerData, clearArray: false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerPrefix, clearArray: false);
            }
        }

        public static async Task<ChunkHeader> ReadChunkHeaderAsync(Stream input, int tagSize, CancellationToken ct)
        {
            int headerLen = ComputeChunkHeaderLength(tagSize);
            byte[] header = ArrayPool<byte>.Shared.Rent(headerLen);
            try
            {
                await ReadExactlyAsync(input, header, headerLen, ct).ConfigureAwait(false);
                if (!ChunkHeader.TryRead(header, tagSize, out ChunkHeader ch))
                {
                    throw new InvalidDataException("Invalid or corrupted chunk header.");
                }
                return ch;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header, clearArray: false);
            }
        }

        public static async Task<ChunkHeader?> TryReadChunkHeaderAsync(Stream input, int tagSize, CancellationToken ct)
        {
            int headerLen = ComputeChunkHeaderLength(tagSize);
            byte[] header = ArrayPool<byte>.Shared.Rent(headerLen);
            try
            {
                int bytesRead = await ReadExactlyOrEndAsync(input, header, headerLen, ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    return null;
                }
                if (bytesRead < headerLen)
                {
                    throw new EndOfStreamException("Unexpected end of stream while reading chunk header.");
                }
                if (!ChunkHeader.TryRead(header, tagSize, out ChunkHeader ch))
                {
                    throw new InvalidDataException("Invalid or corrupted chunk header.");
                }
                return ch;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header, clearArray: false);
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

        private static async Task<int> ReadExactlyOrEndAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
        {
            int offset = 0;
            while (offset < count)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    return offset;
                }
                offset += bytesRead;
            }
            return offset;
        }
    }
}
