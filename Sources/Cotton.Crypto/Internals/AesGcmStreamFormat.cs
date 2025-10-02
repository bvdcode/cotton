using System.Buffers.Binary;

namespace Cotton.Crypto.Internals
{
    // Internal representation of the file-level header
    internal readonly struct FileHeader(long totalLength, int keyId, byte[] nonce, byte[] tag, byte[] encryptedKey)
    {
        public long TotalPlaintextLength { get; } = totalLength;
        public int KeyId { get; } = keyId;
        public byte[] Nonce { get; } = nonce;
        public byte[] Tag { get; } = tag;
        public byte[] EncryptedKey { get; } = encryptedKey;
    }

    // Internal representation of a per-chunk header
    internal readonly struct ChunkHeader(long length, int keyId, byte[] nonce, byte[] tag)
    {
        public long PlaintextLength { get; } = length;
        public int KeyId { get; } = keyId;
        public byte[] Nonce { get; } = nonce;
        public byte[] Tag { get; } = tag;
    }

    // Compact 128-bit tag container used in headers
    internal readonly struct Tag128(ulong low, ulong high)
    {
        public ulong Low { get; } = low;
        public ulong High { get; } = high;
    }

    internal static class AesGcmStreamFormat
    {
        // Magic header marker shared across file and chunk headers
        private static ReadOnlySpan<byte> MagicBytes => "CTN1"u8;

        // Compose a 12-byte nonce (IV) for AES-GCM from a 4-byte keyId and 8-byte chunk index (both little-endian)
        public static void ComposeNonce(Span<byte> destination, int keyId, long chunkIndex)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, unchecked((uint)keyId));
            BinaryPrimitives.WriteUInt64LittleEndian(destination[4..], unchecked((ulong)chunkIndex));
        }

        public static void WriteFileHeader(Stream output, int keyId, byte[] fileKeyNonce, byte[] fileKeyTag, byte[] encryptedFileKey, long totalPlaintextLength, int nonceSize, int tagSize, int keySize)
        {
            // Calculate header length (includes magic + all header fields)
            int headerLen = 4 + 4 + 8 + 4 + nonceSize + tagSize + keySize;
            Span<byte> header = stackalloc byte[headerLen];
            int offset = 0;
            // Magic "CTN1"
            MagicBytes.CopyTo(header[offset..]);
            offset += MagicBytes.Length;                        // 4 bytes
            BitConverter.TryWriteBytes(header[offset..], headerLen);
            offset += sizeof(int);                              // 4 bytes (header length)
            BitConverter.TryWriteBytes(header[offset..], totalPlaintextLength);
            offset += sizeof(long);                             // 8 bytes (total plaintext length)
            BitConverter.TryWriteBytes(header[offset..], keyId);
            offset += sizeof(int);                              // 4 bytes (key ID)
            // Copy file key nonce and tag
            fileKeyNonce.CopyTo(header[offset..]);
            offset += nonceSize;
            fileKeyTag.CopyTo(header[offset..]);
            offset += tagSize;
            // Copy encrypted file key
            encryptedFileKey.CopyTo(header[offset..]);
            // Write the header to output
            output.Write(header);
        }

        public static void WriteChunkHeader(Stream output, int keyId, long chunkIndex, Tag128 tag, int textLength, int nonceSize, int tagSize)
        {
            int headerLen = 4 + 4 + 8 + 4 + nonceSize + tagSize;  // should equal 48 for 12/16 sizes
            Span<byte> header = stackalloc byte[headerLen];
            int offset = 0;
            MagicBytes.CopyTo(header[offset..]);
            offset += MagicBytes.Length;                        // 4 bytes
            BitConverter.TryWriteBytes(header[offset..], headerLen);
            offset += sizeof(int);                              // 4 bytes (header length)
            BitConverter.TryWriteBytes(header[offset..], (long)textLength);
            offset += sizeof(long);                             // 8 bytes (length of chunk)
            BitConverter.TryWriteBytes(header[offset..], keyId);
            offset += sizeof(int);                              // 4 bytes (key ID)
            // Reconstruct the 12-byte nonce for this chunk (keyId + chunkIndex)
            Span<byte> nonceSpan = stackalloc byte[nonceSize];
            ComposeNonce(nonceSpan, keyId, chunkIndex);
            nonceSpan.CopyTo(header[offset..]);
            offset += nonceSize;
            // Write the 16-byte authentication tag from the Tag128 struct
            BinaryPrimitives.WriteUInt64LittleEndian(header[offset..], tag.Low);
            BinaryPrimitives.WriteUInt64LittleEndian(header[(offset + 8)..], tag.High);
            offset += tagSize;
            output.Write(header);
        }

        public static async Task<FileHeader> ReadFileHeaderAsync(Stream input, int nonceSize, int tagSize, int keySize, CancellationToken ct)
        {
            // Read magic (4 bytes) and header length (4 bytes)
            byte[] headerPrefix = new byte[8];
            await ReadExactlyAsync(input, headerPrefix, 8, ct).ConfigureAwait(false);
            // Verify magic bytes
            if (!headerPrefix.AsSpan(0, 4).SequenceEqual(MagicBytes))
            {
                throw new InvalidDataException("Invalid file format: magic header not found.");
            }
            int headerLength = BitConverter.ToInt32(headerPrefix, 4);
            if (headerLength != 4 + 4 + 8 + 4 + nonceSize + tagSize + keySize)
            {
                throw new InvalidDataException("Unsupported file header format (unexpected header length).");
            }
            // Read remaining header bytes (after the first 8 bytes we already read)
            int remainingHeader = headerLength - 8;
            byte[] headerData = new byte[remainingHeader];
            await ReadExactlyAsync(input, headerData, remainingHeader, ct).ConfigureAwait(false);
            // Parse file header fields
            long totalLength = BitConverter.ToInt64(headerData, 0);
            int keyId = BitConverter.ToInt32(headerData, 8);
            // Next nonceSize bytes: file key nonce
            byte[] nonce = new byte[nonceSize];
            Array.Copy(headerData, 12, nonce, 0, nonceSize);
            // Next tagSize bytes: file key tag
            byte[] tag = new byte[tagSize];
            Array.Copy(headerData, 12 + nonceSize, tag, 0, tagSize);
            // Next keySize bytes: encrypted file key
            byte[] encryptedKey = new byte[keySize];
            Array.Copy(headerData, 12 + nonceSize + tagSize, encryptedKey, 0, keySize);
            return new FileHeader(totalLength, keyId, nonce, tag, encryptedKey);
        }

        public static async Task<ChunkHeader> ReadChunkHeaderAsync(Stream input, int nonceSize, int tagSize, CancellationToken ct)
        {
            int headerLen = 4 + 4 + 8 + 4 + nonceSize + tagSize;
            byte[] header = new byte[headerLen];
            await ReadExactlyAsync(input, header, header.Length, ct).ConfigureAwait(false);
            // Verify magic
            if (!header.AsSpan(0, 4).SequenceEqual(MagicBytes))
            {
                throw new InvalidDataException("Chunk magic bytes missing or corrupted.");
            }
            int readHeaderLen = BitConverter.ToInt32(header, 4);
            if (readHeaderLen != headerLen)
            {
                throw new InvalidDataException("Invalid chunk header length.");
            }
            long plaintextLength = BitConverter.ToInt64(header, 8);
            int keyId = BitConverter.ToInt32(header, 16);
            byte[] nonce = new byte[nonceSize];
            Array.Copy(header, 20, nonce, 0, nonceSize);
            byte[] tag = new byte[tagSize];
            Array.Copy(header, 20 + nonceSize, tag, 0, tagSize);
            return new ChunkHeader(plaintextLength, keyId, nonce, tag);
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
