using System.Buffers.Binary;
using Cotton.Crypto.Internals;

namespace Cotton.Crypto.Models
{
    // DTO projection for external use; (de)serialization delegates to Internals.*
    public readonly record struct AesGcmKeyHeader(int KeyId, byte[] Nonce, byte[] Tag, byte[] EncryptedKey, long DataLength = 0)
    {
        public ReadOnlyMemory<byte> ToBytes()
        {
            int nonceSize = Nonce.Length;
            int tagSize = Tag.Length;
            int keySize = EncryptedKey.Length;
            int totalLen = FileHeader.ComputeLength(nonceSize, tagSize, keySize);
            byte[] buffer = new byte[totalLen];
            var fh = new FileHeader(KeyId, 0u, Nonce, Tag128.FromSpan(Tag), EncryptedKey, DataLength);
            if (!FileHeader.TryWrite(buffer, fh, nonceSize, tagSize, keySize))
                throw new InvalidOperationException("Failed to serialize header.");
            return buffer;
        }

        public static AesGcmKeyHeader FromStream(Stream stream, int nonceSize, int tagSize)
        {
            // Peek prefix and full header length
            Span<byte> prefix = stackalloc byte[8];
            stream.ReadExactly(prefix);
            if (!prefix[..4].SequenceEqual(FormatConstants.MagicBytes))
                throw new InvalidDataException("Invalid magic number in header.");
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
                if (encKeyLen >= 0 && FileHeader.TryRead(full, nonceSize, tagSize, encKeyLen, out var fh))
                {
                    // DTO
                    byte[] tagBytes = new byte[tagSize];
                    fh.Tag.CopyTo(tagBytes);
                    return new AesGcmKeyHeader(fh.KeyId, fh.Nonce, tagBytes, fh.EncryptedKey, fh.TotalPlaintextLength);
                }
            }

            // Fallback: compact chunk header (no nonce, no encrypted key)
            if (!ChunkHeader.TryRead(full, tagSize, out var ch))
                throw new InvalidDataException("Unsupported header layout or length.");
            byte[] tagOnly = new byte[tagSize];
            ch.Tag.CopyTo(tagOnly);
            return new AesGcmKeyHeader(ch.KeyId, Array.Empty<byte>(), tagOnly, Array.Empty<byte>(), ch.PlaintextLength);
        }
    }
}
