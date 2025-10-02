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
            BinaryPrimitives.WriteInt32LittleEndian(header[offset..], headerLen);
            offset += sizeof(int);                              // 4 bytes (header length)
            BinaryPrimitives.WriteInt64LittleEndian(header[offset..], totalPlaintextLength);
            offset += sizeof(long);                             // 8 bytes (total plaintext length)
            BinaryPrimitives.WriteInt32LittleEndian(header[offset..], keyId);
            offset += sizeof(int);                              // 4 bytes (key ID)
            // Copy file key nonce and tag (raw bytes, do not change order)
            fileKeyNonce.CopyTo(header[offset..]);
            offset += nonceSize;
            fileKeyTag.CopyTo(header[offset..]);
            offset += tagSize;
            // Copy encrypted file key (raw bytes)
            encryptedFileKey.CopyTo(header[offset..]);
            // Write the header to output
            output.Write(header);
        }

        public static void WriteChunkHeader(Stream output, int keyId, long chunkIndex, ReadOnlySpan<byte> tag, int textLength, int nonceSize, int tagSize)
        {
            int headerLen = 4 + 4 + 8 + 4 + nonceSize + tagSize;  // should equal 48 for 12/16 sizes
            Span<byte> header = stackalloc byte[headerLen];
            int offset = 0;
            MagicBytes.CopyTo(header[offset..]);
            offset += MagicBytes.Length;                        // 4 bytes
            BinaryPrimitives.WriteInt32LittleEndian(header[offset..], headerLen);
            offset += sizeof(int);                              // 4 bytes (header length)
            BinaryPrimitives.WriteInt64LittleEndian(header[offset..], (long)textLength);
            offset += sizeof(long);                             // 8 bytes (length of chunk)
            BinaryPrimitives.WriteInt32LittleEndian(header[offset..], keyId);
            offset += sizeof(int);                              // 4 bytes (key ID)
            // Reconstruct the 12-byte nonce for this chunk (keyId + chunkIndex) and write as raw bytes
            Span<byte> nonceSpan = stackalloc byte[nonceSize];
            ComposeNonce(nonceSpan, keyId, chunkIndex);
            nonceSpan.CopyTo(header[offset..]);
            offset += nonceSize;
            // Copy the authentication tag as raw bytes, do not reinterpret endianness
            tag.CopyTo(header[offset..]);
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
            int headerLength = BinaryPrimitives.ReadInt32LittleEndian(headerPrefix.AsSpan(4));
            if (headerLength != 4 + 4 + 8 + 4 + nonceSize + tagSize + keySize)
            {
                throw new InvalidDataException("Unsupported file header format (unexpected header length).");
            }
            // Read remaining header bytes (after the first 8 bytes we already read)
            int remainingHeader = headerLength - 8;
            byte[] headerData = new byte[remainingHeader];
            await ReadExactlyAsync(input, headerData, remainingHeader, ct).ConfigureAwait(false);
            // Parse file header fields (LE)
            long totalLength = BinaryPrimitives.ReadInt64LittleEndian(headerData.AsSpan(0));
            int keyId = BinaryPrimitives.ReadInt32LittleEndian(headerData.AsSpan(8));
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
            int readHeaderLen = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
            if (readHeaderLen != headerLen)
            {
                throw new InvalidDataException("Invalid chunk header length.");
            }
            long plaintextLength = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8));
            int keyId = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16));
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
