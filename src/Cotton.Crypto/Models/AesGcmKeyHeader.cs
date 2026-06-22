// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto.Internals;
using System;
using System.Buffers.Binary;
using System.IO;

namespace Cotton.Crypto.Models
{
    /// <summary>
    /// Header for an AES-GCM encrypted key: identifiers, nonce, tag, encrypted key material, and plaintext length.
    /// </summary>
    /// <param name="KeyId">Identifier of the encryption key.</param>
    /// <param name="Nonce">AES-GCM nonce.</param>
    /// <param name="Tag">Authentication tag; must be 16 bytes.</param>
    /// <param name="EncryptedKey">Encrypted key material; empty for chunk headers.</param>
    /// <param name="DataLength">Plaintext length in bytes.</param>
    public readonly record struct AesGcmKeyHeader(int KeyId, byte[] Nonce, byte[] Tag, byte[] EncryptedKey, long DataLength = 0)
    {
        /// <summary>
        /// Serializes the header and encrypted key into the file-header binary format.
        /// </summary>
        /// <returns>The serialized header bytes.</returns>
        /// <exception cref="ArgumentException">The authentication tag is not 16 bytes.</exception>
        /// <exception cref="InvalidOperationException">The header could not be serialized.</exception>
        public ReadOnlyMemory<byte> ToBytes()
        {
            int nonceSize = Nonce.Length;
            int tagSize = Tag.Length;
            int keySize = EncryptedKey.Length;
            int totalLen = FileHeader.ComputeLength(nonceSize, tagSize, keySize);
            byte[] buffer = new byte[totalLen];
            // For file header we only support 16-byte authentication tag
            if (tagSize != 16) throw new ArgumentException("Tag span must be 16 bytes", nameof(Tag));
            var fh = new FileHeader(KeyId, 0u, Nonce, Tag128.FromSpan(Tag.AsSpan(0, 16)), EncryptedKey, DataLength);
            if (!FileHeader.TryWrite(buffer, fh, nonceSize, tagSize, keySize))
            {
                throw new InvalidOperationException("Failed to serialize header.");
            }
            return buffer;
        }

        /// <summary>
        /// Reads an AES-GCM key header (file-header or compact chunk-header layout) from a stream.
        /// </summary>
        /// <param name="stream">Stream positioned at the start of the header.</param>
        /// <param name="nonceSize">Nonce size in bytes.</param>
        /// <param name="tagSize">Authentication tag size in bytes.</param>
        /// <returns>The parsed header.</returns>
        /// <exception cref="InvalidDataException">The header is invalid or its layout is unsupported.</exception>
        public static AesGcmKeyHeader FromStream(Stream stream, int nonceSize, int tagSize)
        {
            // Peek prefix and full header length
            Span<byte> prefix = stackalloc byte[8];
            stream.ReadExactly(prefix);
            if (!FormatConstants.TryGetVersion(prefix[..4], out _))
            {
                throw new InvalidDataException("Invalid magic number in header.");
            }
            int headerLength = BinaryPrimitives.ReadInt32LittleEndian(prefix.Slice(4, 4));

            byte[] rest = new byte[headerLength - 8];
            stream.ReadExactly(rest);

            // Build full header and try parse as FileHeader
            byte[] full = new byte[headerLength];
            prefix.CopyTo(full);
            rest.CopyTo(full.AsSpan(8));
            // Best-effort: encrypted key length is dynamic; try plausible sizes
            // First try: treat as file header with encrypted key length = remaining - (uint + nonceSize + tagSize)
            int remaining = headerLength - (4 + 4 + 8 + 4); // after magic+len+dataLen+keyId
            if (remaining >= (sizeof(uint) + nonceSize + tagSize))
            {
                int encKeyLen = remaining - (sizeof(uint) + nonceSize + tagSize);
                if (encKeyLen >= 0 && FileHeader.TryRead(full, nonceSize, tagSize, encKeyLen, out FileHeader fh))
                {
                    // DTO
                    byte[] tagBytes = new byte[tagSize];
                    fh.Tag.CopyTo(tagBytes);
                    return new AesGcmKeyHeader(fh.KeyId, fh.Nonce, tagBytes, fh.EncryptedKey, fh.TotalPlaintextLength);
                }
            }

            // Fallback: compact chunk header (no nonce, no encrypted key)
            if (!ChunkHeader.TryRead(full, tagSize, out ChunkHeader ch))
            {
                throw new InvalidDataException("Unsupported header layout or length.");
            }
            byte[] tagOnly = new byte[tagSize];
            ch.Tag.CopyTo(tagOnly);
            return new AesGcmKeyHeader(ch.KeyId, [], tagOnly, [], ch.PlaintextLength);
        }
    }
}
